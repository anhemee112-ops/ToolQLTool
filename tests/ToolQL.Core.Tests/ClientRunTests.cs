namespace ToolQL.Core.Tests;

public class ClientCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "toolql-cmd-" + Guid.NewGuid().ToString("N"));

    public ClientCommandTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private AppSettings Settings(string java) => new()
    {
        JavaPath = java,
        EmulatorJarPath = @"C:\Tool Dir\AngelAChip.jar",
        GameJarPath = @"C:\Game Dir\Dragonboy250 v3.5.jar",
        ClientWidth = 240,
        ClientHeight = 320,
    };

    [Fact]
    public void Dung_lenh_java_co_user_home_rieng_va_dat_duong_dan_co_dau_cach_trong_ngoac_kep()
    {
        string javaw = Path.Combine(_root, "javaw.exe");
        File.WriteAllText(javaw, "");

        var spec = ClientCommand.Build(Settings(javaw), @"C:\Data\acc 1", "acc1");

        Assert.Equal(javaw, spec.FileName);
        Assert.Equal(@"C:\Data\acc 1", spec.WorkingDirectory);
        Assert.Equal(
            "-Duser.home=\"C:\\Data\\acc 1\\home\" -XX:+IgnoreUnrecognizedVMOptions -Xmx64m -Xms8m -XX:+UseSerialGC -XX:TieredStopAtLevel=1 " +
            "-XX:CICompilerCount=1 -Xss256k -XX:ReservedCodeCacheSize=16m -XX:MaxMetaspaceSize=64m -XX:MinHeapFreeRatio=10 -XX:MaxHeapFreeRatio=20 " +
            "-XX:-UsePerfData -Dsun.java2d.d3d=false -Dsun.java2d.noddraw=true -XX:+UseCompactObjectHeaders " +
            "-jar \"C:\\Tool Dir\\AngelAChip.jar\" --rms file --id acc1 --resizableDevice 240 320 " +
            "--appclasspath \"C:\\Game Dir\\Dragonboy250 v3.5.jar\" nro.hero69",
            spec.Arguments);
    }

    [Fact]
    public void Co_java_exe_ma_co_javaw_exe_canh_do_thi_dung_javaw_de_khong_hien_cua_so_console()
    {
        string java = Path.Combine(_root, "java.exe");
        string javaw = Path.Combine(_root, "javaw.exe");
        File.WriteAllText(java, "");
        File.WriteAllText(javaw, "");

        Assert.Equal(javaw, ClientCommand.Build(Settings(java), @"C:\Data\a", "a").FileName);
    }

    [Fact]
    public void Check_bao_loi_tieng_viet_chi_ro_thu_thieu()
    {
        string javaw = Path.Combine(_root, "javaw.exe");
        File.WriteAllText(javaw, "");
        var ok = Settings(javaw);
        File.WriteAllText(ok.EmulatorJarPath = Path.Combine(_root, "emu.jar"), "");
        File.WriteAllText(ok.GameJarPath = Path.Combine(_root, "game.jar"), "");
        Assert.Null(ClientCommand.Check(ok));   // đủ thì không có lỗi

        Assert.Contains("Java", ClientCommand.Check(Settings(Path.Combine(_root, "khong-co.exe")))!);
        Assert.Contains("Java", ClientCommand.Check(Settings(""))!);

        ok.EmulatorJarPath = Path.Combine(_root, "khong-co-emu.jar");
        Assert.Contains("trình chạy", ClientCommand.Check(ok)!);

        ok.EmulatorJarPath = Path.Combine(_root, "emu.jar");
        ok.GameJarPath = Path.Combine(_root, "khong-co-game.jar");
        Assert.Contains("jar game", ClientCommand.Check(ok)!);
    }

    [Fact]
    public void Duong_dan_co_dau_ngoac_kep_bi_tu_choi_va_ma_dinh_danh_chi_cho_chu_so_gach()
    {
        string javaw = Path.Combine(_root, "javaw.exe");
        File.WriteAllText(javaw, "");
        var s = Settings(javaw);
        File.WriteAllText(s.EmulatorJarPath = Path.Combine(_root, "emu.jar"), "");
        File.WriteAllText(s.GameJarPath = Path.Combine(_root, "game.jar"), "");

        Assert.Throws<ArgumentException>(() => ClientCommand.Build(s, @"C:\Data\a""b", "a"));
        Assert.Throws<ArgumentException>(() => ClientCommand.Build(s, @"C:\Data\a", "a b"));
        Assert.Throws<ArgumentException>(() => ClientCommand.Build(s, @"C:\Data\a", "a;rm"));
    }
}

public class WindowGridTests
{
    [Fact]
    public void Xep_thanh_luoi_tu_trai_sang_phai_roi_xuong_hang_moi()
    {
        // Vùng 1000×700, ô 256×404, khe 8: mỗi hàng chứa 3 ô (3·256 + 2·8 = 784 <= 1000), chỉ 1 hàng (404 < 700 < 808).
        var cells = WindowGrid.Layout(5, 1000, 700, 256, 404, gap: 8);

        Assert.Equal(5, cells.Count);
        Assert.Equal(new WindowRect(0, 0, 256, 404), cells[0]);
        Assert.Equal(new WindowRect(264, 0, 256, 404), cells[1]);
        Assert.Equal(new WindowRect(528, 0, 256, 404), cells[2]);
        // Hàng thứ hai không đủ chỗ (404 + 8 + 404 > 700): xếp chồng lệch sang phải một chút thay vì rơi ra ngoài màn hình.
        Assert.All(cells, c => Assert.True(c.Y + c.Height <= 700 + 404 && c.X >= 0));
        Assert.Equal(cells.Count, cells.Distinct().Count());   // không ô nào trùng hẳn ô khác
    }

    [Fact]
    public void Nhieu_hang_khi_vung_cao_du()
    {
        var cells = WindowGrid.Layout(4, 600, 900, 256, 404, gap: 0);   // 2 cột, 2 hàng
        Assert.Equal(new[] { (0, 0), (256, 0), (0, 404), (256, 404) }, cells.Select(c => (c.X, c.Y)).ToArray());
    }

    [Fact]
    public void Khong_co_o_nao_hoac_vung_nho_hon_mot_o_van_khong_loi()
    {
        Assert.Empty(WindowGrid.Layout(0, 1000, 700, 256, 404));
        var tiny = WindowGrid.Layout(2, 100, 100, 256, 404);
        Assert.Equal(2, tiny.Count);
        Assert.All(tiny, c => Assert.True(c.X >= 0 && c.Y >= 0));
    }
}
