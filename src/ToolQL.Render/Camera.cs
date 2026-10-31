namespace ToolQL.Render;

/// <summary>Kích thước khung nhìn tính bằng điểm ảnh game (trước khi phóng to khi hiển thị).</summary>
public sealed record Viewport(int Width, int Height);

/// <summary>Góc trên trái của khung nhìn trong tọa độ thế giới (điểm ảnh). Có thể âm khi map hẹp hơn khung.</summary>
public readonly record struct CameraPosition(int X, int Y);

/// <summary>
/// Camera theo nhân vật, làm đúng thuật toán của client (<c>cu_0.l</c> trong jar, đọc từ mã dịch ngược):
/// <list type="bullet">
/// <item>x = chânX − rộng/2 + (rộng/6)·hướng; y = chânY − (cao·2/3) − 1 (chân nhân vật ở 2/3 chiều cao khung);</item>
/// <item>x kẹp vào [24, (cột − 1)·24 − rộng], y kẹp vào [0, (hàng − 1)·24 − cao]: cận trên được áp sau cận dưới như client,
/// nên map hẹp hơn khung cho giá trị âm. Cận 24 và (cột − 1) khiến hai cột ngoài cùng của map không bao giờ hiện ra.</item>
/// </list>
/// Client còn làm mượt chuyển động (mỗi khung đi khoảng 1/4 quãng đường tới đích); ở đây đặt thẳng vào đích.
/// Chưa đối chiếu với ảnh chụp thật; kích thước khung của ToolQL do ToolQL chọn, không phải kích thước của client.
/// </summary>
public static class Camera
{
    private const int TileSize = TileSet.Size;

    /// <param name="facing">Hướng nhìn của nhân vật: -1 trái, 0 chưa biết, 1 phải.</param>
    public static CameraPosition Follow(int footX, int footY, int facing, int viewWidth, int viewHeight, int cols, int rows)
    {
        int x = footX - viewWidth / 2 + viewWidth / 6 * facing;
        int y = footY - viewHeight * 2 / 3 - 1;

        int maxX = (cols - 1) * TileSize - viewWidth;
        int maxY = (rows - 1) * TileSize - viewHeight;
        if (x < TileSize) x = TileSize;
        if (x > maxX) x = maxX;
        if (y < 0) y = 0;
        if (y > maxY) y = maxY;
        return new CameraPosition(x, y);
    }
}
