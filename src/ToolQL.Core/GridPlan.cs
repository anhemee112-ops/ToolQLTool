namespace ToolQL.Core;

/// <summary>
/// Chọn và bố trí các ô của lưới xem nhiều acc. Lưới chỉ có acc đang "Trong game" (acc khác không có hình để xem), lấy theo
/// thứ tự danh sách, tối đa <see cref="MaxCells"/> (spec v0.1: thường 3-4 acc cùng lúc). Acc không nằm trong lưới thì không
/// bị tạo snapshot hay vẽ.
/// </summary>
public static class GridPlan
{
    public const int MaxCells = 4;

    public static IReadOnlyList<Guid> Choose(IEnumerable<(Guid Id, SessionState State)> accounts, int max = MaxCells) =>
        accounts.Where(a => a.State == SessionState.InGame).Take(max).Select(a => a.Id).ToArray();

    /// <summary>Số cột và số hàng cho <paramref name="count"/> ô: 1 ô một mình, 2 ô cạnh nhau, 3-4 ô thành 2×2.</summary>
    public static (int Columns, int Rows) Layout(int count) => count switch
    {
        <= 0 => (0, 0),
        1 => (1, 1),
        2 => (2, 1),
        _ => (2, 2),
    };
}
