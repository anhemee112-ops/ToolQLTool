using System.ComponentModel;
using System.Windows.Media;
using ToolQL.Core;

namespace ToolQL.App;

/// <summary>Một dòng của bảng acc (kiểu Tool 2: STT, TK, SV, Ghi chú, Status, Hiện, ✓). Không giữ mật khẩu.</summary>
public sealed class AccountRow : INotifyPropertyChanged
{
    private static readonly Brush Green = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x9E, 0x4A)));
    private static readonly Brush Orange = Freeze(new SolidColorBrush(Color.FromRgb(0xD9, 0x82, 0x0B)));
    private static readonly Brush Red = Freeze(new SolidColorBrush(Color.FromRgb(0xD3, 0x3B, 0x3B)));
    private static readonly Brush Gray = Freeze(new SolidColorBrush(Color.FromRgb(0x6B, 0x77, 0x85)));

    private int _index;
    private bool _tick;
    private ClientState _state = ClientState.NotOpened;
    private string _stateText = "Chưa mở";
    private string? _error;
    private int? _pid;
    private bool? _connected;

    public AccountRow(Guid id, string user, string server, string note, bool tick)
    {
        Id = id;
        User = user;
        Server = server;
        Note = note;
        _tick = tick;
    }

    /// <summary>Che tên tài khoản trên màn hình (khi chụp ảnh, quay video): menu View → Ẩn tên tài khoản.</summary>
    public static bool HideNames { get; set; }

    public Guid Id { get; }
    public string User { get; }
    public string Server { get; private set; }
    public string Note { get; private set; }

    /// <summary>Số server (cột SV), bắt đầu từ 1; 0 nếu server không có trong danh sách.</summary>
    public int ServerNumber => ToolQL.Protocol.ServerList.All.ToList().FindIndex(s => s.Name == Server) + 1;

    public int Index { get => _index; set => Set(ref _index, value, nameof(Index)); }

    /// <summary>Dấu ✓ Auto login. Đổi từ bảng thì <see cref="TickChanged"/> báo để cửa sổ lưu xuống kho.</summary>
    public bool Tick
    {
        get => _tick;
        set
        {
            if (_tick == value) return;
            _tick = value;
            Raise(nameof(Tick));
            TickChanged?.Invoke(this);
        }
    }

    public event Action<AccountRow>? TickChanged;

    public ClientState State => _state;
    public string StateText => _stateText;
    public string? Error => _error;
    public int? Pid => _pid;

    public string UserDisplay => HideNames ? (User.Length <= 2 ? "***" : User[..2] + new string('*', Math.Min(User.Length - 2, 6))) : User;

    public Brush StateBrush => _state switch
    {
        ClientState.Running => Green,
        ClientState.Queued => Orange,
        ClientState.Exited when _error != null => Red,
        _ => Gray,
    };

    /// <summary>Đọc lại trạng thái từ bộ quản lý client. Gọi mỗi giây.</summary>
    public void Refresh(ClientHub hub, DateTimeOffset now)
    {
        _state = hub.StateOf(Id);
        _error = hub.ErrorOf(Id);
        _pid = hub.ProcessIdOf(Id);
        _connected = hub.ConnectedOf(Id);
        string text = _state switch
        {
            ClientState.Queued => "Đang chờ mở",
            ClientState.Running => RunningText(hub, now),
            ClientState.Exited => hub.EndOf(Id) switch
            {
                ClientEnd.Failed => "Lỗi: " + Shorten(_error ?? "không mở được", 60),
                ClientEnd.User => "Đã đóng",
                ClientEnd.Maintenance => "Bảo trì · sẽ mở lại sau khung giờ",
                _ => LostText(hub),
            },
            _ => "Chưa mở",
        };
        if (text != _stateText)
        {
            _stateText = text;
            Raise(nameof(StateText));
        }
        Raise(nameof(State));
        Raise(nameof(StateBrush));
        Raise(nameof(Pid));
        Raise(nameof(Error));
    }

    private string RunningText(ClientHub hub, DateTimeOffset now)
    {
        string elapsed = Elapsed(now - (hub.StartedAtOf(Id) ?? now));
        if (hub.ConnectedOf(Id) is null) return "Đang chạy · " + elapsed;                    // chưa dò được kết nối
        if (!hub.StableConnectionOf(Id)) return "Đang kết nối · " + elapsed;                            // chưa có kết nối ổn định
        // Kết nối ổn định chưa chứng minh đã đăng nhập: client giữ kết nối tới server ngay từ menu (đã thấy ở chế độ không đăng nhập).
        return (_connected == true ? "Đã kết nối · " : "Mất kết nối? · ") + elapsed;
    }

    private string LostText(ClientHub hub)
    {
        if (hub.AutoRetryPausedOf(Id)) return $"Mất kết nối {ClientHub.MaxConsecutiveLosses} lần liền · dừng tự mở lại (bấm Login để thử)";
        var wait = hub.WaitLeftOf(Id);
        return wait > TimeSpan.Zero ? $"Mất kết nối · chờ {Math.Ceiling(wait.TotalSeconds):0}s" : "Mất kết nối";
    }

    /// <summary>Lần dò gần nhất: có kết nối TCP tới server game không (null: chưa dò được).</summary>
    public bool? Connected => _connected;

    public void SetDetails(string server, string note)
    {
        Server = server;
        Note = note;
        Raise(nameof(Server)); Raise(nameof(ServerNumber)); Raise(nameof(Note));
    }

    public void RaiseUserDisplay() => Raise(nameof(UserDisplay));

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, string name)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Raise(name);
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static string Elapsed(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";

    private static string Shorten(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
