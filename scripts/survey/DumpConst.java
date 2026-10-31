import java.lang.reflect.*;
import java.net.*;
import java.nio.file.*;
import java.util.Arrays;
/** In các mảng/hằng int tĩnh của lớp. Dùng: DumpConst <jar> <lớp>... */
public class DumpConst {
    public static void main(String[] a) throws Exception {
        try (URLClassLoader cl = new URLClassLoader(new URL[]{Paths.get(a[0]).toUri().toURL()})) {
            for (int i = 1; i < a.length; i++) {
                Class<?> c = cl.loadClass(a[i]);
                for (Field f : c.getDeclaredFields()) {
                    if (!Modifier.isStatic(f.getModifiers())) continue;
                    f.setAccessible(true);
                    Object v = f.get(null);
                    if (v instanceof int[]) System.out.println(a[i] + "." + f.getName() + " = " + Arrays.toString((int[]) v));
                    else if (v instanceof Integer) System.out.println(a[i] + "." + f.getName() + " = " + v);
                }
            }
        }
    }
}
