namespace ToolQL.Core.Tests;

/// <summary>
/// Quy tắc của chủ repo (2026-10-31): đăng nhập lại phải chờ >= 40 giây SAU KHI MẤT KẾT NỐI (rớt mạng, game đá ra, client sập),
/// KHÔNG phải sau khi tự tay bấm Đóng rồi mở lại. Treo xuyên ngày đêm nên phải phát hiện mất kết nối kể cả khi cửa sổ client còn mở.
/// Game bảo trì hằng ngày (khoảng 4h sáng): tới giờ thì đóng hết, tới 5h mở lại.
/// </summary>
public class ClientResilienceTests
{
    private sealed class Clk : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(double seconds) => _now += TimeSpan.FromSeconds(seconds);
    }

    private sealed class Proc : IClientProcess
    {
        public int Id { get; init; }
        public bool HasExited { get; set; }
        public int ExitCode { get; set; }
        public bool Killed { get; private set; }
        public void Kill() { Killed = true; HasExited = true; ExitCode = -1; }
        public void Dispose() { }
    }

    private sealed class Launcher : IClientLauncher
    {
        private int _next = 500;
        public List<Proc> Procs { get; } = new();
        public IClientProcess Start(ClientLaunchSpec spec) { var p = new Proc { Id = _next++ }; Procs.Add(p); return p; }
    }

    private sealed class Probe : IConnectionProbe
    {
        public bool? Value { get; set; } = true;
        public bool? IsConnected(int processId) => Value;
    }

    private sealed class Rig
    {
        public required ClientHub Hub { get; init; }
        public required Launcher Launcher { get; init; }
        public required Clk Clock { get; init; }
        public required Probe Probe { get; init; }
        public List<Guid> Cleaned { get; } = new();

        /// <summary>Chạy Tick mỗi giây trong <paramref name="seconds"/> giây (như bộ đếm giờ của giao diện).</summary>
        public void Run(double seconds, IReadOnlyCollection<Guid> ticked, bool auto = true, bool maintenance = false)
        {
            for (double s = 0; s < seconds; s++)
            {
                Clock.Advance(1);
                Hub.Tick(ticked, auto, maintenance);
            }
        }
    }

    private static Rig Make(int delayMs = 0)
    {
        var launcher = new Launcher();
        var clock = new Clk();
        var probe = new Probe();
        Rig? rig = null;
        var hub = new ClientHub(launcher, id => new ClientLaunchSpec("javaw", "a-" + id, "d"), id => rig!.Cleaned.Add(id),
            TimeSpan.FromMilliseconds(delayMs), clock, probe);
        return rig = new Rig { Hub = hub, Launcher = launcher, Clock = clock, Probe = probe };
    }

    // ---------- đóng tay không phải chờ ----------

    [Fact]
    public void Dong_tay_roi_mo_lai_ngay_khong_phai_cho_40_giay()
    {
        var r = Make();
        var a = Guid.NewGuid();
        r.Hub.Request(a);
        r.Hub.Stop(a);

        Assert.Equal(ClientEnd.User, r.Hub.EndOf(a));
        Assert.Equal(StartResult.Started, r.Hub.Request(a).Result);
        Assert.Equal(2, r.Launcher.Procs.Count);
    }

    [Fact]
    public void Mo_loi_khong_chan_nguoi_dung_thu_lai_nhung_auto_login_van_cho_40_giay()
    {
        var launcher = new Launcher();
        var clock = new Clk();
        bool broken = true;
        var hub = new ClientHub(launcher, id => broken ? throw new InvalidOperationException("thiếu Java") : new ClientLaunchSpec("j", "a", "d"),
            _ => { }, TimeSpan.Zero, clock);
        var a = Guid.NewGuid();
        hub.Request(a);
        Assert.Equal(ClientEnd.Failed, hub.EndOf(a));

        broken = false;
        clock.Advance(5);
        Assert.Empty(hub.Tick(new[] { a }, autoLogin: true));          // tự động: chờ đủ 40 giây
        Assert.Equal(StartResult.Started, hub.Request(a).Result);      // người dùng bấm Login thì mở được ngay
    }

    // ---------- mất kết nối ----------

    [Fact]
    public void Mat_ket_noi_khi_cua_so_con_mo_thi_dong_client_cho_40_giay_roi_auto_login_mo_lai()
    {
        var r = Make();
        var a = Guid.NewGuid();
        var ticked = new[] { a };
        r.Run(1, ticked);                       // mở acc
        Assert.Equal(ClientState.Running, r.Hub.StateOf(a));

        r.Run(30, ticked);                      // kết nối ổn định > 20 giây
        Assert.Equal(true, r.Hub.ConnectedOf(a));
        Assert.True(r.Hub.StableConnectionOf(a));

        r.Probe.Value = false;                  // game đá ra / rớt mạng, cửa sổ client vẫn mở
        r.Run(14, ticked);
        Assert.Equal(ClientState.Running, r.Hub.StateOf(a));   // chưa đủ 15 giây
        r.Run(2, ticked);
        Assert.Equal(ClientState.Exited, r.Hub.StateOf(a));
        Assert.Equal(ClientEnd.Lost, r.Hub.EndOf(a));
        Assert.True(r.Launcher.Procs[0].Killed);
        Assert.Contains(a, r.Cleaned);

        r.Probe.Value = true;
        r.Run(38, ticked);
        Assert.Single(r.Launcher.Procs);        // chưa đủ 40 giây kể từ lúc phát hiện
        r.Run(3, ticked);
        Assert.Equal(2, r.Launcher.Procs.Count);
        Assert.Equal(ClientState.Running, r.Hub.StateOf(a));
    }

    [Fact]
    public void Chua_tung_vao_game_thi_rot_ket_noi_khong_bi_dong_va_khong_mo_lai_lien_tuc()
    {
        var r = Make();
        var a = Guid.NewGuid();
        r.Run(1, new[] { a });
        r.Probe.Value = true;
        r.Run(5, new[] { a });                  // chỉ nối thử 5 giây (lúc mới mở / sai mật khẩu)
        r.Probe.Value = false;
        r.Run(300, new[] { a });

        Assert.Equal(ClientState.Running, r.Hub.StateOf(a));
        Assert.Single(r.Launcher.Procs);
    }

    [Fact]
    public void Khong_xac_dinh_duoc_ket_noi_thi_khong_bao_gio_dong_client()
    {
        var r = Make();
        var a = Guid.NewGuid();
        r.Run(30, new[] { a });
        r.Probe.Value = null;
        r.Run(600, new[] { a });

        Assert.Equal(ClientState.Running, r.Hub.StateOf(a));
    }

    [Fact]
    public void Rot_lien_tiep_trong_10_phut_thi_cho_lau_dan_roi_chay_on_dinh_lau_thi_ve_40_giay()
    {
        var r = Make();
        var a = Guid.NewGuid();
        var ticked = new[] { a };

        // Lần 1: vào game ~30 giây rồi rớt -> chờ 40 giây.
        r.Run(1, ticked); r.Run(30, ticked); r.Probe.Value = false; r.Run(16, ticked);
        Assert.Equal(ClientEnd.Lost, r.Hub.EndOf(a));
        Assert.Equal(TimeSpan.FromSeconds(40), r.Hub.WaitLeftOf(a));

        // Lần 2: vừa mở lại đã rớt lần nữa trong vòng 10 phút -> chờ 80 giây.
        r.Probe.Value = true; r.Run(41, ticked);
        Assert.Equal(2, r.Launcher.Procs.Count);
        r.Run(30, ticked); r.Probe.Value = false; r.Run(16, ticked);
        Assert.Equal(TimeSpan.FromSeconds(80), r.Hub.WaitLeftOf(a));

        // Lần 3: chạy ổn định hơn 10 phút rồi mới rớt -> trở lại 40 giây.
        r.Probe.Value = true; r.Run(81, ticked);
        Assert.Equal(3, r.Launcher.Procs.Count);
        r.Run(11 * 60, ticked); r.Probe.Value = false; r.Run(16, ticked);
        Assert.Equal(TimeSpan.FromSeconds(40), r.Hub.WaitLeftOf(a));
    }

    [Fact]
    public void Mat_ket_noi_lien_tiep_3_lan_nhanh_thi_dung_tu_mo_lai_de_khong_lap_dang_nhap_mai_nhung_Login_tay_van_duoc()
    {
        var r = Make();
        var a = Guid.NewGuid();
        var ticked = new[] { a };

        // Ba lần: vào được vài chục giây rồi rớt (ví dụ sai mật khẩu bị server ngắt), mỗi lần chạy < 10 phút.
        for (int lan = 1; lan <= 3; lan++)
        {
            r.Probe.Value = true;
            r.Run(lan == 1 ? 1 : 400, ticked);        // lần đầu mở ngay; các lần sau chờ đủ rồi tự mở lại
            r.Run(30, ticked);
            r.Probe.Value = false;
            r.Run(16, ticked);
            Assert.Equal(ClientEnd.Lost, r.Hub.EndOf(a));
        }
        int opened = r.Launcher.Procs.Count;
        Assert.Equal(3, opened);

        Assert.True(r.Hub.AutoRetryPausedOf(a));
        r.Probe.Value = true;
        r.Run(3600, ticked);                          // cả giờ sau Auto login vẫn không mở lại
        Assert.Equal(opened, r.Launcher.Procs.Count);

        Assert.Equal(StartResult.Started, r.Hub.Request(a).Result);   // người dùng bấm Login thì thử lại được
        Assert.Equal(opened + 1, r.Launcher.Procs.Count);
        Assert.False(r.Hub.AutoRetryPausedOf(a));
    }

    [Fact]
    public void Nguoi_dung_bam_Login_sau_khi_mat_ket_noi_van_bi_chan_40_giay()
    {
        var r = Make();
        var a = Guid.NewGuid();
        r.Run(1, Array.Empty<Guid>(), auto: false);
        r.Hub.Request(a);
        r.Run(30, Array.Empty<Guid>(), auto: false);
        r.Launcher.Procs[0].HasExited = true;       // client sập
        r.Run(5, Array.Empty<Guid>(), auto: false);

        var early = r.Hub.Request(a);

        Assert.Equal(StartResult.TooSoon, early.Result);
        Assert.InRange(early.Wait.TotalSeconds, 30, 40);
    }

    // ---------- bảo trì ----------

    [Fact]
    public void Vao_khung_bao_tri_thi_dong_het_va_bo_hang_doi_het_khung_thi_mo_lai_dung_cac_acc_do_khong_cho_40_giay()
    {
        var r = Make(delayMs: 3000);
        var (a, b, c, other) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        r.Hub.Request(a);
        r.Hub.Request(b);      // đang chờ tới lượt (delay 3 giây)
        r.Hub.Request(other);
        r.Hub.Stop(other);     // người dùng tự đóng trước đó: không được tự mở lại
        r.Run(5, Array.Empty<Guid>(), auto: false);
        r.Hub.Request(c);
        Assert.Equal(ClientState.Running, r.Hub.StateOf(a));

        r.Run(1, new[] { a }, auto: true, maintenance: true);

        Assert.True(r.Hub.MaintenanceActive);
        foreach (var id in new[] { a, b, c }) Assert.Equal(ClientEnd.Maintenance, r.Hub.EndOf(id));
        Assert.All(r.Launcher.Procs.Where(p => r.Hub.Running().All(x => x.Pid != p.Id)), p => Assert.True(p.Killed));
        Assert.Empty(r.Hub.Running());

        // Suốt khung bảo trì: Auto login không mở gì dù acc có ✓.
        int opened = r.Launcher.Procs.Count;
        r.Run(600, new[] { a, b, c }, auto: true, maintenance: true);
        Assert.Equal(opened, r.Launcher.Procs.Count);

        // Hết khung: mở lại a, b, c (cách nhau delay), không phải chờ 40 giây, không mở acc `other`.
        r.Run(1, Array.Empty<Guid>(), auto: false, maintenance: false);
        Assert.False(r.Hub.MaintenanceActive);
        r.Run(15, Array.Empty<Guid>(), auto: false);
        Assert.Equal(ClientState.Running, r.Hub.StateOf(a));
        Assert.Equal(ClientState.Running, r.Hub.StateOf(b));
        Assert.Equal(ClientState.Running, r.Hub.StateOf(c));
        Assert.NotEqual(ClientState.Running, r.Hub.StateOf(other));
        Assert.Equal(opened + 3, r.Launcher.Procs.Count);
    }

    [Fact]
    public void Khung_bao_tri_khong_chan_nguoi_dung_tu_mo_acc_bang_tay()
    {
        var r = Make();
        var a = Guid.NewGuid();
        r.Run(1, Array.Empty<Guid>(), auto: true, maintenance: true);

        Assert.Equal(StartResult.Started, r.Hub.Request(a).Result);
        Assert.Equal(ClientState.Running, r.Hub.StateOf(a));
        r.Run(5, Array.Empty<Guid>(), auto: true, maintenance: true);
        Assert.Equal(ClientState.Running, r.Hub.StateOf(a));   // chỉ đóng một lần khi VÀO khung, không đóng liên tục
    }
}
