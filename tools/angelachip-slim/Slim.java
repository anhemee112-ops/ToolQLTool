import java.io.*;
import java.lang.classfile.*;
import java.lang.classfile.constantpool.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;
import java.util.jar.*;
import java.util.regex.*;
import java.util.zip.*;

/**
 * Cắt gọn AngelAChip.jar: thay lớp Main bằng bản gọn (không menu/hộp thoại), rồi chỉ giữ các lớp còn với tới được.
 *
 * Cách dùng: java Slim.java <AngelAChip.jar gốc> <thư mục lớp Main mới đã biên dịch> <jar kết quả>
 *
 * Gốc (root) giữ lại cho an toàn vì được nạp động hoặc là API của game: javax/**, org/microemu/cldc/**,
 * org/microemu/microedition/**, org/microemu/midp/**, cùng mọi tên lớp ghi trong tài nguyên (device.xml...).
 */
public class Slim {
    static final String NEW_MAIN = "org/microemu/app/Main";
    static final Pattern CLASS_NAME = Pattern.compile("org[./]microemu[\\w./$]*|javax[./][\\w./$]*");

    public static void main(String[] args) throws Exception {
        Path src = Path.of(args[0]), newClasses = Path.of(args[1]), out = Path.of(args[2]);

        Map<String, byte[]> classes = new TreeMap<>();
        Map<String, byte[]> resources = new TreeMap<>();
        try (ZipFile z = new ZipFile(src.toFile())) {
            for (ZipEntry e : Collections.list(z.entries())) {
                if (e.isDirectory()) continue;
                byte[] data = z.getInputStream(e).readAllBytes();
                if (e.getName().endsWith(".class")) classes.put(e.getName().substring(0, e.getName().length() - 6), data);
                else if (!e.getName().startsWith("META-INF/")) resources.put(e.getName(), data);
            }
        }
        // Lớp Main mới (và lớp con của nó) thay lớp Main cũ.
        classes.keySet().removeIf(n -> n.equals(NEW_MAIN) || n.startsWith(NEW_MAIN + "$"));
        try (var walk = Files.walk(newClasses)) {
            for (Path p : (Iterable<Path>) walk.filter(f -> f.toString().endsWith(".class"))::iterator) {
                String name = newClasses.relativize(p).toString().replace('\\', '/');
                classes.put(name.substring(0, name.length() - 6), Files.readAllBytes(p));
            }
        }

        Deque<String> stack = new ArrayDeque<>();
        Set<String> keep = new TreeSet<>();
        for (String n : classes.keySet()) {
            if (n.equals(NEW_MAIN) || n.startsWith("javax/") || n.startsWith("org/microemu/cldc/")
                    || n.startsWith("org/microemu/microedition/") || n.startsWith("org/microemu/midp/")) {
                if (keep.add(n)) stack.push(n);
            }
        }
        for (var r : resources.entrySet()) {
            if (!r.getKey().endsWith(".xml") && !r.getKey().endsWith(".properties")) continue;
            Matcher m = CLASS_NAME.matcher(new String(r.getValue(), StandardCharsets.UTF_8));
            while (m.find()) mark(m.group().replace('.', '/'), classes, keep, stack);
        }
        while (!stack.isEmpty()) {
            String n = stack.pop();
            current = n;
            ClassModel cm = ClassFile.of().parse(classes.get(n));
            for (var entry : cm.constantPool()) {
                if (entry instanceof ClassEntry ce) mark(ce.asInternalName(), classes, keep, stack);
                else if (entry instanceof Utf8Entry u) {
                    Matcher m = CLASS_NAME.matcher(u.stringValue());
                    while (m.find()) mark(m.group().replace('.', '/'), classes, keep, stack);
                }
            }
        }

        Manifest mf = new Manifest();
        mf.getMainAttributes().put(java.util.jar.Attributes.Name.MANIFEST_VERSION, "1.0");
        mf.getMainAttributes().put(java.util.jar.Attributes.Name.MAIN_CLASS, "org.microemu.app.Main");
        Files.createDirectories(out.toAbsolutePath().getParent());
        try (JarOutputStream jar = new JarOutputStream(Files.newOutputStream(out), mf)) {
            for (String n : keep) put(jar, n + ".class", classes.get(n));
            for (var r : resources.entrySet()) put(jar, r.getKey(), r.getValue());
        }

        long total = classes.size();
        System.out.printf("Lớp: giữ %d / %d, bỏ %d. Tài nguyên: %d. Jar: %d KB%n", keep.size(), total, total - keep.size(), resources.size(), Files.size(out) / 1024);
        for (String why : args.length > 3 ? args[3].split(",") : new String[0]) {
            StringBuilder chain = new StringBuilder(why);
            for (String p = WHY.get(why); p != null && chain.length() < 400; p = WHY.get(p)) chain.append(" <- ").append(p);
            System.out.println("Vì sao giữ: " + chain);
        }
        System.out.println("Bỏ: " + String.join(" ", classes.keySet().stream().filter(n -> !keep.contains(n)).toList()));
    }

    /** Lớp nào kéo lớp nào vào (để tra "vì sao lớp này còn"). */
    static final Map<String, String> WHY = new HashMap<>();
    static String current = "(gốc)";

    static void mark(String name, Map<String, byte[]> classes, Set<String> keep, Deque<String> stack) {
        if (name.startsWith("[")) {                                  // mảng: "[S" là mảng short (không phải lớp S), "[Lx;" là mảng lớp x
            name = name.replaceFirst("^\\[+", "");
            if (!(name.startsWith("L") && name.endsWith(";"))) return;
            name = name.substring(1, name.length() - 1);
        }
        if (classes.containsKey(name) && keep.add(name)) {
            WHY.put(name, current);
            stack.push(name);
        }
    }

    static void put(JarOutputStream jar, String name, byte[] data) throws IOException {
        ZipEntry e = new ZipEntry(name);
        e.setTime(0);
        jar.putNextEntry(e);
        jar.write(data);
        jar.closeEntry();
    }
}
