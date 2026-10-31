namespace ToolQL.Core;

/// <summary>Khung ảnh (vùng client của cửa sổ client) để nhận dạng popup. Tách khỏi Windows để test được.</summary>
public interface IPixelFrame
{
    int Width { get; }
    int Height { get; }
    void Read(int x, int y, out byte r, out byte g, out byte b);
}

/// <summary>Khung ảnh BGRA 32 bit (đúng định dạng <c>GetDIBits</c> trả về), mỗi hàng dài <c>stride</c> byte.</summary>
public sealed class BgraFrame : IPixelFrame
{
    private readonly byte[] _data;
    private readonly int _stride;
    private readonly int _originX, _originY;

    /// <param name="originX">Cột của điểm ảnh đầu tiên của khung trong <paramref name="data"/> (cắt bỏ viền/thanh tiêu đề của cửa sổ).</param>
    public BgraFrame(int width, int height, int stride, byte[] data, int originX = 0, int originY = 0)
    {
        Width = width;
        Height = height;
        _stride = stride;
        _data = data;
        _originX = originX;
        _originY = originY;
    }

    public int Width { get; }
    public int Height { get; }

    public void Read(int x, int y, out byte r, out byte g, out byte b)
    {
        int i = (_originY + y) * _stride + (_originX + x) * 4;
        b = _data[i];
        g = _data[i + 1];
        r = _data[i + 2];
    }
}

/// <summary>
/// Nhận ra popup "Đã có bản cập nhật mới, bạn có muốn cập nhật không? [Có] [Không]" mà client hiện sau khi vào game (đo trên cửa sổ thật
/// 2026-10-31 trên bản AngelAChip gốc, vùng client 240×365 gồm 22 px thanh menu ở trên; đã trừ 22 px cho vùng client 240×320 của bản gọn,
/// là bản duy nhất ToolQL dùng): hộp thoại nền TRẮNG nằm sát đáy và hai nút CAM, "Có" ở x 46-111 và "Không" ở x 132-197, cùng y 288-307.
/// Phải có đủ ba dấu hiệu (nền trắng, nút cam trái, nút cam phải) mới tính, nên màn hình toàn trắng/toàn cam/menu không bị nhận nhầm.
/// Cũng khớp mọi hộp thoại Có/Không cùng bố cục; bấm "Không" ở các hộp đó là lựa chọn an toàn (hủy). Tọa độ tính theo tỉ lệ nên
/// còn đúng khi khung client phóng to; mới đo ở kích thước màn hình game 240×320.
/// </summary>
public static class UpdatePromptDetector
{
    private const double RefWidth = 240, RefHeight = 320;
    private const double MinOrange = 0.60, MinWhite = 0.50;

    /// <summary>Điểm bấm nút "Không" theo tỉ lệ của vùng client (tâm nút ≈ (165, 298) trên 240×320).</summary>
    public const double NoButtonX = 165.0 / 240, NoButtonY = 298.0 / 320;

    public static bool IsPresent(IPixelFrame frame)
    {
        if (frame.Width < 60 || frame.Height < 100) return false;
        return Fraction(frame, 20, 254, 226, 284, IsWhite) >= MinWhite
            && Fraction(frame, 50, 290, 108, 305, IsOrange) >= MinOrange
            && Fraction(frame, 136, 290, 194, 305, IsOrange) >= MinOrange;
    }

    private static bool IsWhite(byte r, byte g, byte b) => r >= 220 && g >= 220 && b >= 220;

    private static bool IsOrange(byte r, byte g, byte b) => r >= 190 && g >= 80 && g <= 170 && b <= 90 && r > g + 50;

    /// <summary>Tỉ lệ điểm ảnh thỏa <paramref name="test"/> trong hình chữ nhật (tọa độ tham chiếu 240×320, phóng theo kích thước khung).</summary>
    private static double Fraction(IPixelFrame f, int x0, int y0, int x1, int y1, Func<byte, byte, byte, bool> test)
    {
        int sx0 = Math.Clamp((int)(x0 / RefWidth * f.Width), 0, f.Width - 1), sx1 = Math.Clamp((int)(x1 / RefWidth * f.Width), 0, f.Width - 1);
        int sy0 = Math.Clamp((int)(y0 / RefHeight * f.Height), 0, f.Height - 1), sy1 = Math.Clamp((int)(y1 / RefHeight * f.Height), 0, f.Height - 1);
        int total = 0, hit = 0;
        for (int y = sy0; y <= sy1; y++)
        {
            for (int x = sx0; x <= sx1; x++)
            {
                f.Read(x, y, out byte r, out byte g, out byte b);
                total++;
                if (test(r, g, b)) hit++;
            }
        }
        return total == 0 ? 0 : (double)hit / total;
    }
}

/// <summary>
/// Quyết định khi nào chụp cửa sổ client để tìm popup cập nhật và khi nào bấm "Không". Popup hiện khoảng 30-40 giây sau khi mở acc,
/// nên canh trong khoảng [mở + <c>startAfter</c>, mở + <c>watchFor</c>) của mỗi lần chạy. Phải thấy popup <c>confirmations</c> lần liên tiếp
/// mới bấm (tránh khung hình thoáng qua); bấm xong mà popup còn thì thử lại sau ít nhất 5 giây, tối đa 3 lần; popup biến mất sau khi bấm thì xong.
/// Chỉ gọi từ luồng giao diện.
/// </summary>
public sealed class PopupGuard
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan RetryGap = TimeSpan.FromSeconds(5);

    private sealed class State
    {
        public int Consecutive;
        public int Attempts;
        public DateTimeOffset? LastClick;
        public bool Done;
    }

    private readonly TimeSpan _startAfter;
    private readonly TimeSpan _watchFor;
    private readonly int _confirmations;
    private readonly Dictionary<(Guid, DateTimeOffset), State> _states = new();

    public PopupGuard(TimeSpan startAfter, TimeSpan watchFor, int confirmations = 2)
    {
        _startAfter = startAfter;
        _watchFor = watchFor;
        _confirmations = confirmations;
    }

    /// <summary>Các acc cần chụp ảnh trong lần gọi này. Cũng dọn trạng thái của các lần chạy đã kết thúc.</summary>
    public IReadOnlyList<Guid> Watching(IEnumerable<(Guid Id, DateTimeOffset StartedAt)> running, DateTimeOffset now)
    {
        var list = running.ToList();
        foreach (var key in _states.Keys.Where(k => !list.Contains(k)).ToList()) _states.Remove(key);
        return list
            .Where(r => now - r.StartedAt >= _startAfter && now - r.StartedAt < _watchFor && !(_states.TryGetValue(r, out var s) && s.Done))
            .Select(r => r.Id)
            .ToList();
    }

    /// <summary>Ghi kết quả một lần chụp; true nghĩa là phải bấm "Không" ngay bây giờ.</summary>
    public bool Observe(Guid id, DateTimeOffset startedAt, DateTimeOffset now, bool popupPresent)
    {
        if (!_states.TryGetValue((id, startedAt), out var s)) _states[(id, startedAt)] = s = new State();
        if (s.Done) return false;

        if (!popupPresent)
        {
            s.Consecutive = 0;
            if (s.Attempts > 0) s.Done = true;   // đã bấm mà popup đã biến mất
            return false;
        }

        s.Consecutive++;
        if (s.Consecutive < _confirmations) return false;
        if (s.Attempts >= MaxAttempts)
        {
            s.Done = true;                       // bấm 3 lần không được: bỏ cuộc, tránh bấm bậy mãi
            return false;
        }
        if (s.LastClick is { } last && now - last < RetryGap) return false;

        s.Attempts++;
        s.LastClick = now;
        s.Consecutive = 0;
        return true;
    }
}
