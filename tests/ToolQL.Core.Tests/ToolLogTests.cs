namespace ToolQL.Core.Tests;

public class ToolLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "toolql-log-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Ghi_moi_su_kien_mot_dong_co_gio_va_tao_thu_muc()
    {
        string path = Path.Combine(_dir, "thu-muc-con", "toolql.log");
        var log = new ToolLog(path, TimeProvider.System);

        log.Write("mở acc ab***");
        log.Write("bấm Không ở popup");

        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.Matches(@"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d  mở acc ab\*\*\*$", lines[0]);
        Assert.EndsWith("bấm Không ở popup", lines[1]);
    }

    [Fact]
    public void Qua_gioi_han_kich_thuoc_thi_chuyen_sang_tep_cu_va_chi_giu_mot_tep_cu()
    {
        string path = Path.Combine(_dir, "toolql.log");
        var log = new ToolLog(path, TimeProvider.System, maxBytes: 200);

        for (int i = 0; i < 40; i++) log.Write("dòng số " + i);

        Assert.True(File.Exists(path + ".1"));
        Assert.True(new FileInfo(path).Length <= 400);                       // tệp hiện tại không phình mãi
        Assert.False(File.Exists(path + ".2"));
        Assert.Contains("dòng số 39", File.ReadAllText(path));               // sự kiện mới nhất vẫn nằm trong tệp hiện tại
    }

    [Fact]
    public void Khong_ghi_duoc_thi_nuot_loi_khong_lam_hong_chuong_trinh()
    {
        Directory.CreateDirectory(_dir);
        string path = Path.Combine(_dir, "toolql.log");
        using var lockFile = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);   // giữ khóa độc quyền

        var ex = Record.Exception(() => new ToolLog(path, TimeProvider.System).Write("sự kiện"));

        Assert.Null(ex);
    }
}
