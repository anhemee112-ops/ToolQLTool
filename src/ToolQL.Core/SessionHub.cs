using ToolQL.Protocol;

namespace ToolQL.Core;

/// <summary>Nhãn hiển thị và phân loại của <see cref="SessionState"/> cho giao diện.</summary>
public static class SessionStateText
{
    public static string For(SessionState state) => state switch
    {
        SessionState.Idle => "Chưa chạy",
        SessionState.Connecting => "Đang kết nối",
        SessionState.LoggingIn => "Đang đăng nhập",
        SessionState.Loading => "Đang tải dữ liệu",
        SessionState.InGame => "Trong game",
        SessionState.Reconnecting => "Chờ nối lại",
        SessionState.NeedCaptcha => "Cần captcha",
        SessionState.NeedCharacter => "Cần tạo nhân vật",
        SessionState.Rejected => "Bị từ chối",
        SessionState.Faulted => "Lỗi giao thức",
        SessionState.Stopped => "Đã dừng",
        _ => state.ToString(),
    };

    /// <summary>True khi phiên còn đang chạy (kể cả chờ nối lại); false khi chưa chạy hoặc đã dừng hẳn.</summary>
    public static bool IsRunning(SessionState state) =>
        state is SessionState.Connecting or SessionState.LoggingIn or SessionState.Loading or SessionState.InGame or SessionState.Reconnecting;
}

public enum StartResult
{
    Started,
    /// <summary>Acc đang chạy (hoặc là acc demo); không nối thêm.</summary>
    AlreadyRunning,
    /// <summary>Phiên trước của acc mới kết thúc chưa đủ <see cref="Backoff.MinReconnectDelay"/>; không nối.</summary>
    TooSoon,
}

/// <summary>Kết quả <see cref="SessionHub.Start"/>; <paramref name="Wait"/> là thời gian còn phải chờ khi <see cref="StartResult.TooSoon"/>.</summary>
public sealed record StartOutcome(StartResult Result, TimeSpan Wait = default);

/// <summary>
/// Giữ các <see cref="AccountSession"/> của nhiều acc trong MỘT tiến trình (mỗi phiên một luồng đọc riêng). Giao diện chỉ
/// gọi <see cref="Start"/>, <see cref="Stop"/>, <see cref="StateOf"/>, <see cref="GameOf"/>; trạng thái game lấy bằng
/// <see cref="GameState.Snapshot"/> từ luồng khác.
/// <para>
/// Quy tắc từ chủ repo: sau khi mất kết nối phải chờ ít nhất 40 giây mới được đăng nhập lại, sớm hơn thì server khóa acc.
/// Dừng tay và các trạng thái kết thúc (từ chối, captcha, lỗi) cũng tính là mất kết nối, nên <see cref="Start"/> từ chối
/// với <see cref="StartResult.TooSoon"/> nếu phiên trước của acc đó kết thúc chưa đủ <see cref="Backoff.MinReconnectDelay"/>.
/// </para>
/// </summary>
public sealed class SessionHub : IDisposable
{
    private sealed class Slot
    {
        public AccountSession? Session;
        public GameState? Demo;
        public SessionState State = SessionState.Idle;
        public string? Message;
        public DateTimeOffset? EndedAt;
    }

    private readonly AccountStore _store;
    private readonly Func<GameServer, Func<CancellationToken, Stream>> _connectorFor;
    private readonly Func<SessionOptions> _options;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Slot> _slots = new();

    public SessionHub(
        AccountStore store,
        Func<GameServer, Func<CancellationToken, Stream>>? connectorFor = null,
        Func<SessionOptions>? options = null,
        TimeProvider? time = null)
    {
        _store = store;
        _connectorFor = connectorFor ?? (server => TcpConnector.Create(server.Host, server.Port));
        _options = options ?? (() => new SessionOptions());
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Đổi trạng thái của một acc. Gọi trên luồng của phiên (hoặc luồng gọi <see cref="Stop"/>); giao diện phải tự chuyển luồng.</summary>
    public event Action<Guid, SessionState, string?>? Changed;

    public SessionState StateOf(Guid id)
    {
        lock (_gate) return _slots.TryGetValue(id, out var slot) ? slot.State : SessionState.Idle;
    }

    public string? MessageOf(Guid id)
    {
        lock (_gate) return _slots.TryGetValue(id, out var slot) ? slot.Message : null;
    }

    /// <summary>Trạng thái game của acc; null nếu acc chưa từng chạy.</summary>
    public GameState? GameOf(Guid id)
    {
        lock (_gate)
        {
            if (!_slots.TryGetValue(id, out var slot)) return null;
            return slot.Demo ?? slot.Session?.Game;
        }
    }

    /// <summary>
    /// Bắt đầu một acc. Ném <see cref="InvalidOperationException"/> nếu server của acc không có trong danh sách,
    /// <see cref="InvalidDataException"/> nếu không giải được mật khẩu đã lưu (cần nhập lại); cả hai trường hợp không nối mạng.
    /// </summary>
    public StartOutcome Start(Guid id)
    {
        Slot slot;
        AccountSession session;
        lock (_gate)
        {
            slot = GetSlot(id);
            if (slot.Demo != null || SessionStateText.IsRunning(slot.State)) return new StartOutcome(StartResult.AlreadyRunning);
            if (slot.EndedAt is { } ended)
            {
                var wait = Backoff.MinReconnectDelay - (_time.GetUtcNow() - ended);
                if (wait > TimeSpan.Zero) return new StartOutcome(StartResult.TooSoon, wait);
            }

            var entry = _store.Entries.FirstOrDefault(e => e.Id == id) ?? throw new KeyNotFoundException("Không có acc với id " + id);
            var server = ServerList.Find(entry.Server)
                ?? throw new InvalidOperationException($"Server \"{entry.Server}\" không có trong danh sách (acc {entry.User}).");
            var credentials = _store.GetCredentials(id);

            session = new AccountSession(credentials, _connectorFor(server), _options());
            slot.Session = session;
            slot.State = SessionState.Connecting;
            slot.Message = null;
            slot.EndedAt = null;
            session.StateChanged += (state, message) => OnStateChanged(id, session, state, message);
        }
        Changed?.Invoke(id, SessionState.Connecting, null);
        session.Start();
        return new StartOutcome(StartResult.Started);
    }

    /// <summary>Dừng một acc (đóng socket, chờ luồng kết thúc tối đa vài giây). Không làm gì nếu acc không chạy.</summary>
    public void Stop(Guid id)
    {
        AccountSession? session;
        lock (_gate)
        {
            if (!_slots.TryGetValue(id, out var slot) || slot.Demo != null) return;
            session = slot.Session;
        }
        session?.Stop();
        lock (_gate)
        {
            var slot = _slots[id];
            if (slot.Session == session) slot.EndedAt = _time.GetUtcNow();
        }
    }

    public void StopAll()
    {
        Guid[] ids;
        lock (_gate) ids = _slots.Keys.ToArray();
        foreach (var id in ids) Stop(id);
    }

    /// <summary>Gắn một <see cref="GameState"/> có sẵn (phát lại từ tệp bắt gói) làm acc luôn "Trong game", không nối mạng. Để thử giao diện.</summary>
    public void RegisterDemo(Guid id, GameState game)
    {
        lock (_gate)
        {
            var slot = GetSlot(id);
            slot.Demo = game;
            slot.State = SessionState.InGame;
            slot.Message = "Chế độ demo: phát lại từ tệp bắt gói, không nối mạng.";
        }
        Changed?.Invoke(id, SessionState.InGame, MessageOf(id));
    }

    public void Dispose() => StopAll();

    private Slot GetSlot(Guid id)
    {
        if (!_slots.TryGetValue(id, out var slot)) _slots[id] = slot = new Slot();
        return slot;
    }

    private void OnStateChanged(Guid id, AccountSession session, SessionState state, string? message)
    {
        lock (_gate)
        {
            var slot = GetSlot(id);
            if (slot.Session != session) return;   // phiên cũ đã bị thay: bỏ qua
            slot.State = state;
            slot.Message = message;
            if (!SessionStateText.IsRunning(state) && state != SessionState.Idle) slot.EndedAt ??= _time.GetUtcNow();
        }
        Changed?.Invoke(id, state, message);
    }
}
