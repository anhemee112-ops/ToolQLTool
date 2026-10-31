namespace ToolQL.Core;

/// <summary>
/// Client chỉ đọc sẵn tài khoản từ RMS rồi hiện nút "Chơi TK: ..." và chờ người bấm (đã kiểm 2026-10-31); đăng nhập tự động nghĩa là
/// ToolQL phải bấm nút đó MỘT lần sau khi cửa sổ client mở và client nạp xong menu. Lớp này chỉ quyết định acc nào đến lúc bấm:
/// sau <c>delay</c> kể từ lúc mở, mỗi lần chạy bấm đúng một lần, mỗi lần gọi trả tối đa một acc (cái mở sớm nhất) để các cú bấm
/// (chuột thật, đưa cửa sổ ra trước) không chồng lên nhau. Chỉ gọi từ luồng giao diện.
/// </summary>
public sealed class LoginClickScheduler
{
    private readonly HashSet<(Guid Id, DateTimeOffset StartedAt)> _done = new();

    public LoginClickScheduler(TimeSpan delay) => Delay = delay;

    /// <summary>Chờ bao lâu sau khi mở client thì bấm. Đổi được khi người dùng sửa cài đặt (không làm mất dấu các acc đã bấm).</summary>
    public TimeSpan Delay { get; set; }

    public int TrackedCount => _done.Count;

    /// <summary>Acc đang chạy đã đến hạn bấm mà chưa bấm cho lần chạy này; null nếu không có. Cũng dọn các lần chạy đã kết thúc.</summary>
    public Guid? NextDue(IEnumerable<(Guid Id, DateTimeOffset StartedAt)> running, DateTimeOffset now)
    {
        var list = running.ToList();
        _done.RemoveWhere(d => !list.Contains(d));
        return list
            .Where(r => !_done.Contains(r) && now - r.StartedAt >= Delay)
            .OrderBy(r => r.StartedAt)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefault();
    }

    /// <summary>Đã bấm cho lần chạy bắt đầu lúc <paramref name="startedAt"/>.</summary>
    public void MarkDone(Guid id, DateTimeOffset startedAt) => _done.Add((id, startedAt));
}
