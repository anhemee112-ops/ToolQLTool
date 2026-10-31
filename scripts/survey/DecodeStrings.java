import java.lang.reflect.Method;
import java.net.URL;
import java.net.URLClassLoader;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.regex.*;
import java.util.stream.*;

/** Giải các chuỗi Y.b("...") trong mã CFR bằng chính lớp nro.Y của jar. Dùng: DecodeStrings <jar> <thư mục vào> <thư mục ra> */
public class DecodeStrings {
    static String unescape(String s) {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            if (c != '\\') { sb.append(c); continue; }
            char n = s.charAt(++i);
            switch (n) {
                case 'n': sb.append('\n'); break;
                case 't': sb.append('\t'); break;
                case 'r': sb.append('\r'); break;
                case 'b': sb.append('\b'); break;
                case 'f': sb.append('\f'); break;
                case '"': sb.append('"'); break;
                case '\'': sb.append('\''); break;
                case '\\': sb.append('\\'); break;
                case 'u': {
                    while (s.charAt(i + 1) == 'u') i++;
                    sb.append((char) Integer.parseInt(s.substring(i + 1, i + 5), 16));
                    i += 4;
                    break;
                }
                default:
                    if (n >= '0' && n <= '7') {
                        int j = i, v = 0;
                        while (j < s.length() && j < i + 3 && s.charAt(j) >= '0' && s.charAt(j) <= '7') v = v * 8 + (s.charAt(j++) - '0');
                        sb.append((char) v);
                        i = j - 1;
                    } else sb.append(n);
            }
        }
        return sb.toString();
    }

    static String quote(String s) {
        StringBuilder sb = new StringBuilder("\"");
        for (char c : s.toCharArray()) {
            if (c == '"' || c == '\\') sb.append('\\').append(c);
            else if (c == '\n') sb.append("\\n");
            else if (c == '\r') sb.append("\\r");
            else if (c == '\t') sb.append("\\t");
            else if (c < 32) sb.append(String.format("\\u%04x", (int) c));
            else sb.append(c);
        }
        return sb.append('"').toString();
    }

    public static void main(String[] a) throws Exception {
        try (URLClassLoader cl = new URLClassLoader(new URL[]{Paths.get(a[0]).toUri().toURL()})) {
            Method m = cl.loadClass("nro.Y").getMethod("b", String.class);
            Pattern p = Pattern.compile("(?:nro\\.)?Y\\.b\\(\\(?String\\)?\\s*\"((?:[^\"\\\\]|\\\\.)*)\"\\)|(?:nro\\.)?Y\\.b\\(\"((?:[^\"\\\\]|\\\\.)*)\"\\)");
            Path in = Paths.get(a[1]), out = Paths.get(a[2]);
            int files = 0, strs = 0, fail = 0;
            try (Stream<Path> st = Files.walk(in)) {
                for (Path f : st.filter(x -> x.toString().endsWith(".java")).collect(Collectors.toList())) {
                    String src = new String(Files.readAllBytes(f), StandardCharsets.UTF_8);
                    Matcher mt = p.matcher(src);
                    StringBuilder sb = new StringBuilder();
                    while (mt.find()) {
                        String lit = mt.group(1) != null ? mt.group(1) : mt.group(2);
                        String rep;
                        try { rep = quote((String) m.invoke(null, unescape(lit))); strs++; }
                        catch (Throwable t) { rep = mt.group(); fail++; }
                        mt.appendReplacement(sb, Matcher.quoteReplacement(rep));
                    }
                    mt.appendTail(sb);
                    Path dst = out.resolve(in.relativize(f));
                    Files.createDirectories(dst.getParent());
                    Files.write(dst, sb.toString().getBytes(StandardCharsets.UTF_8));
                    files++;
                }
            }
            System.out.println("files=" + files + " decoded=" + strs + " failed=" + fail);
        }
    }
}
