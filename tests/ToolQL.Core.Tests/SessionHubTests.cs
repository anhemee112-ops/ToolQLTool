using ToolQL.Protocol;

namespace ToolQL.Core.Tests;

public class SessionHubTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "toolql-hub-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    /// <summary>Mã hóa giả (đảo chuỗi) để test không phụ thuộc DPAPI.</summary>
    private sealed class ReversingProtector : IPasswordProtector
    {
        public string Protect(string plain) => new(plain.Reverse().ToArray());
        public string Unprotect(string protectedText) => new(protectedText.Reverse().ToArray());
    }

    private sealed class FakeClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private (SessionHub Hub, AccountStore Store, Guid Id, FakeClock Clock) Make(FakeServer server, string serverName = "Vũ trụ 1")
    {
        var store = new AccountStore(Path.Combine(_dir, "accounts.json"), new ReversingProtector());
        var entry = store.Add("nguoi-thu", "mat-khau-thu", serverName);
        var clock = new FakeClock();
        var hub = new SessionHub(store, _ => server.Connector(), time: clock);
        return (hub, store, entry.Id, clock);
    }

    private static bool WaitFor(Func<bool> condition, int ms = 5000) => SpinWait.SpinUntil(condition, ms);

    [Fact]
    public void Bat_dau_thi_vao_game_va_bao_trang_thai_cho_nguoi_nghe()
    {
        using var server = new FakeServer((conn, _) => { conn.RunHappyPath(); conn.Drain(); });
        var (hub, _, id, _) = Make(server);
        using var _h = hub;
        var seen = new List<SessionState>();
        hub.Changed += (who, state, _) => { lock (seen) if (who == id) seen.Add(state); };

        Assert.Equal(SessionState.Idle, hub.StateOf(id));
        var outcome = hub.Start(id);

        Assert.Equal(StartResult.Started, outcome.Result);
        Assert.True(WaitFor(() => hub.StateOf(id) == SessionState.InGame), "không vào game: " + hub.StateOf(id));
        Assert.NotNull(hub.GameOf(id)!.Snapshot().Map);
        lock (seen) Assert.Contains(SessionState.InGame, seen);
        Assert.Empty(server.Errors);
    }

    [Fact]
    public void Bat_dau_hai_lan_thi_lan_hai_la_dang_chay_va_chi_noi_mot_ket_noi()
    {
        using var server = new FakeServer((conn, _) => { conn.RunHappyPath(); conn.Drain(); });
        var (hub, _, id, _) = Make(server);
        using var _h = hub;

        hub.Start(id);
        Assert.True(WaitFor(() => hub.StateOf(id) == SessionState.InGame));
        var second = hub.Start(id);

        Assert.Equal(StartResult.AlreadyRunning, second.Result);
        Assert.Equal(1, server.Accepted);
    }

    [Fact]
    public void Dung_roi_bat_dau_lai_ngay_bi_chan_doi_du_40_giay()
    {
        using var server = new FakeServer((conn, _) => { conn.RunHappyPath(); conn.Drain(); });
        var (hub, _, id, clock) = Make(server);
        using var _h = hub;

        hub.Start(id);
        Assert.True(WaitFor(() => hub.StateOf(id) == SessionState.InGame));
        hub.Stop(id);
        Assert.Equal(SessionState.Stopped, hub.StateOf(id));

        clock.Advance(TimeSpan.FromSeconds(10));
        var early = hub.Start(id);
        Assert.Equal(StartResult.TooSoon, early.Result);
        Assert.Equal(TimeSpan.FromSeconds(30), early.Wait);
        Assert.Equal(1, server.Accepted);   // không nối thêm

        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(StartResult.Started, hub.Start(id).Result);
        Assert.True(WaitFor(() => server.Accepted == 2));
    }

    [Fact]
    public void Bi_tu_choi_thi_cung_phai_cho_40_giay_moi_bat_dau_lai()
    {
        using var server = new FakeServer((conn, _) =>
        {
            conn.Handshake();
            conn.ExpectLogin();
            conn.Send(-85, new byte[] { 0 });   // captcha
            conn.Drain();
        });
        var (hub, _, id, clock) = Make(server);
        using var _h = hub;

        hub.Start(id);
        Assert.True(WaitFor(() => hub.StateOf(id) == SessionState.NeedCaptcha));

        Assert.Equal(StartResult.TooSoon, hub.Start(id).Result);
        clock.Advance(Backoff.MinReconnectDelay);
        Assert.Equal(StartResult.Started, hub.Start(id).Result);
    }

    [Fact]
    public void Server_khong_co_trong_danh_sach_la_loi_ro_rang_va_khong_noi()
    {
        using var server = new FakeServer((conn, _) => conn.Drain());
        var (hub, _, id, _) = Make(server, serverName: "Server không tồn tại");
        using var _h = hub;

        var ex = Assert.Throws<InvalidOperationException>(() => hub.Start(id));
        Assert.Contains("Server không tồn tại", ex.Message);
        Assert.Equal(0, server.Accepted);
        Assert.Equal(SessionState.Idle, hub.StateOf(id));
    }

    [Fact]
    public void Khong_giai_duoc_mat_khau_thi_bao_nhap_lai_va_khong_noi()
    {
        using var server = new FakeServer((conn, _) => conn.Drain());
        var store = new AccountStore(Path.Combine(_dir, "accounts.json"), new ThrowingProtector());
        // ghi acc bằng protector chạy được, rồi đọc bằng protector hỏng
        var writer = new AccountStore(Path.Combine(_dir, "accounts.json"), new ReversingProtector());
        var entry = writer.Add("nguoi-thu", "mat-khau-thu", "Vũ trụ 1");
        store.Load();
        using var hub = new SessionHub(store, _ => server.Connector());

        Assert.Throws<InvalidDataException>(() => hub.Start(entry.Id));
        Assert.Equal(0, server.Accepted);
    }

    private sealed class ThrowingProtector : IPasswordProtector
    {
        public string Protect(string plain) => plain;
        public string Unprotect(string protectedText) => throw new System.Security.Cryptography.CryptographicException("hỏng");
    }

    [Fact]
    public void Demo_giu_trang_thai_va_khong_noi_mang()
    {
        using var server = new FakeServer((conn, _) => conn.Drain());
        var (hub, _, _, _) = Make(server);
        using var _h = hub;
        var demoId = Guid.NewGuid();
        var game = new GameState();
        string? lastMessage = null;
        hub.Changed += (who, _, message) => { if (who == demoId) lastMessage = message; };

        hub.RegisterDemo(demoId, game);

        Assert.Equal(SessionState.InGame, hub.StateOf(demoId));
        Assert.Same(game, hub.GameOf(demoId));
        Assert.Equal(StartResult.AlreadyRunning, hub.Start(demoId).Result);
        Assert.Equal(0, server.Accepted);
        Assert.NotNull(lastMessage);
    }

    [Fact]
    public void Dispose_dung_het_cac_phien()
    {
        using var server = new FakeServer((conn, _) => { conn.RunHappyPath(); conn.Drain(); });
        var (hub, _, id, _) = Make(server);

        hub.Start(id);
        Assert.True(WaitFor(() => hub.StateOf(id) == SessionState.InGame));
        hub.Dispose();

        Assert.Equal(SessionState.Stopped, hub.StateOf(id));
    }
}
