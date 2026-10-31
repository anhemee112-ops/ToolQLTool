namespace ToolQL.Core.Tests;

/// <summary>Khung ảnh dựng tay cho test (vùng client của cửa sổ client, 240×320 hoặc phóng to).</summary>
internal sealed class TestFrame : IPixelFrame
{
    private readonly (byte R, byte G, byte B)[,] _px;

    public TestFrame(int width, int height, (byte, byte, byte) background)
    {
        Width = width;
        Height = height;
        _px = new (byte, byte, byte)[width, height];
        Fill(0, 0, width, height, background);
    }

    public int Width { get; }
    public int Height { get; }

    public void Fill(int x, int y, int w, int h, (byte, byte, byte) c)
    {
        for (int i = x; i < x + w && i < Width; i++)
            for (int j = y; j < y + h && j < Height; j++) _px[i, j] = c;
    }

    public void Read(int x, int y, out byte r, out byte g, out byte b) => (r, g, b) = _px[x, y];

    public static readonly (byte, byte, byte) Orange = (228, 127, 59), White = (255, 255, 255), Black = (0, 0, 0), Grass = (60, 140, 60);

    /// <summary>Popup "Đã có bản cập nhật mới... Có / Không" đo trên cửa sổ thật (vùng client 240×320), nhân với <paramref name="scale"/>.</summary>
    public static TestFrame WithPrompt(int scale = 1, bool yes = true, bool no = true, bool panel = true)
    {
        var f = new TestFrame(240 * scale, 320 * scale, Grass);
        if (panel) f.Fill(14 * scale, 252 * scale, 212 * scale, 68 * scale, White);
        f.Fill(30 * scale, 270 * scale, 150 * scale, 6 * scale, Black);     // dòng chữ đen trên nền trắng
        if (yes) f.Fill(46 * scale, 288 * scale, 66 * scale, 20 * scale, Orange);
        if (no) f.Fill(132 * scale, 288 * scale, 66 * scale, 20 * scale, Orange);
        return f;
    }
}

public class UpdatePromptDetectorTests
{
    [Fact]
    public void Nhan_ra_popup_co_nen_trang_va_hai_nut_cam()
    {
        Assert.True(UpdatePromptDetector.IsPresent(TestFrame.WithPrompt()));
    }

    [Fact]
    public void Nhan_ra_ca_khi_cua_so_duoc_phong_to()
    {
        Assert.True(UpdatePromptDetector.IsPresent(TestFrame.WithPrompt(scale: 2)));
    }

    [Theory]
    [InlineData(false, true, true)]    // thiếu nút Có
    [InlineData(true, false, true)]    // thiếu nút Không
    [InlineData(true, true, false)]    // không có nền trắng của hộp thoại
    public void Thieu_mot_dau_hieu_thi_khong_phai_popup(bool yes, bool no, bool panel)
    {
        Assert.False(UpdatePromptDetector.IsPresent(TestFrame.WithPrompt(yes: yes, no: no, panel: panel)));
    }

    [Fact]
    public void Man_hinh_thuong_khong_bi_nhan_nham()
    {
        Assert.False(UpdatePromptDetector.IsPresent(new TestFrame(240, 320, TestFrame.Grass)));
        Assert.False(UpdatePromptDetector.IsPresent(new TestFrame(240, 320, TestFrame.White)));    // toàn trắng (màn hình tải)
        Assert.False(UpdatePromptDetector.IsPresent(new TestFrame(240, 320, TestFrame.Orange)));   // toàn cam (hoàng hôn)
        Assert.False(UpdatePromptDetector.IsPresent(new TestFrame(240, 320, TestFrame.Black)));    // màn hình đen khi nạp
    }

    [Fact]
    public void Menu_dang_nhap_co_nut_cam_o_cho_khac_khong_bi_nhan_nham()
    {
        var f = new TestFrame(240, 320, TestFrame.Grass);
        foreach (int y in new[] { 113, 144, 175, 206 }) f.Fill(48, y, 144, 24, TestFrame.Orange);   // bốn nút của menu (đo ~ y 125-218)
        Assert.False(UpdatePromptDetector.IsPresent(f));
    }

    [Fact]
    public void Khung_qua_nho_hoac_rong_khong_loi()
    {
        Assert.False(UpdatePromptDetector.IsPresent(new TestFrame(1, 1, TestFrame.White)));
    }

    [Fact]
    public void Diem_bam_Khong_nam_trong_nut_Khong_o_moi_ti_le()
    {
        foreach (int scale in new[] { 1, 2 })
        {
            int x = (int)(UpdatePromptDetector.NoButtonX * 240 * scale), y = (int)(UpdatePromptDetector.NoButtonY * 320 * scale);
            Assert.InRange(x, 132 * scale, 198 * scale);
            Assert.InRange(y, 288 * scale, 308 * scale);
        }
    }
}

public class PopupGuardTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset At(double s) => T0 + TimeSpan.FromSeconds(s);

    private static PopupGuard Make() => new(startAfter: TimeSpan.FromSeconds(10), watchFor: TimeSpan.FromMinutes(5));

    [Fact]
    public void Chi_canh_trong_khoang_sau_khi_mo_den_het_han()
    {
        var g = Make();
        var a = Guid.NewGuid();
        var running = new[] { (a, T0) };

        Assert.Empty(g.Watching(running, At(9)));
        Assert.Equal(new[] { a }, g.Watching(running, At(10)));
        Assert.Equal(new[] { a }, g.Watching(running, At(299)));
        Assert.Empty(g.Watching(running, At(301)));
    }

    [Fact]
    public void Phai_thay_popup_hai_lan_lien_tiep_moi_bam_lan_le_thi_khong()
    {
        var g = Make();
        var a = Guid.NewGuid();

        Assert.False(g.Observe(a, T0, At(20), popupPresent: true));    // mới thấy một lần
        Assert.False(g.Observe(a, T0, At(21), popupPresent: false));   // biến mất: đếm lại
        Assert.False(g.Observe(a, T0, At(22), popupPresent: true));
        Assert.True(g.Observe(a, T0, At(23), popupPresent: true));     // hai lần liên tiếp: bấm
    }

    [Fact]
    public void Sau_khi_bam_cho_nhip_roi_neu_popup_van_con_thi_thu_lai_toi_da_3_lan_roi_bo()
    {
        var g = Make();
        var a = Guid.NewGuid();
        var clicks = new List<double>();
        for (double s = 20; s < 200; s++)
            if (g.Observe(a, T0, At(s), popupPresent: true)) clicks.Add(s);

        Assert.Equal(3, clicks.Count);
        Assert.All(clicks.Zip(clicks.Skip(1)), p => Assert.True(p.Second - p.First >= 5));
    }

    [Fact]
    public void Popup_bien_mat_sau_khi_bam_thi_xong_va_lan_chay_moi_duoc_canh_lai()
    {
        var g = Make();
        var a = Guid.NewGuid();
        g.Observe(a, T0, At(20), true);
        Assert.True(g.Observe(a, T0, At(21), true));
        Assert.False(g.Observe(a, T0, At(30), false));
        Assert.Empty(g.Watching(new[] { (a, T0) }, At(40)));                 // đã xong lần chạy này

        var restarted = At(100);                                              // mở lại sau khi mất kết nối: StartedAt mới
        Assert.Equal(new[] { a }, g.Watching(new[] { (a, restarted) }, restarted + TimeSpan.FromSeconds(10)));
    }
}

public class LowGraphicsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "toolql-gfx-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string Template()
    {
        string template = Path.Combine(_root, "mau");
        Directory.CreateDirectory(ClientDirectory.SuiteDir(template));
        return template;
    }

    private static byte LastByte(string suite, string key) => File.ReadAllBytes(Path.Combine(suite, $"vj{key}.rs")).Last();

    [Fact]
    public void Mac_dinh_ghi_san_do_hoa_thap_nhat_theo_dung_khoa_cua_menu_Cau_hinh_cua_client()
    {
        string acc = Path.Combine(_root, "acc1");

        ClientDirectory.Prepare(Template(), acc, "u", "p", 0, DateTimeOffset.UtcNow);

        string suite = ClientDirectory.SuiteDir(acc);
        Assert.Equal(1, LastByte(suite, "lowGraphic"));     // 1 = đồ họa thấp (cs_0: aJ = lowGraphic == 1)
        Assert.Equal(0, LastByte(suite, "isPaintAura"));    // 0 = không vẽ hào quang
        Assert.Equal(0, LastByte(suite, "isPaintAura2"));
    }

    [Fact]
    public void Tat_do_hoa_thap_thi_khong_dung_vao_cai_dat_do_hoa_cua_client()
    {
        string acc = Path.Combine(_root, "acc1");

        ClientDirectory.Prepare(Template(), acc, "u", "p", 0, DateTimeOffset.UtcNow, lowGraphics: false);

        Assert.False(File.Exists(Path.Combine(ClientDirectory.SuiteDir(acc), "vjlowGraphic.rs")));
    }

    [Fact]
    public void Lan_sau_van_ghi_de_ve_do_hoa_thap_du_nguoi_dung_da_doi_trong_client()
    {
        string template = Template();
        string acc = Path.Combine(_root, "acc1");
        ClientDirectory.Prepare(template, acc, "u", "p", 0, DateTimeOffset.UtcNow);
        RmsFile.Write(ClientDirectory.SuiteDir(acc), "lowGraphic", new byte[] { 0 }, DateTimeOffset.UtcNow);   // client đổi lại đồ họa cao

        ClientDirectory.Prepare(template, acc, "u", "p", 0, DateTimeOffset.UtcNow);

        Assert.Equal(1, LastByte(ClientDirectory.SuiteDir(acc), "lowGraphic"));
    }
}
