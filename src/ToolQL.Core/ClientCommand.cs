using System.Text.RegularExpressions;

namespace ToolQL.Core;

/// <summary>Cách mở một tiến trình client: tệp chạy, chuỗi đối số và thư mục làm việc.</summary>
public sealed record ClientLaunchSpec(string FileName, string Arguments, string WorkingDirectory);

/// <summary>
/// Dựng lệnh mở MỘT cửa sổ client: jar game (<c>nro.hero69</c>) chạy bằng trình J2ME AngelChip (MicroEmulator), với <c>user.home</c> riêng
/// của acc để kho RMS tách biệt. Chạy thẳng lớp MIDlet để bỏ qua màn Launcher. JVM đặt gọn (đo ~96 MB RAM mỗi cửa sổ) và
/// <c>javaw.exe</c> để không hiện cửa sổ console.
/// </summary>
public static class ClientCommand
{
    public const string MainClass = "nro.hero69";

    private static readonly Regex IdPattern = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);

    // IgnoreUnrecognizedVMOptions: Java cũ hơn (VPS) bỏ qua cờ không biết (UseCompactObjectHeaders chỉ có từ Java 24) thay vì không chạy.
    private const string JvmArgs = "-XX:+IgnoreUnrecognizedVMOptions -Xmx64m -Xms8m -XX:+UseSerialGC -XX:TieredStopAtLevel=1 -XX:CICompilerCount=1 -Xss256k " +
                                   "-XX:ReservedCodeCacheSize=16m -XX:MaxMetaspaceSize=64m -XX:MinHeapFreeRatio=10 -XX:MaxHeapFreeRatio=20 -XX:-UsePerfData " +
                                   "-Dsun.java2d.d3d=false -Dsun.java2d.noddraw=true -XX:+UseCompactObjectHeaders";

    /// <summary>Ghép lệnh; không kiểm tệp có tồn tại (xem <see cref="Check"/>). Ném <see cref="ArgumentException"/> nếu đường dẫn có dấu ngoặc kép hoặc mã định danh lạ.</summary>
    public static ClientLaunchSpec Build(AppSettings s, string accountDir, string id)
    {
        if (!IdPattern.IsMatch(id)) throw new ArgumentException("Mã cửa sổ chỉ gồm chữ, số, gạch.", nameof(id));
        foreach (string path in new[] { accountDir, s.JavaPath, s.EmulatorJarPath, s.GameJarPath })
            if (path.Contains('"')) throw new ArgumentException("Đường dẫn không được chứa dấu ngoặc kép: " + path);

        string home = Path.Combine(accountDir, "home");
        string args = $"-Duser.home=\"{home}\" {JvmArgs} -jar \"{s.EmulatorJarPath}\" --rms file --id {id} " +
                      $"--resizableDevice {s.ClientWidth} {s.ClientHeight} --appclasspath \"{s.GameJarPath}\" {MainClass}";
        return new ClientLaunchSpec(PreferWindowless(s.JavaPath), args, accountDir);
    }

    /// <summary>Lỗi cấu hình (thiếu Java, trình chạy, jar game) bằng tiếng Việt; null nếu đủ.</summary>
    public static string? Check(AppSettings s)
    {
        if (s.JavaPath.Length == 0 || !File.Exists(s.JavaPath)) return "Chưa chọn Java (javaw.exe) hoặc tệp không tồn tại. Chọn trong Cài đặt.";
        if (s.EmulatorJarPath.Length == 0 || !File.Exists(s.EmulatorJarPath)) return "Chưa chọn trình chạy J2ME (AngelAChip.jar) hoặc tệp không tồn tại. Chọn trong Cài đặt.";
        if (s.GameJarPath.Length == 0 || !File.Exists(s.GameJarPath)) return "Chưa chọn jar game (Dragonboy250 v3.5.jar) hoặc tệp không tồn tại. Chọn trong Cài đặt.";
        return null;
    }

    /// <summary>Nếu <paramref name="javaPath"/> là <c>java.exe</c> mà cạnh nó có <c>javaw.exe</c> thì dùng <c>javaw.exe</c> (không có console).</summary>
    public static string PreferWindowless(string javaPath)
    {
        if (!string.Equals(Path.GetFileName(javaPath), "java.exe", StringComparison.OrdinalIgnoreCase)) return javaPath;
        string windowless = Path.Combine(Path.GetDirectoryName(javaPath) ?? "", "javaw.exe");
        return File.Exists(windowless) ? windowless : javaPath;
    }
}

public readonly record struct WindowRect(int X, int Y, int Width, int Height);

/// <summary>
/// Xếp các cửa sổ client thành lưới (nút "Xếp cửa sổ"): từ trái sang phải rồi xuống hàng; hết chỗ thì xếp lớp mới lệch 24 điểm ảnh
/// thay vì rơi ra ngoài vùng. Vùng nhỏ hơn một ô thì mỗi lớp chứa một ô.
/// </summary>
public static class WindowGrid
{
    private const int CascadeOffset = 24;

    public static IReadOnlyList<WindowRect> Layout(int count, int areaWidth, int areaHeight, int cellWidth, int cellHeight, int gap = 0)
    {
        var result = new List<WindowRect>(Math.Max(count, 0));
        if (count <= 0) return result;
        int cols = Math.Max(1, (areaWidth + gap) / (cellWidth + gap));
        int rows = Math.Max(1, (areaHeight + gap) / (cellHeight + gap));
        int perLayer = cols * rows;
        for (int i = 0; i < count; i++)
        {
            int layer = i / perLayer, j = i % perLayer;
            int x = layer * CascadeOffset + j % cols * (cellWidth + gap);
            int y = layer * CascadeOffset + j / cols * (cellHeight + gap);
            result.Add(new WindowRect(x, y, cellWidth, cellHeight));
        }
        return result;
    }
}
