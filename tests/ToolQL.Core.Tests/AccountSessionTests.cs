using ToolQL.Protocol;

namespace ToolQL.Core.Tests;

public class AccountSessionTests
{
    private static readonly AccountCredentials Creds = new("nguoi-thu", "mat-khau-thu");

    /// <summary>Ghi lại chuỗi trạng thái và cho phép chờ tới khi gặp một trạng thái.</summary>
    private sealed class StateLog
    {
        private readonly object _gate = new();
        private readonly List<SessionState> _states = new();

        public void Add(SessionState state, string? _)
        {
            lock (_gate) _states.Add(state);
        }

        public SessionState[] Snapshot()
        {
            lock (_gate) return _states.ToArray();
        }

        public bool Wait(SessionState state, int timeoutMs = 5000) =>
            SpinWait.SpinUntil(() => Snapshot().Contains(state), timeoutMs);
    }

    private static (AccountSession Session, StateLog Log, List<TimeSpan> Delays) Make(FakeServer server)
    {
        var delays = new List<TimeSpan>();
        var options = new SessionOptions
        {
            Wait = (delay, cancel) =>
            {
                lock (delays) delays.Add(delay);
                return !cancel.IsCancellationRequested;   // test không chờ thật
            },
        };
        var session = new AccountSession(Creds, server.Connector(), options);
        var log = new StateLog();
        session.StateChanged += log.Add;
        return (session, log, delays);
    }

    [Fact]
    public void Dang_nhap_thanh_cong_di_qua_cac_trang_thai_theo_thu_tu()
    {
        using var server = new FakeServer((conn, _) =>
        {
            conn.RunHappyPath();
            conn.Drain();   // giữ kết nối tới khi client đóng
        });
        var (session, log, _) = Make(server);
        var received = new List<sbyte>();
        session.PacketReceived += p => { lock (received) received.Add(p.Command); };

        session.Start();
        Assert.True(log.Wait(SessionState.InGame), "không tới InGame: " + string.Join(",", log.Snapshot()));
        session.Stop();

        Assert.Equal(new[] { SessionState.Connecting, SessionState.LoggingIn, SessionState.Loading, SessionState.InGame, SessionState.Stopped },
            log.Snapshot());
        Assert.Empty(server.Errors);
        lock (received) Assert.Equal(new sbyte[] { -28, 12, -24, -28 }, received);
        Assert.Equal(1, server.Accepted);
    }

    [Fact]
    public void Khi_bao_InGame_trang_thai_game_da_co_map_va_luoi()
    {
        using var server = new FakeServer((conn, _) =>
        {
            conn.RunHappyPath();
            conn.Drain();
        });
        var (session, log, _) = Make(server);

        session.Start();
        Assert.True(log.Wait(SessionState.InGame));
        var snapshot = session.Game.Snapshot();
        session.Stop();

        Assert.NotNull(snapshot.Map);
        Assert.Equal((2, 2), (snapshot.Map!.Width, snapshot.Map.Height));
        Assert.Equal(0, snapshot.ParseErrors);
    }

    [Fact]
    public void Co_Cache_thi_bang_nhan_duoc_duoc_luu_theo_phien_ban()
    {
        string dir = Path.Combine(Path.GetTempPath(), "toolql-cache-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var server = new FakeServer((conn, _) =>
            {
                conn.RunHappyPath();
                conn.Drain();
            });
            var options = new SessionOptions { Cache = new DataCache(dir), Wait = (_, c) => !c.IsCancellationRequested };
            using var session = new AccountSession(Creds, server.Connector(), options);
            var log = new StateLog();
            session.StateChanged += log.Add;

            session.Start();
            Assert.True(log.Wait(SessionState.InGame));
            session.Stop();

            // RunHappyPath gửi gói 12 (3 byte) với phiên bản item = 24.
            Assert.NotNull(options.Cache.TryLoad("itemlist-0", 24));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Server_gui_captcha_thi_dung_khong_noi_lai()
    {
        using var server = new FakeServer((conn, _) =>
        {
            conn.Handshake();
            conn.ExpectLogin();
            conn.Send(-85, new byte[] { 0 });
            conn.Drain();
        });
        var (session, log, delays) = Make(server);

        session.Start();
        Assert.True(log.Wait(SessionState.NeedCaptcha));
        Thread.Sleep(300);
        session.Stop();

        Assert.Equal(SessionState.NeedCaptcha, session.State);
        Assert.Equal(1, server.Accepted);
        Assert.Empty(delays);
    }

    [Fact]
    public void Server_ngat_khi_dang_nhap_la_bi_tu_choi_va_khong_thu_lai()
    {
        using var server = new FakeServer((conn, _) =>
        {
            conn.Handshake();
            conn.ExpectLogin();
            // đóng kết nối ngay
        });
        var (session, log, delays) = Make(server);

        session.Start();
        Assert.True(log.Wait(SessionState.Rejected));
        Thread.Sleep(300);
        session.Stop();

        Assert.Equal(SessionState.Rejected, session.State);
        Assert.Equal(1, server.Accepted);
        Assert.Empty(delays);
    }

    [Fact]
    public void Thong_bao_26_la_bi_tu_choi_kem_noi_dung()
    {
        using var server = new FakeServer((conn, _) =>
        {
            conn.Handshake();
            conn.ExpectLogin();
            var w = new JavaStreamWriter();
            w.WriteUtf("Tài khoản bị khóa");
            conn.Send(-26, w.ToArray());
            conn.Drain();
        });
        var (session, log, _) = Make(server);

        session.Start();
        Assert.True(log.Wait(SessionState.Rejected));
        session.Stop();

        Assert.Contains("Tài khoản bị khóa", session.Message);
        Assert.Equal(1, server.Accepted);
    }

    [Fact]
    public void Mat_mang_khi_dang_trong_game_thi_noi_lai_voi_thoi_gian_cho_tang_dan()
    {
        using var server = new FakeServer((conn, index) =>
        {
            if (index == 0)
            {
                conn.RunHappyPath();
                return;   // đóng kết nối: client thấy mất mạng
            }
            conn.Handshake();
            conn.ExpectLogin();
            conn.Drain();
        });
        var (session, log, delays) = Make(server);

        session.Start();
        Assert.True(SpinWait.SpinUntil(() => server.Accepted >= 2 && log.Snapshot().Count(s => s == SessionState.LoggingIn) >= 2, 5000),
            "không nối lại: " + string.Join(",", log.Snapshot()));
        session.Stop();

        Assert.Contains(SessionState.Reconnecting, log.Snapshot());
        Assert.Empty(server.Errors);
        lock (delays) Assert.Equal(new[] { TimeSpan.FromSeconds(40) }, delays.Take(1).ToArray());   // vào game đã Reset, nên lần đầu là 40 giây (server khóa nếu đăng nhập lại sớm hơn)
    }

    [Fact]
    public void Khong_noi_duoc_thi_cho_tang_dan_roi_dung_khi_Stop()
    {
        var delays = new List<TimeSpan>();
        var options = new SessionOptions
        {
            Wait = (delay, cancel) =>
            {
                lock (delays) delays.Add(delay);
                return !cancel.IsCancellationRequested;
            },
        };
        // Bộ nối giả luôn bị từ chối ngay (cổng đóng thật trên Windows mất cỡ giây mỗi lần).
        Func<CancellationToken, Stream> refuse = _ => throw new System.Net.Sockets.SocketException(10061);
        var session = new AccountSession(Creds, refuse, options);
        var log = new StateLog();
        session.StateChanged += log.Add;

        session.Start();
        Assert.True(SpinWait.SpinUntil(() => { lock (delays) return delays.Count >= 3; }, 5000));
        session.Stop();

        lock (delays) Assert.Equal(new[] { 40.0, 80.0, 160.0 }, delays.Take(3).Select(d => d.TotalSeconds).ToArray());
        Assert.Equal(SessionState.Stopped, session.State);
    }

    [Fact]
    public void Stop_mo_khoa_luong_doc_dang_cho_goi()
    {
        using var server = new FakeServer((conn, _) =>
        {
            conn.Handshake();
            conn.ExpectLogin();
            conn.Drain();   // im lặng, client đang chờ
        });
        var (session, log, _) = Make(server);

        session.Start();
        Assert.True(log.Wait(SessionState.LoggingIn));
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        session.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), "Stop treo " + stopwatch.Elapsed);
        Assert.Equal(SessionState.Stopped, session.State);
    }

    [Fact]
    public void Server_im_lang_sau_dang_nhap_qua_han_thi_bi_tu_choi_khong_ket_va_khong_thu_lai()
    {
        using var server = new FakeServer((conn, _) =>
        {
            conn.Handshake();
            conn.ExpectLogin();
            conn.Drain();   // nhận đăng nhập rồi im lặng, không gửi -28 sub 4
        });
        var delays = new List<TimeSpan>();
        var options = new SessionOptions
        {
            LoginTimeout = TimeSpan.FromMilliseconds(300),
            Wait = (delay, cancel) =>
            {
                lock (delays) delays.Add(delay);
                return !cancel.IsCancellationRequested;
            },
        };
        using var session = new AccountSession(Creds, server.Connector(), options);
        var log = new StateLog();
        session.StateChanged += log.Add;

        session.Start();
        Assert.True(log.Wait(SessionState.Rejected), "kẹt: " + string.Join(",", log.Snapshot()));
        Thread.Sleep(300);

        Assert.Equal(SessionState.Rejected, session.State);
        Assert.Contains("không trả lời", session.Message);
        Assert.Equal(1, server.Accepted);
        lock (delays) Assert.Empty(delays);
    }

    [Fact]
    public void Vao_game_dung_han_thi_dong_ho_dang_nhap_khong_cat_phien()
    {
        using var server = new FakeServer((conn, _) =>
        {
            conn.RunHappyPath();
            conn.Drain();
        });
        var options = new SessionOptions
        {
            LoginTimeout = TimeSpan.FromMilliseconds(300),
            Wait = (_, c) => !c.IsCancellationRequested,
        };
        using var session = new AccountSession(Creds, server.Connector(), options);
        var log = new StateLog();
        session.StateChanged += log.Add;

        session.Start();
        Assert.True(log.Wait(SessionState.InGame));
        Thread.Sleep(600);   // quá hạn đăng nhập mà vẫn phải ở trong game

        Assert.Equal(SessionState.InGame, session.State);
    }

    [Fact]
    public void Goi_hong_trong_map_la_Faulted_khong_noi_lai()
    {
        using var server = new FakeServer((conn, _) =>
        {
            conn.Handshake();
            conn.ExpectLogin();
            conn.Send(-28, Packets.Versions());
            conn.Send(-24, new byte[] { 1, 2 });   // -24 cụt: bộ đọc không đọc được
            conn.Drain();
        });
        var (session, log, delays) = Make(server);

        session.Start();
        Assert.True(log.Wait(SessionState.Faulted));
        session.Stop();

        Assert.Equal(1, server.Accepted);
        Assert.Empty(delays);
    }

    [Fact]
    public void Luong_phien_khong_nam_tren_thread_pool()
    {
        bool? onPool = null;
        using var server = new FakeServer((conn, _) =>
        {
            conn.RunHappyPath();
            conn.Drain();
        });
        var (session, log, _) = Make(server);
        session.PacketReceived += _ => onPool ??= Thread.CurrentThread.IsThreadPoolThread;

        session.Start();
        Assert.True(log.Wait(SessionState.InGame));
        session.Stop();

        Assert.False(onPool);
    }
}
