using System.Net.Sockets;
using ToolQL.Protocol;

namespace ToolQL.Core;

public enum SessionState
{
    /// <summary>Chưa chạy.</summary>
    Idle,
    /// <summary>Đang nối TCP và bắt tay khóa.</summary>
    Connecting,
    /// <summary>Đã gửi đăng nhập, chưa nhận phiên bản dữ liệu (-28 sub 4).</summary>
    LoggingIn,
    /// <summary>Đang tải bảng dữ liệu và nạp map.</summary>
    Loading,
    InGame,
    /// <summary>Mất mạng, đang chờ nối lại (thời gian chờ tăng dần).</summary>
    Reconnecting,
    /// <summary>Server gửi -85. Dừng, KHÔNG xử lý captcha.</summary>
    NeedCaptcha,
    NeedCharacter,
    /// <summary>Server từ chối đăng nhập (hoặc ngắt khi đang đăng nhập). Không thử lại.</summary>
    Rejected,
    /// <summary>Bộ đọc không đọc được gói hợp lệ (lỗi của ToolQL hoặc giao thức đổi). Không thử lại.</summary>
    Faulted,
    Stopped,
}

public sealed class SessionOptions
{
    /// <summary>Ghi/bắt gói (ví dụ <c>CaptureSink.Record</c>, tự che gói đăng nhập). Gọi trên luồng của phiên.</summary>
    public Action<PacketDirection, Packet>? Tap { get; init; }

    /// <summary>Kho lưu các bảng dữ liệu nhận được; null thì không lưu.</summary>
    public DataCache? Cache { get; init; }

    public Backoff Backoff { get; init; } = new();

    /// <summary>
    /// Chờ tối đa bao lâu sau khi gửi đăng nhập để nhận phiên bản dữ liệu (-28 sub 4). Hết hạn thì coi như bị từ chối
    /// (server im lặng sau đăng nhập, thường do đăng nhập lại quá sớm) và dừng, không thử lại.
    /// <see cref="Timeout.InfiniteTimeSpan"/> để tắt.
    /// </summary>
    public TimeSpan LoginTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Chờ trước khi nối lại; trả false nếu bị hủy. Test thay bằng hàm không chờ.</summary>
    public Func<TimeSpan, CancellationToken, bool> Wait { get; init; } =
        (delay, cancel) => !cancel.WaitHandle.WaitOne(delay);
}

/// <summary>
/// Một acc: một luồng riêng (không dùng thread pool) giữ kết nối, đăng nhập, đọc gói và đưa cho người nghe.
/// Mất mạng thì nối lại với thời gian chờ tăng dần; bị từ chối, captcha, thiếu nhân vật hay lỗi bộ đọc thì dừng,
/// không tự thử lại.
/// </summary>
public sealed class AccountSession : IDisposable
{
    private readonly AccountCredentials _credentials;
    private readonly Func<CancellationToken, Stream> _connect;
    private readonly SessionOptions _options;
    private readonly CancellationTokenSource _cancel = new();
    private readonly object _gate = new();
    private SessionState _state = SessionState.Idle;
    private string? _message;
    private Stream? _stream;
    private PacketWriter? _writer;
    private Thread? _thread;

    public AccountSession(AccountCredentials credentials, Func<CancellationToken, Stream> connect, SessionOptions? options = null)
    {
        _credentials = credentials;
        _connect = connect;
        _options = options ?? new SessionOptions();
    }

    /// <summary>Trạng thái game của acc. Chỉ luồng phiên ghi; luồng khác dùng <see cref="GameState.Snapshot"/>.</summary>
    public GameState Game { get; } = new();

    public SessionState State { get { lock (_gate) return _state; } }

    /// <summary>Thông báo kèm trạng thái gần nhất (lý do từ chối, lỗi mạng ...). Không chứa mật khẩu.</summary>
    public string? Message { get { lock (_gate) return _message; } }

    /// <summary>Đổi trạng thái. Gọi trên luồng của phiên (hoặc luồng gọi <see cref="Stop"/>).</summary>
    public event Action<SessionState, string?>? StateChanged;

    /// <summary>Mỗi gói nhận được SAU khi máy trạng thái đăng nhập đã xử lý. Gọi trên luồng của phiên; không được chặn lâu.</summary>
    public event Action<Packet>? PacketReceived;

    public void Start()
    {
        lock (_gate)
        {
            if (_thread != null) throw new InvalidOperationException("Phiên đã được Start.");
            _thread = new Thread(Run) { IsBackground = true, Name = "ToolQL.Session:" + _credentials.User };
        }
        _thread.Start();
    }

    /// <summary>Gửi một gói (từ luồng khác). False nếu đang không có kết nối.</summary>
    public bool TrySend(sbyte command, byte[] payload)
    {
        PacketWriter? writer;
        lock (_gate) writer = _writer;
        if (writer is null) return false;
        try
        {
            lock (writer) writer.Write(command, payload);
            _options.Tap?.Invoke(PacketDirection.ClientToServer, new Packet(command, payload));
            return true;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>Dừng phiên, đóng socket để mở khóa luồng đọc, chờ luồng kết thúc (tối đa 5 giây).</summary>
    public void Stop()
    {
        _cancel.Cancel();
        CloseStream();
        Thread? thread;
        lock (_gate) thread = _thread;
        if (thread != null && thread != Thread.CurrentThread && thread.IsAlive)
            thread.Join(TimeSpan.FromSeconds(5));
    }

    public void Dispose()
    {
        Stop();
        _cancel.Dispose();
    }

    private void Run()
    {
        while (!_cancel.IsCancellationRequested)
        {
            var (terminal, message) = RunOnce();
            if (terminal is { } state)
            {
                SetState(state, message);
                return;
            }

            SetState(SessionState.Reconnecting, message);
            if (!_options.Wait(_options.Backoff.Next(), _cancel.Token)) break;
        }
        SetState(SessionState.Stopped, null);
    }

    /// <summary>Một lần nối: trả trạng thái cuối nếu phải dừng hẳn, hoặc null nếu nên nối lại.</summary>
    private (SessionState? Terminal, string? Message) RunOnce()
    {
        Game.Reset();
        SetState(SessionState.Connecting, null);
        Stream? stream = null;
        Timer? watchdog = null;
        bool loginTimedOut = false;
        try
        {
            stream = _connect(_cancel.Token);
            var writer = new PacketWriter(stream);
            var reader = new PacketReader(stream);
            lock (_gate)
            {
                _stream = stream;
                _writer = writer;
            }
            if (_cancel.IsCancellationRequested) return (SessionState.Stopped, null);

            KeyHandshake.Perform(writer, reader, _options.Tap);

            var flow = new LoginFlow(_credentials);
            var recorder = _options.Cache is { } cache ? new TableRecorder(cache) : null;
            foreach (var packet in flow.Start()) SendFlow(writer, packet);
            SetState(SessionState.LoggingIn, null);
            watchdog = new Timer(_ =>
            {
                loginTimedOut = true;
                CloseStream();   // mở khóa luồng đọc đang chờ
            }, null, _options.LoginTimeout, Timeout.InfiniteTimeSpan);

            var shown = LoginPhase.LoggingIn;
            while (true)
            {
                var received = reader.Read();
                _options.Tap?.Invoke(PacketDirection.ServerToClient, received);

                FlowStep step;
                try
                {
                    step = flow.OnPacket(received);
                }
                catch (InvalidDataException ex)
                {
                    return (SessionState.Faulted, $"Không đọc được gói {received.Command}: {ex.Message}");
                }
                foreach (var packet in step.Send) SendFlow(writer, packet);

                switch (step.Outcome)
                {
                    case FlowOutcome.NeedCaptcha: return (SessionState.NeedCaptcha, step.Message);
                    case FlowOutcome.NeedCharacter: return (SessionState.NeedCharacter, step.Message);
                    case FlowOutcome.Rejected: return (SessionState.Rejected, step.Message);
                }

                // Áp vào trạng thái game TRƯỚC khi báo InGame, để người nghe thấy InGame là đã có map và lưới.
                Game.Apply(received);
                recorder?.Record(received, flow.Versions);

                if (flow.Phase != shown)
                {
                    shown = flow.Phase;
                    watchdog.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    if (shown == LoginPhase.InGame) _options.Backoff.Reset();
                    SetState(shown == LoginPhase.Loading ? SessionState.Loading : SessionState.InGame, null);
                }

                RaisePacket(received);
            }
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or SocketException
                                       or ObjectDisposedException or InvalidDataException or OperationCanceledException)
        {
            if (_cancel.IsCancellationRequested) return (SessionState.Stopped, null);
            if (loginTimedOut)
                return (SessionState.Rejected, $"Server không trả lời đăng nhập sau {_options.LoginTimeout.TotalSeconds:0} giây.");
            // Ngắt khi đang đăng nhập (trước khi nhận -28 sub 4) coi như bị từ chối: không thử lại (rule CLAUDE.md).
            if (State == SessionState.LoggingIn)
                return (SessionState.Rejected, "Server ngắt kết nối khi đăng nhập: " + ex.Message);
            return (null, "Mất kết nối: " + ex.Message);
        }
        finally
        {
            watchdog?.Dispose();
            lock (_gate)
            {
                _stream = null;
                _writer = null;
            }
            stream?.Dispose();
        }
    }

    private void SendFlow(PacketWriter writer, Packet packet)
    {
        lock (writer) writer.Write(packet.Command, packet.Payload);
        _options.Tap?.Invoke(PacketDirection.ClientToServer, packet);
    }

    private void RaisePacket(Packet packet)
    {
        try
        {
            PacketReceived?.Invoke(packet);
        }
        catch (Exception ex)
        {
            // Lỗi của người nghe không được làm sập luồng phiên (sẽ kéo sập cả tiến trình).
            lock (_gate) _message = "Lỗi xử lý gói " + packet.Command + ": " + ex.Message;
        }
    }

    private void SetState(SessionState state, string? message)
    {
        lock (_gate)
        {
            _state = state;
            _message = message;
        }
        try
        {
            StateChanged?.Invoke(state, message);
        }
        catch (Exception ex)
        {
            lock (_gate) _message = "Lỗi người nghe trạng thái: " + ex.Message;
        }
    }

    private void CloseStream()
    {
        Stream? stream;
        lock (_gate) stream = _stream;
        try
        {
            stream?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
        }
    }
}
