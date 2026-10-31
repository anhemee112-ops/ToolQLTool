using System.Globalization;

namespace ToolQL.Core;

/// <summary>
/// Cho biết tiến trình client có kết nối TCP đang mở tới server game hay không. Dùng để phát hiện MẤT KẾT NỐI khi cửa sổ client còn
/// sống (client không báo gì ra ngoài). true: có; false: không; null: không xác định được (lỗi đọc bảng kết nối).
/// </summary>
public interface IConnectionProbe
{
    bool? IsConnected(int processId);
}

/// <summary>
/// Theo dõi kết nối của MỘT client. Chỉ coi là MẤT KẾT NỐI khi trước đó kết nối đã ỔN ĐỊNH (liên tục <c>stableAfter</c>, tức đã vào game)
/// rồi biến mất liên tục <c>lostAfter</c>. Nhờ vậy lúc client mới mở (nối thử vài lần rồi ngắt), sai mật khẩu hay chưa đăng nhập được
/// không bị tính là "rớt mạng" (để tránh vòng lặp đăng nhập lại liên tục làm khóa acc). Kết quả "không xác định" bị bỏ qua.
/// </summary>
public sealed class ConnectionWatcher
{
    private readonly TimeSpan _stableAfter;
    private readonly TimeSpan _lostAfter;
    private DateTimeOffset? _connectedSince;
    private DateTimeOffset? _absentSince;
    private bool _reported;

    public ConnectionWatcher(TimeSpan stableAfter, TimeSpan lostAfter)
    {
        _stableAfter = stableAfter;
        _lostAfter = lostAfter;
    }

    /// <summary>Lần quan sát gần nhất có kết nối hay không; null nếu chưa quan sát được lần nào.</summary>
    public bool? Connected { get; private set; }

    /// <summary>Đã từng có kết nối liên tục đủ lâu (coi là đã vào game).</summary>
    public bool WasStable { get; private set; }

    /// <summary>Ghi một lần quan sát; trả true đúng MỘT lần khi vừa xác định là mất kết nối.</summary>
    public bool Observe(DateTimeOffset now, bool? connected)
    {
        if (connected is null) return false;
        Connected = connected;
        if (connected == true)
        {
            _absentSince = null;
            _connectedSince ??= now;
            if (now - _connectedSince >= _stableAfter) WasStable = true;
            return false;
        }

        _connectedSince = null;
        _absentSince ??= now;
        if (WasStable && !_reported && now - _absentSince >= _lostAfter)
        {
            _reported = true;
            return true;
        }
        return false;
    }
}

/// <summary>
/// Khung giờ bảo trì hằng ngày của game (theo giờ Việt Nam, UTC+7, kể cả khi máy chạy ở múi giờ khác): trong khung này ToolQL đóng mọi
/// client và không tự mở lại; hết khung thì mở lại đúng những acc đã bị đóng. Hỗ trợ khung qua nửa đêm. ToolQL KHÔNG đọc được
/// thông báo bảo trì trong game; chỉ theo giờ cài đặt.
/// </summary>
public sealed class MaintenanceSchedule
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private readonly bool _enabled;
    private readonly TimeSpan _start, _end;

    private MaintenanceSchedule(bool enabled, TimeSpan start, TimeSpan end)
    {
        _enabled = enabled;
        _start = start;
        _end = end;
    }

    /// <summary>Tạo từ cài đặt; null nếu giờ sai định dạng (HH:mm, 0-23 giờ).</summary>
    public static MaintenanceSchedule? Create(bool enabled, string start, string end) =>
        TryParse(start, out var s) && TryParse(end, out var e) ? new MaintenanceSchedule(enabled, s, e) : null;

    public bool IsMaintenance(DateTimeOffset now)
    {
        if (!_enabled || _start == _end) return false;
        var t = now.ToOffset(VietnamOffset).TimeOfDay;
        return _start < _end ? t >= _start && t < _end : t >= _start || t < _end;
    }

    public string Describe() => $"{Format(_start)}-{Format(_end)}";

    private static string Format(TimeSpan t) => $"{t.Hours:00}:{t.Minutes:00}";

    private static bool TryParse(string text, out TimeSpan value)
    {
        value = default;
        if (!TimeSpan.TryParseExact(text.Trim(), new[] { "h\\:mm", "hh\\:mm" }, CultureInfo.InvariantCulture, out value)) return false;
        return value >= TimeSpan.Zero && value < TimeSpan.FromHours(24);
    }
}
