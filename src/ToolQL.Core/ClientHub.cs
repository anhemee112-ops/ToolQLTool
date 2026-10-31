namespace ToolQL.Core;

/// <summary>Một tiến trình client đang chạy (trình J2ME mang cửa sổ game của một acc).</summary>
public interface IClientProcess : IDisposable
{
    int Id { get; }
    bool HasExited { get; }
    int ExitCode { get; }
    void Kill();
}

public interface IClientLauncher
{
    /// <summary>Mở tiến trình. Ném ngoại lệ nếu không mở được (ví dụ thiếu Java).</summary>
    IClientProcess Start(ClientLaunchSpec spec);
}

public enum ClientState
{
    /// <summary>Chưa mở, hoặc đã bị bỏ khỏi hàng đợi.</summary>
    NotOpened,
    /// <summary>Đã yêu cầu mở, đang chờ tới lượt (khoảng nghỉ giữa hai lần mở acc).</summary>
    Queued,
    Running,
    /// <summary>Tiến trình đã kết thúc hoặc mở không được; xem <see cref="ClientHub.EndOf"/>.</summary>
    Exited,
}

/// <summary>Vì sao lần chạy gần nhất của một acc kết thúc.</summary>
public enum ClientEnd
{
    None,
    /// <summary>Người dùng bấm Đóng / Tắt tất cả. KHÔNG phải mất kết nối: mở lại ngay được.</summary>
    User,
    /// <summary>Mất kết nối: client tự thoát/sập/bị giết từ ngoài, hoặc kết nối tới server rớt khi cửa sổ còn mở. Phải chờ >= 40 giây mới đăng nhập lại.</summary>
    Lost,
    /// <summary>Mở không được (thiếu Java, thiếu dữ liệu mẫu...). Người dùng thử lại được ngay; Auto login chờ 40 giây giữa các lần thử.</summary>
    Failed,
    /// <summary>Bị đóng vì vào khung giờ bảo trì; hết khung sẽ được mở lại.</summary>
    Maintenance,
}

/// <summary>
/// Quản lý các cửa sổ client (một tiến trình mỗi acc) của nhiều acc: mở có khoảng nghỉ giữa hai lần, đóng, phát hiện mất kết nối,
/// Auto login và bảo trì định kỳ. Chỉ gọi từ MỘT luồng (luồng giao diện, gọi <see cref="Tick"/> mỗi giây); không có khóa. Không biết gì
/// về cửa sổ hay mật khẩu: việc chuẩn bị thư mục acc nằm ở <c>prepare</c>, dọn mật khẩu ở <c>cleanup</c>.
/// <para>
/// Quy tắc từ chủ repo: sau khi <b>mất kết nối</b> phải chờ ít nhất 40 giây mới đăng nhập lại, sớm hơn thì server khóa acc. Chỉ mất kết nối
/// mới phải chờ (xem <see cref="ClientEnd.Lost"/>); đóng tay, bảo trì và mở lỗi thì không. Mất kết nối liên tiếp trong 10 phút thì chờ lâu dần
/// (40, 80, 160... tối đa 5 phút) để không lặp đăng nhập liên tục.
/// </para>
/// <para>
/// Mất kết nối được phát hiện từ <see cref="IConnectionProbe"/> (kết nối TCP tới server game): chỉ tính khi kết nối từng ổn định rồi biến mất,
/// xem <see cref="ConnectionWatcher"/>. Client không báo gì ra ngoài nên đây là cách duy nhất khi cửa sổ còn sống.
/// </para>
/// </summary>
public sealed class ClientHub : IDisposable
{
    private static readonly TimeSpan LongRun = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MaxLossDelay = TimeSpan.FromMinutes(5);

    /// <summary>Số lần mất kết nối liên tiếp (mỗi lần chạy < 10 phút) mà Auto login chịu thử lại.</summary>
    public const int MaxConsecutiveLosses = 3;

    private sealed class Slot
    {
        public ClientState State = ClientState.NotOpened;
        public ClientEnd End = ClientEnd.None;
        public IClientProcess? Process;
        public ConnectionWatcher? Watcher;
        public DateTimeOffset? StartedAt, EndedAt;
        public int? ExitCode;
        public string? Error;
        public int ConsecutiveLosses;
    }

    private readonly IClientLauncher _launcher;
    private readonly Func<Guid, ClientLaunchSpec> _prepare;
    private readonly Action<Guid> _cleanup;
    private readonly TimeSpan _openDelay;
    private readonly TimeProvider _time;
    private readonly IConnectionProbe? _probe;
    private readonly TimeSpan _stableAfter;
    private readonly TimeSpan _lostAfter;
    private readonly Dictionary<Guid, Slot> _slots = new();
    private readonly List<Guid> _queue = new();
    private readonly HashSet<Guid> _resumeAfterMaintenance = new();
    private DateTimeOffset? _lastOpen;

    public ClientHub(
        IClientLauncher launcher, Func<Guid, ClientLaunchSpec> prepare, Action<Guid> cleanup, TimeSpan openDelay,
        TimeProvider? time = null, IConnectionProbe? probe = null, TimeSpan? stableAfter = null, TimeSpan? lostAfter = null)
    {
        _launcher = launcher;
        _prepare = prepare;
        _cleanup = cleanup;
        _openDelay = openDelay;
        _time = time ?? TimeProvider.System;
        _probe = probe;
        _stableAfter = stableAfter ?? TimeSpan.FromSeconds(20);
        _lostAfter = lostAfter ?? TimeSpan.FromSeconds(15);
    }

    /// <summary>Trạng thái của một acc đã đổi (mở, thoát, xếp hàng...).</summary>
    public event Action<Guid>? Changed;

    /// <summary>Mở một acc không được (thiếu Java, thiếu dữ liệu mẫu...); thông báo bằng tiếng Việt, không chứa mật khẩu.</summary>
    public event Action<Guid, string>? Failed;

    /// <summary>Đang trong khung giờ bảo trì (đã đóng hết client, không tự mở lại).</summary>
    public bool MaintenanceActive { get; private set; }

    public ClientState StateOf(Guid id) => _slots.TryGetValue(id, out var s) ? s.State : ClientState.NotOpened;
    public ClientEnd EndOf(Guid id) => _slots.TryGetValue(id, out var s) ? s.End : ClientEnd.None;
    public int? ProcessIdOf(Guid id) => _slots.TryGetValue(id, out var s) && s.State == ClientState.Running ? s.Process?.Id : null;
    public DateTimeOffset? StartedAtOf(Guid id) => _slots.TryGetValue(id, out var s) ? s.StartedAt : null;
    public DateTimeOffset? EndedAtOf(Guid id) => _slots.TryGetValue(id, out var s) ? s.EndedAt : null;
    public int? ExitCodeOf(Guid id) => _slots.TryGetValue(id, out var s) ? s.ExitCode : null;
    public string? ErrorOf(Guid id) => _slots.TryGetValue(id, out var s) ? s.Error : null;

    /// <summary>True nếu lần chạy gần nhất kết thúc vì người dùng đóng (khác với mất kết nối).</summary>
    public bool StoppedByUserOf(Guid id) => EndOf(id) == ClientEnd.User;

    /// <summary>Lần quan sát gần nhất có kết nối TCP tới server game hay không; null nếu chưa biết (chưa chạy, hoặc không có bộ dò).</summary>
    public bool? ConnectedOf(Guid id) => _slots.TryGetValue(id, out var s) ? s.Watcher?.Connected : null;

    /// <summary>Đã vào game (kết nối từng ổn định) trong lần chạy này.</summary>
    public bool StableConnectionOf(Guid id) => _slots.TryGetValue(id, out var s) && s.Watcher?.WasStable == true;

    /// <summary>
    /// True nếu Auto login đã DỪNG mở lại acc này vì mất kết nối liên tiếp <see cref="MaxConsecutiveLosses"/> lần mà mỗi lần chạy chưa tới 10 phút
    /// (ví dụ sai mật khẩu bị server ngắt): tránh lặp đăng nhập mãi làm khóa acc. Người dùng bấm Login thì thử lại được.
    /// </summary>
    public bool AutoRetryPausedOf(Guid id) => _slots.TryGetValue(id, out var s) && s.End == ClientEnd.Lost && s.ConsecutiveLosses >= MaxConsecutiveLosses;

    /// <summary>Còn phải chờ bao lâu thì Auto login được mở lại acc này (0 nếu không phải chờ).</summary>
    public TimeSpan WaitLeftOf(Guid id) => _slots.TryGetValue(id, out var s) ? WaitLeft(s, _time.GetUtcNow(), manual: false) : TimeSpan.Zero;

    /// <summary>Các acc đang chạy kèm mã tiến trình (để xếp, ẩn, hiện cửa sổ).</summary>
    public IEnumerable<(Guid Id, int Pid)> Running() =>
        _slots.Where(kv => kv.Value.State == ClientState.Running && kv.Value.Process != null).Select(kv => (kv.Key, kv.Value.Process!.Id)).ToArray();

    /// <summary>
    /// Yêu cầu mở một acc (người dùng bấm Login). <see cref="StartResult.Started"/> nghĩa là đã nhận (mở ngay nếu tới lượt, không thì xếp hàng);
    /// kết quả mở thật báo qua <see cref="Changed"/> hoặc <see cref="Failed"/>. Sau MẤT KẾT NỐI thì trả <see cref="StartResult.TooSoon"/> tới khi đủ 40 giây;
    /// các lần kết thúc khác không chặn.
    /// </summary>
    public StartOutcome Request(Guid id)
    {
        var slot = GetSlot(id);
        if (slot.State is ClientState.Running or ClientState.Queued) return new StartOutcome(StartResult.AlreadyRunning);
        var wait = WaitLeft(slot, _time.GetUtcNow(), manual: true);
        if (wait > TimeSpan.Zero) return new StartOutcome(StartResult.TooSoon, wait);

        Enqueue(id, slot);
        Pump();
        return new StartOutcome(StartResult.Started);
    }

    /// <summary>
    /// Gọi mỗi giây: phát hiện tiến trình thoát và kết nối rớt, vào/ra khung bảo trì (<paramref name="maintenance"/>), xếp hàng mở các acc có ✓
    /// khi <paramref name="autoLogin"/> (đã chờ đủ sau khi mất kết nối), rồi mở một acc trong hàng nếu đã qua khoảng nghỉ. Trả các acc vừa mở.
    /// </summary>
    public IReadOnlyList<Guid> Tick(IEnumerable<Guid> ticked, bool autoLogin, bool maintenance = false)
    {
        var now = _time.GetUtcNow();

        foreach (var (id, slot) in _slots.Where(kv => kv.Value.Process != null).ToArray())
        {
            var process = slot.Process!;
            if (process.HasExited)
            {
                End(id, slot, process.ExitCode, now, ClientEnd.Lost);   // tự thoát, sập, hoặc bị giết từ bên ngoài
            }
            else if (_probe != null && slot.Watcher != null && slot.Watcher.Observe(now, _probe.IsConnected(process.Id)))
            {
                Kill(process);                                           // kết nối rớt mà cửa sổ còn mở: đóng hẳn để mở lại sạch
                End(id, slot, -1, now, ClientEnd.Lost);
            }
        }

        if (maintenance && !MaintenanceActive) BeginMaintenance(now);
        else if (!maintenance && MaintenanceActive) EndMaintenance();

        if (autoLogin && !MaintenanceActive)
        {
            foreach (var id in ticked)
            {
                var slot = GetSlot(id);
                if (slot.State is ClientState.NotOpened or ClientState.Exited && !AutoRetryPausedOf(id) && WaitLeft(slot, now, manual: false) <= TimeSpan.Zero)
                    Enqueue(id, slot);
            }
        }
        return Pump();
    }

    /// <summary>Đóng một acc theo yêu cầu người dùng: giết tiến trình hoặc bỏ khỏi hàng đợi. Không làm gì nếu acc không chạy.</summary>
    public void Stop(Guid id)
    {
        if (!_slots.TryGetValue(id, out var slot)) return;
        if (slot.State == ClientState.Queued)
        {
            _queue.Remove(id);
            slot.State = ClientState.NotOpened;
            Changed?.Invoke(id);
        }
        else if (slot.Process != null)
        {
            var process = slot.Process;
            Kill(process);
            End(id, slot, process.HasExited ? process.ExitCode : -1, _time.GetUtcNow(), ClientEnd.User);
        }
    }

    public void StopAll()
    {
        _queue.Clear();
        foreach (var id in _slots.Keys.ToArray()) Stop(id);
    }

    public void Dispose() => StopAll();

    private Slot GetSlot(Guid id)
    {
        if (!_slots.TryGetValue(id, out var slot)) _slots[id] = slot = new Slot();
        return slot;
    }

    /// <summary>Thời gian còn phải chờ trước khi được mở lại. Người dùng bấm (<paramref name="manual"/>) chỉ bị chặn 40 giây sau mất kết nối.</summary>
    private static TimeSpan WaitLeft(Slot slot, DateTimeOffset now, bool manual)
    {
        if (slot.EndedAt is not { } ended) return TimeSpan.Zero;
        var required = slot.End switch
        {
            ClientEnd.Lost => manual ? Backoff.MinReconnectDelay : LossDelay(slot.ConsecutiveLosses),
            ClientEnd.Failed => manual ? TimeSpan.Zero : Backoff.MinReconnectDelay,
            _ => TimeSpan.Zero,
        };
        var left = required - (now - ended);
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    private static TimeSpan LossDelay(int consecutiveLosses)
    {
        double seconds = Backoff.MinReconnectDelay.TotalSeconds * Math.Pow(2, Math.Max(0, consecutiveLosses - 1));
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxLossDelay.TotalSeconds));
    }

    private void Enqueue(Guid id, Slot slot)
    {
        slot.State = ClientState.Queued;
        _queue.Add(id);
        Changed?.Invoke(id);
    }

    private List<Guid> Pump()
    {
        var started = new List<Guid>();
        var now = _time.GetUtcNow();
        if (_queue.Count == 0 || (_lastOpen is { } last && now - last < _openDelay)) return started;

        var id = _queue[0];
        _queue.RemoveAt(0);
        var slot = GetSlot(id);
        try
        {
            var spec = _prepare(id);
            slot.Process = _launcher.Start(spec);
            slot.Watcher = _probe is null ? null : new ConnectionWatcher(_stableAfter, _lostAfter);
            slot.State = ClientState.Running;
            slot.End = ClientEnd.None;
            slot.StartedAt = now;
            slot.EndedAt = null;
            slot.ExitCode = null;
            slot.Error = null;
            _lastOpen = now;
            started.Add(id);
            Changed?.Invoke(id);
        }
        catch (Exception ex)
        {
            TryCleanup(id, null);
            slot.State = ClientState.Exited;
            slot.End = ClientEnd.Failed;
            slot.EndedAt = now;
            slot.Error = ex.Message;
            Changed?.Invoke(id);
            Failed?.Invoke(id, ex.Message);
        }
        return started;
    }

    private void End(Guid id, Slot slot, int exitCode, DateTimeOffset now, ClientEnd kind)
    {
        if (kind == ClientEnd.Lost)
        {
            bool longRun = slot.StartedAt is { } started && now - started >= LongRun;
            slot.ConsecutiveLosses = longRun ? 1 : slot.ConsecutiveLosses + 1;
        }
        slot.Process?.Dispose();
        slot.Process = null;
        slot.Watcher = null;
        slot.State = ClientState.Exited;
        slot.End = kind;
        slot.EndedAt = now;
        slot.ExitCode = exitCode;
        TryCleanup(id, slot);
        Changed?.Invoke(id);
    }

    private void TryCleanup(Guid id, Slot? slot)
    {
        try
        {
            _cleanup(id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (slot != null) slot.Error = "Không xóa được thông tin đăng nhập trong kho của client: " + ex.Message;
        }
    }

    private static void Kill(IClientProcess process)
    {
        try
        {
            process.Kill();
        }
        catch (InvalidOperationException)
        {
            // đã thoát đúng lúc này
        }
    }

    /// <summary>Vào khung bảo trì: nhớ các acc đang chạy hoặc đang chờ mở, rồi đóng hết (không tính là mất kết nối).</summary>
    private void BeginMaintenance(DateTimeOffset now)
    {
        MaintenanceActive = true;
        _resumeAfterMaintenance.Clear();
        _queue.Clear();
        foreach (var (id, slot) in _slots.ToArray())
        {
            if (slot.State == ClientState.Running && slot.Process != null)
            {
                _resumeAfterMaintenance.Add(id);
                var process = slot.Process;
                Kill(process);
                End(id, slot, process.HasExited ? process.ExitCode : -1, now, ClientEnd.Maintenance);
            }
            else if (slot.State == ClientState.Queued)
            {
                _resumeAfterMaintenance.Add(id);
                slot.State = ClientState.Exited;
                slot.End = ClientEnd.Maintenance;
                slot.EndedAt = now;
                Changed?.Invoke(id);
            }
        }
    }

    /// <summary>Hết khung bảo trì: mở lại các acc đã bị đóng khi vào khung (xếp hàng, cách nhau khoảng nghỉ), không phải chờ 40 giây.</summary>
    private void EndMaintenance()
    {
        MaintenanceActive = false;
        foreach (var id in _resumeAfterMaintenance)
        {
            var slot = GetSlot(id);
            if (slot.State == ClientState.Exited && slot.End == ClientEnd.Maintenance) Enqueue(id, slot);
        }
        _resumeAfterMaintenance.Clear();
    }
}
