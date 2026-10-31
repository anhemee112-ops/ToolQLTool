namespace ToolQL.Core.Tests;

public class AppSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "toolql-settings-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Chua_co_file_thi_dung_mac_dinh()
    {
        var s = AppSettings.Load(Path.Combine(_dir, "settings.json"));
        Assert.Equal("", s.ClientJarPath);
    }

    [Fact]
    public void Luu_roi_doc_lai_giu_duong_dan_jar_va_tao_thu_muc()
    {
        string path = Path.Combine(_dir, "thu-muc-con", "settings.json");
        new AppSettings { ClientJarPath = @"C:\Dev\ToolQL-input\client.jar" }.Save(path);

        Assert.Equal(@"C:\Dev\ToolQL-input\client.jar", AppSettings.Load(path).ClientJarPath);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void File_hong_thi_nem_loi_va_khong_ghi_de()
    {
        Directory.CreateDirectory(_dir);
        string path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, "{ không phải json");

        Assert.Throws<InvalidDataException>(() => AppSettings.Load(path));
        Assert.Equal("{ không phải json", File.ReadAllText(path));
    }
}

public class SessionStateTextTests
{
    [Fact]
    public void Moi_trang_thai_deu_co_nhan_tieng_viet_khong_trung_nhau()
    {
        var labels = Enum.GetValues<SessionState>().Select(SessionStateText.For).ToArray();

        Assert.All(labels, l => Assert.False(string.IsNullOrWhiteSpace(l)));
        Assert.Equal(labels.Length, labels.Distinct().Count());
        Assert.Equal("Trong game", SessionStateText.For(SessionState.InGame));
        Assert.Equal("Cần captcha", SessionStateText.For(SessionState.NeedCaptcha));
    }

    [Theory]
    [InlineData(SessionState.Connecting, true)]
    [InlineData(SessionState.LoggingIn, true)]
    [InlineData(SessionState.Loading, true)]
    [InlineData(SessionState.InGame, true)]
    [InlineData(SessionState.Reconnecting, true)]
    [InlineData(SessionState.Idle, false)]
    [InlineData(SessionState.Stopped, false)]
    [InlineData(SessionState.Rejected, false)]
    [InlineData(SessionState.NeedCaptcha, false)]
    [InlineData(SessionState.NeedCharacter, false)]
    [InlineData(SessionState.Faulted, false)]
    public void Dang_chay_chi_khi_dang_ket_noi_hoac_trong_game(SessionState state, bool running)
    {
        Assert.Equal(running, SessionStateText.IsRunning(state));
    }
}
