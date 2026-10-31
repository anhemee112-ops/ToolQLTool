using System.Text;

namespace ToolQL.Core.Tests;

/// <summary>
/// Định dạng RMS của MicroEmulator (AngelChip) đối chiếu từng byte với tệp thật do chính client ghi (2026-10-31):
/// UTF tên kho, int phiên bản, long thời điểm sửa, int mã bản ghi kế tiếp, rồi mỗi bản ghi: int mã, int độ dài, byte.
/// </summary>
public class RmsFileTests
{
    private static string Hex(byte[] b) => Convert.ToHexString(b);

    [Fact]
    public void Dung_tung_byte_voi_tep_vjResVersion_that()
    {
        // vjResVersion.rs thật, 44 byte, bản ghi "740992".
        var built = RmsFile.Build("vjResVersion", Encoding.UTF8.GetBytes("740992"), 0x01A192D815A1);
        Assert.Equal("000C766A52657356657273696F6E" + "00000001" + "000001A192D815A1" + "00000002" + "00000001" + "00000006" + "373430393932", Hex(built));
    }

    [Fact]
    public void Dung_tung_byte_voi_tep_vjsvselect_that()
    {
        // vjsvselect.rs thật, 37 byte, bản ghi 1 byte 0x00 (Vũ trụ 1).
        var built = RmsFile.Build("vjsvselect", new byte[] { 0 }, 0x01A192D7A03B);
        Assert.Equal("000A766A737673656C656374" + "00000001" + "000001A192D7A03B" + "00000002" + "00000001" + "00000001" + "00", Hex(built));
    }

    [Fact]
    public void Ghi_xuong_thu_muc_voi_ten_tep_vj_cong_khoa_va_giu_nguyen_tieng_viet()
    {
        string dir = Path.Combine(Path.GetTempPath(), "toolql-rms-" + Guid.NewGuid().ToString("N"));
        try
        {
            RmsFile.WriteString(dir, "acc", "tài-khoản-1", new DateTimeOffset(2026, 10, 31, 1, 2, 3, TimeSpan.Zero));

            string path = Path.Combine(dir, "vjacc.rs");
            Assert.True(File.Exists(path));
            Assert.False(File.Exists(path + ".tmp"));
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(new byte[] { 0x00, 0x05 }, bytes[..2]);                 // UTF "vjacc" dài 5
            Assert.Equal("vjacc", Encoding.UTF8.GetString(bytes, 2, 5));
            Assert.EndsWith(Hex(Encoding.UTF8.GetBytes("tài-khoản-1")), Hex(bytes)); // bản ghi là UTF-8
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Ghi_de_len_tep_cu_va_xoa_duoc()
    {
        string dir = Path.Combine(Path.GetTempPath(), "toolql-rms-" + Guid.NewGuid().ToString("N"));
        try
        {
            var t = DateTimeOffset.UtcNow;
            RmsFile.WriteString(dir, "pass", "cu", t);
            RmsFile.WriteString(dir, "pass", "moi-moi", t);
            string path = Path.Combine(dir, "vjpass.rs");
            Assert.Contains(Hex(Encoding.UTF8.GetBytes("moi-moi")), Hex(File.ReadAllBytes(path)));
            Assert.Equal(RmsFile.Build("vjpass", Encoding.UTF8.GetBytes("moi-moi"), t.ToUnixTimeMilliseconds()).Length, new FileInfo(path).Length);   // không còn dấu vết của bản ghi cũ

            RmsFile.Delete(dir, "pass");
            RmsFile.Delete(dir, "pass");   // xóa lần hai không lỗi
            Assert.False(File.Exists(Path.Combine(dir, "vjpass.rs")));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Ten_khoa_khong_hop_le_bi_tu_choi()
    {
        Assert.Throws<ArgumentException>(() => RmsFile.WriteString(Path.GetTempPath(), "..\\x", "v", DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => RmsFile.WriteString(Path.GetTempPath(), "", "v", DateTimeOffset.UtcNow));
    }
}

public class ClientDirectoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "toolql-cd-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string MakeTemplate()
    {
        string template = Path.Combine(_root, "mau");
        string suite = ClientDirectory.SuiteDir(template);
        Directory.CreateDirectory(suite);
        File.WriteAllText(Path.Combine(suite, "vjResVersion.rs"), "du lieu mau");
        File.WriteAllText(Path.Combine(template, "home", ".microemulator", "angelchip_config2.xml"), "<config/>");
        return template;
    }

    [Fact]
    public void Lan_dau_sao_chep_ban_mau_roi_ghi_tai_khoan_mat_khau_va_server()
    {
        string template = MakeTemplate();
        string acc = Path.Combine(_root, "acc1");

        ClientDirectory.Prepare(template, acc, "nguoi-thu", "mat-khau-thu", serverIndex: 2, DateTimeOffset.UtcNow);

        string suite = ClientDirectory.SuiteDir(acc);
        Assert.Equal("du lieu mau", File.ReadAllText(Path.Combine(suite, "vjResVersion.rs")));   // đã sao chép dữ liệu mẫu
        Assert.True(File.Exists(Path.Combine(acc, "home", ".microemulator", "angelchip_config2.xml")));
        Assert.Contains(Convert.ToHexString(Encoding.UTF8.GetBytes("nguoi-thu")), Convert.ToHexString(File.ReadAllBytes(Path.Combine(suite, "vjacc.rs"))));
        Assert.Contains(Convert.ToHexString(Encoding.UTF8.GetBytes("mat-khau-thu")), Convert.ToHexString(File.ReadAllBytes(Path.Combine(suite, "vjpass.rs"))));
        Assert.Equal(2, File.ReadAllBytes(Path.Combine(suite, "vjsvselect.rs")).Last());
    }

    [Fact]
    public void Lan_sau_khong_chep_de_len_du_lieu_da_co_nhung_cap_nhat_mat_khau()
    {
        string template = MakeTemplate();
        string acc = Path.Combine(_root, "acc1");
        ClientDirectory.Prepare(template, acc, "u", "mk-cu", 0, DateTimeOffset.UtcNow);
        string suite = ClientDirectory.SuiteDir(acc);
        File.WriteAllText(Path.Combine(suite, "vjResVersion.rs"), "da bi client sua");

        ClientDirectory.Prepare(template, acc, "u", "mk-moi", 0, DateTimeOffset.UtcNow);

        Assert.Equal("da bi client sua", File.ReadAllText(Path.Combine(suite, "vjResVersion.rs")));
        Assert.Contains(Convert.ToHexString(Encoding.UTF8.GetBytes("mk-moi")), Convert.ToHexString(File.ReadAllBytes(Path.Combine(suite, "vjpass.rs"))));
    }

    [Fact]
    public void Che_do_khong_dang_nhap_khong_ghi_tai_khoan_mat_khau_va_xoa_cai_cu()
    {
        string template = MakeTemplate();
        string acc = Path.Combine(_root, "acc1");
        ClientDirectory.Prepare(template, acc, "u", "p", 0, DateTimeOffset.UtcNow);
        string suite = ClientDirectory.SuiteDir(acc);
        Assert.True(File.Exists(Path.Combine(suite, "vjpass.rs")));

        ClientDirectory.Prepare(template, acc, "u", "p", 1, DateTimeOffset.UtcNow, writeCredentials: false);

        Assert.False(File.Exists(Path.Combine(suite, "vjacc.rs")));
        Assert.False(File.Exists(Path.Combine(suite, "vjpass.rs")));
        Assert.Equal(1, File.ReadAllBytes(Path.Combine(suite, "vjsvselect.rs")).Last());   // server vẫn được ghi
    }

    [Fact]
    public void Thieu_ban_mau_thi_bao_loi_ro_rang_va_khong_tao_thu_muc_acc()
    {
        string acc = Path.Combine(_root, "acc1");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ClientDirectory.Prepare(Path.Combine(_root, "khong-co"), acc, "u", "p", 0, DateTimeOffset.UtcNow));

        Assert.Contains("mẫu", ex.Message);
        Assert.False(Directory.Exists(acc));
    }

    [Fact]
    public void Xoa_thong_tin_dang_nhap_chi_xoa_acc_va_pass()
    {
        string template = MakeTemplate();
        string acc = Path.Combine(_root, "acc1");
        ClientDirectory.Prepare(template, acc, "u", "p", 0, DateTimeOffset.UtcNow);
        string suite = ClientDirectory.SuiteDir(acc);

        ClientDirectory.ClearCredentials(acc);

        Assert.False(File.Exists(Path.Combine(suite, "vjacc.rs")));
        Assert.False(File.Exists(Path.Combine(suite, "vjpass.rs")));
        Assert.True(File.Exists(Path.Combine(suite, "vjsvselect.rs")));
        Assert.True(File.Exists(Path.Combine(suite, "vjResVersion.rs")));
        ClientDirectory.ClearCredentials(Path.Combine(_root, "khong-co"));   // thư mục không có: không lỗi
    }
}
