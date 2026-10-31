namespace ToolQL.Core.Tests;

public class ClientHubTests
{
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class FakeProcess : IClientProcess
    {
        public int Id { get; init; }
        public bool HasExited { get; set; }
        public int ExitCode { get; set; }
        public bool Killed { get; private set; }
        public void Kill()
        {
            Killed = true;
            HasExited = true;
            ExitCode = -1;
        }
        public void Dispose() { }
    }

    private sealed class FakeLauncher : IClientLauncher
    {
        private int _next = 100;
        public List<ClientLaunchSpec> Specs { get; } = new();
        public List<FakeProcess> Processes { get; } = new();
        public Exception? ThrowOnStart { get; set; }

        public IClientProcess Start(ClientLaunchSpec spec)
        {
            if (ThrowOnStart != null) throw ThrowOnStart;
            Specs.Add(spec);
            var p = new FakeProcess { Id = _next++ };
            Processes.Add(p);
            return p;
        }
    }

    private sealed class Rig
    {
        public required ClientHub Hub { get; init; }
        public required FakeLauncher Launcher { get; init; }
        public required Clock Clock { get; init; }
        public List<Guid> Cleaned { get; } = new();
        public List<(Guid Id, string Message)> Failures { get; } = new();
    }

    private static Rig Make(int delayMs = 3000, Func<Guid, ClientLaunchSpec>? prepare = null)
    {
        var launcher = new FakeLauncher();
        var clock = new Clock();
        var cleaned = new List<Guid>();
        Rig? rig = null;
        var hub = new ClientHub(launcher, prepare ?? (id => new ClientLaunchSpec("javaw", "args-" + id, "dir")),
            id => rig!.Cleaned.Add(id), TimeSpan.FromMilliseconds(delayMs), clock);
        rig = new Rig { Hub = hub, Launcher = launcher, Clock = clock };
        hub.Failed += (id, message) => rig.Failures.Add((id, message));
        return rig;
    }

    private static readonly TimeSpan Forty = TimeSpan.FromSeconds(40);

    [Fact]
    public void Yeu_cau_mo_thi_mo_ngay_khi_khong_phai_doi_va_ghi_tien_trinh()
    {
        var r = Make();
        var a = Guid.NewGuid();

        var outcome = r.Hub.Request(a);

        Assert.Equal(StartResult.Started, outcome.Result);
        Assert.Equal("args-" + a, Assert.Single(r.Launcher.Specs).Arguments);
        Assert.Equal(ClientState.Running, r.Hub.StateOf(a));
        Assert.Equal(100, r.Hub.ProcessIdOf(a));
        Assert.Equal(r.Clock.GetUtcNow(), r.Hub.StartedAtOf(a));
    }

    [Fact]
    public void Dang_chay_thi_yeu_cau_lan_hai_khong_mo_them()
    {
        var r = Make();
        var a = Guid.NewGuid();
        r.Hub.Request(a);

        Assert.Equal(StartResult.AlreadyRunning, r.Hub.Request(a).Result);
        Assert.Single(r.Launcher.Specs);
    }

    [Fact]
    public void Tien_trinh_thoat_thi_don_thong_tin_dang_nhap_va_phai_cho_40_giay_moi_mo_lai()
    {
        var r = Make();
        var a = Guid.NewGuid();
        r.Hub.Request(a);
        r.Launcher.Processes[0].HasExited = true;
        r.Launcher.Processes[0].ExitCode = 1;

        r.Clock.Advance(TimeSpan.FromSeconds(5));
        r.Hub.Tick(Array.Empty<Guid>(), autoLogin: false);

        Assert.Equal(ClientState.Exited, r.Hub.StateOf(a));
        Assert.Equal(1, r.Hub.ExitCodeOf(a));
        Assert.False(r.Hub.StoppedByUserOf(a));   // tự thoát hoặc bị giết từ bên ngoài, không phải người dùng bấm Đóng
        Assert.Equal(new[] { a }, r.Cleaned);   // xóa tài khoản/mật khẩu khỏi kho RMS

        r.Clock.Advance(TimeSpan.FromSeconds(10));
        var early = r.Hub.Request(a);
        Assert.Equal(StartResult.TooSoon, early.Result);
        Assert.Equal(TimeSpan.FromSeconds(30), early.Wait);   // thoát lúc +5 s, bây giờ +15 s: đã 10 giây, còn 30 giây
        Assert.Single(r.Launcher.Specs);

        r.Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(StartResult.Started, r.Hub.Request(a).Result);
        Assert.Equal(2, r.Launcher.Specs.Count);
    }

    [Fact]
    public void Dong_tay_giet_tien_trinh_don_dep_nhung_KHONG_phai_cho_40_giay_vi_khong_phai_mat_ket_noi()
    {
        var r = Make();
        var a = Guid.NewGuid();
        r.Hub.Request(a);

        r.Hub.Stop(a);

        Assert.True(r.Launcher.Processes[0].Killed);
        Assert.Equal(ClientState.Exited, r.Hub.StateOf(a));
        Assert.True(r.Hub.StoppedByUserOf(a));   // do người dùng đóng: khác với tự thoát/bị giết từ ngoài
        Assert.Equal(new[] { a }, r.Cleaned);
        Assert.Equal(StartResult.Started, r.Hub.Request(a).Result);   // được nhận ngay, không bị chặn 40 giây
        r.Clock.Advance(TimeSpan.FromSeconds(3));                     // chỉ còn khoảng nghỉ 3 giây giữa hai lần mở acc
        r.Hub.Tick(Array.Empty<Guid>(), autoLogin: false);
        Assert.Equal(2, r.Launcher.Specs.Count);
    }

    [Fact]
    public void Mo_nhieu_acc_thi_cach_nhau_do_tre_moi_lan_mot_cai()
    {
        var r = Make(delayMs: 3000);
        var (a, b, c) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        r.Hub.Request(a);
        r.Hub.Request(b);
        r.Hub.Request(c);

        Assert.Equal(ClientState.Running, r.Hub.StateOf(a));
        Assert.Equal(ClientState.Queued, r.Hub.StateOf(b));
        Assert.Equal(ClientState.Queued, r.Hub.StateOf(c));
        Assert.Empty(r.Hub.Tick(Array.Empty<Guid>(), false));   // chưa đủ 3 giây

        r.Clock.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(new[] { b }, r.Hub.Tick(Array.Empty<Guid>(), false));
        Assert.Equal(ClientState.Queued, r.Hub.StateOf(c));

        r.Clock.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(new[] { c }, r.Hub.Tick(Array.Empty<Guid>(), false));
        Assert.Equal(3, r.Launcher.Specs.Count);
    }

    [Fact]
    public void Dong_acc_dang_cho_thi_bo_khoi_hang_doi()
    {
        var r = Make();
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        r.Hub.Request(a);
        r.Hub.Request(b);

        r.Hub.Stop(b);
        r.Clock.Advance(TimeSpan.FromSeconds(10));
        r.Hub.Tick(Array.Empty<Guid>(), false);

        Assert.Single(r.Launcher.Specs);
        Assert.Equal(ClientState.NotOpened, r.Hub.StateOf(b));
    }

    [Fact]
    public void Auto_login_mo_acc_co_tick_chua_mo_chi_khi_cong_tac_bat()
    {
        var r = Make();
        var (ticked, unticked) = (Guid.NewGuid(), Guid.NewGuid());

        Assert.Empty(r.Hub.Tick(new[] { ticked }, autoLogin: false));
        Assert.Empty(r.Launcher.Specs);

        Assert.Equal(new[] { ticked }, r.Hub.Tick(new[] { ticked }, autoLogin: true));
        Assert.Single(r.Launcher.Specs);
        Assert.Equal(ClientState.NotOpened, r.Hub.StateOf(unticked));   // acc không tick không bị đụng tới
    }

    [Fact]
    public void Auto_login_mo_lai_acc_bi_thoat_chi_sau_40_giay()
    {
        var r = Make();
        var a = Guid.NewGuid();
        r.Hub.Tick(new[] { a }, autoLogin: true);
        r.Launcher.Processes[0].HasExited = true;

        r.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Empty(r.Hub.Tick(new[] { a }, autoLogin: true));   // vừa phát hiện thoát
        r.Clock.Advance(TimeSpan.FromSeconds(38));
        Assert.Empty(r.Hub.Tick(new[] { a }, autoLogin: true));   // mới 38 giây
        Assert.Single(r.Launcher.Specs);

        r.Clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(new[] { a }, r.Hub.Tick(new[] { a }, autoLogin: true));   // đủ 40 giây
        Assert.Equal(2, r.Launcher.Specs.Count);
    }

    [Fact]
    public void Auto_login_khong_xep_hang_hai_lan_cung_mot_acc()
    {
        var r = Make();
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());

        r.Hub.Tick(new[] { a, b }, autoLogin: true);    // mở a, b chờ
        r.Hub.Tick(new[] { a, b }, autoLogin: true);
        r.Hub.Tick(new[] { a, b }, autoLogin: true);
        r.Clock.Advance(TimeSpan.FromSeconds(3));
        r.Hub.Tick(new[] { a, b }, autoLogin: true);

        Assert.Equal(2, r.Launcher.Specs.Count);
    }

    [Fact]
    public void Loi_chuan_bi_hoac_mo_thi_bao_loi_khong_mo_don_dep_va_tinh_40_giay_de_khong_lap_lai_ngay()
    {
        var failing = Make(prepare: _ => throw new InvalidOperationException("Chưa có thư mục dữ liệu mẫu"));
        var a = Guid.NewGuid();
        failing.Hub.Request(a);
        Assert.Equal(("Chưa có thư mục dữ liệu mẫu", a), (Assert.Single(failing.Failures).Message, failing.Failures[0].Id));
        Assert.Empty(failing.Launcher.Specs);
        Assert.Equal(ClientState.Exited, failing.Hub.StateOf(a));
        Assert.Equal("Chưa có thư mục dữ liệu mẫu", failing.Hub.ErrorOf(a));

        var launching = Make();
        launching.Launcher.ThrowOnStart = new System.ComponentModel.Win32Exception("không thấy javaw.exe");
        var b = Guid.NewGuid();
        launching.Hub.Request(b);
        Assert.Single(launching.Failures);
        Assert.Contains(b, launching.Cleaned);   // đã ghi mật khẩu thì phải xóa lại

        // Auto login không thử lại mỗi giây khi Java hỏng: phải chờ 40 giây.
        launching.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Empty(launching.Hub.Tick(new[] { b }, autoLogin: true));
        Assert.Single(launching.Failures);
    }

    [Fact]
    public void Tat_het_giet_moi_tien_trinh_va_bo_hang_doi_va_Dispose_cung_vay()
    {
        var r = Make();
        var (a, b, c) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        r.Hub.Request(a);
        r.Hub.Request(b);   // chờ

        r.Hub.StopAll();

        Assert.True(r.Launcher.Processes[0].Killed);
        Assert.Equal(ClientState.NotOpened, r.Hub.StateOf(b));
        r.Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Empty(r.Hub.Tick(Array.Empty<Guid>(), false));

        r.Clock.Advance(Forty);
        r.Hub.Request(c);
        r.Hub.Dispose();
        Assert.True(r.Launcher.Processes[1].Killed);
    }

    [Fact]
    public void Danh_sach_tien_trinh_dang_chay_cho_viec_xep_cua_so()
    {
        var r = Make(delayMs: 0);
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        r.Hub.Request(a);
        r.Hub.Request(b);
        r.Hub.Tick(Array.Empty<Guid>(), false);

        Assert.Equal(new[] { (a, 100), (b, 101) }, r.Hub.Running().OrderBy(x => x.Pid).ToArray());
    }
}
