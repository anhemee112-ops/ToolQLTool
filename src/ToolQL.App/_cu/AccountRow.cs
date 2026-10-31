using System.ComponentModel;
using System.Windows.Media;
using ToolQL.Core;

namespace ToolQL.App;

/// <summary>Một dòng trong danh sách acc. Chỉ giữ dữ liệu hiển thị; không giữ mật khẩu.</summary>
public sealed class AccountRow : INotifyPropertyChanged
{
    private static readonly Brush Green = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x9E, 0x4A)));
    private static readonly Brush Orange = Freeze(new SolidColorBrush(Color.FromRgb(0xD9, 0x82, 0x0B)));
    private static readonly Brush Red = Freeze(new SolidColorBrush(Color.FromRgb(0xD3, 0x3B, 0x3B)));
    private static readonly Brush Gray = Freeze(new SolidColorBrush(Color.FromRgb(0x6B, 0x77, 0x85)));

    private SessionState _state = SessionState.Idle;
    private string? _message;

    public AccountRow(Guid id, string user, string server, string note, bool isDemo = false)
    {
        Id = id;
        User = user;
        Server = server;
        Note = note;
        IsDemo = isDemo;
    }

    public Guid Id { get; }
    public string User { get; }
    public string Server { get; private set; }
    public string Note { get; private set; }

    /// <summary>Dòng demo (phát lại tệp bắt gói): không sửa, không xóa, không nối mạng.</summary>
    public bool IsDemo { get; }

    public SessionState State => _state;
    public string StateText => SessionStateText.For(_state);
    public string? Message => _message;

    public Brush StateBrush => _state switch
    {
        SessionState.InGame => Green,
        SessionState.Connecting or SessionState.LoggingIn or SessionState.Loading or SessionState.Reconnecting => Orange,
        SessionState.NeedCaptcha or SessionState.NeedCharacter or SessionState.Rejected or SessionState.Faulted => Red,
        _ => Gray,
    };

    public void SetState(SessionState state, string? message)
    {
        _state = state;
        _message = message;
        Raise(nameof(State)); Raise(nameof(StateText)); Raise(nameof(StateBrush)); Raise(nameof(Message));
    }

    public void SetDetails(string server, string note)
    {
        Server = server;
        Note = note;
        Raise(nameof(Server)); Raise(nameof(Note));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
