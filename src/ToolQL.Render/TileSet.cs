using SkiaSharp;

namespace ToolQL.Render;

/// <summary>
/// Bộ ô lát: dải DỌC rộng 24, mỗi ô 24×24 xếp từ trên xuống. Mã ô trong lưới tính từ 1 (0 là rỗng): ô mã <c>n</c> nằm ở
/// <c>y = (n - 1) * 24</c>. Kiểm trên map thật: mã lớn nhất 26 = số ô của dải <c>t/9.png</c> (cao 624).
/// </summary>
public sealed class TileSet : IDisposable
{
    public const int Size = 24;

    private TileSet(SKBitmap strip) => Strip = strip;

    public SKBitmap Strip { get; }

    public int Count => Strip.Height / Size;

    public static TileSet FromPng(byte[] png)
    {
        var bitmap = SKBitmap.Decode(png) ?? throw new InvalidDataException("Không giải mã được ảnh bộ ô lát.");
        return new TileSet(bitmap);
    }

    /// <summary>Vùng của ô mã <paramref name="id"/> trong dải; false nếu mã là 0, âm hoặc vượt số ô.</summary>
    public bool TryGetSource(int id, out SKRectI source)
    {
        if (id < 1 || id > Count)
        {
            source = default;
            return false;
        }
        int y = (id - 1) * Size;
        source = new SKRectI(0, y, Strip.Width, y + Size);
        return true;
    }

    public void Dispose() => Strip.Dispose();
}
