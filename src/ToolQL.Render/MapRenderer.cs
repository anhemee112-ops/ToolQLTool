using SkiaSharp;
using ToolQL.Core;

namespace ToolQL.Render;

/// <summary>
/// Vẽ ảnh của map từ <see cref="GameSnapshot"/> (bất biến): lưới ô lát thật (từ <see cref="TileSet"/>) rồi các thực thể.
/// Không truyền <see cref="Viewport"/> thì vẽ CẢ map; truyền thì vẽ một khung nhìn theo nhân vật (<see cref="Camera"/>), chỉ tô các ô
/// nằm trong khung. Thực thể (quái, NPC, người chơi, mình) hiện chỉ là KÝ HIỆU (khung màu + tên), chưa phải sprite: cần ghép
/// sprite theo bảng "trạng thái → khung" mà ta chưa có. Chưa vẽ nền trời, vật trang trí, nước, cờ ô lát (bit "không vẽ"),
/// vật rơi, cổng. Gốc tọa độ ở góc trên trái map, ô lát cạnh 24 điểm ảnh.
/// </summary>
public static class MapRenderer
{
    public static readonly SKColor BackgroundColor = new(0x6E, 0xC1, 0xE4);

    private const int DefaultWidth = 320, DefaultHeight = 240;
    private const int MarkerWidth = 20, MarkerHeight = 32;

    public static SKBitmap Render(GameSnapshot snapshot, TileSet? tiles, Viewport? viewport = null)
    {
        var map = snapshot.Map;
        int cols = map?.Width ?? 0, rows = map?.Height ?? 0;

        int width, height;
        var camera = new CameraPosition(0, 0);
        if (viewport != null)
        {
            width = viewport.Width;
            height = viewport.Height;
            if (cols > 0 && rows > 0)
            {
                // Chưa có nhân vật thì nhìn vào giữa map.
                int footX = snapshot.Me?.X ?? cols * TileSet.Size / 2;
                int footY = snapshot.Me?.Y ?? rows * TileSet.Size / 2;
                camera = Camera.Follow(footX, footY, facing: 0, width, height, cols, rows);
            }
        }
        else
        {
            width = cols > 0 ? cols * TileSet.Size : DefaultWidth;
            height = rows > 0 ? rows * TileSet.Size : DefaultHeight;
        }

        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(BackgroundColor);
        canvas.Translate(-camera.X, -camera.Y);

        if (map?.Tiles is { } grid && tiles != null && grid.Length >= cols * rows)
            DrawTiles(canvas, grid, cols, rows, tiles, camera, width, height);

        DrawEntities(canvas, snapshot);
        return bitmap;
    }

    private static void DrawTiles(SKCanvas canvas, byte[] grid, int cols, int rows, TileSet tiles, CameraPosition camera, int viewWidth, int viewHeight)
    {
        // Chỉ các cột/hàng giao với khung nhìn.
        int firstCol = Math.Max(0, FloorDiv(camera.X, TileSet.Size));
        int lastCol = Math.Min(cols - 1, FloorDiv(camera.X + viewWidth - 1, TileSet.Size));
        int firstRow = Math.Max(0, FloorDiv(camera.Y, TileSet.Size));
        int lastRow = Math.Min(rows - 1, FloorDiv(camera.Y + viewHeight - 1, TileSet.Size));

        using var paint = new SKPaint { FilterQuality = SKFilterQuality.None };
        for (int row = firstRow; row <= lastRow; row++)
        {
            for (int col = firstCol; col <= lastCol; col++)
            {
                if (!tiles.TryGetSource(grid[row * cols + col], out var source)) continue;
                canvas.DrawBitmap(tiles.Strip, source, new SKRect(col * TileSet.Size, row * TileSet.Size, (col + 1) * TileSet.Size, (row + 1) * TileSet.Size), paint);
            }
        }
    }

    private static int FloorDiv(int a, int b) => (int)Math.Floor(a / (double)b);

    private static void DrawEntities(SKCanvas canvas, GameSnapshot s)
    {
        foreach (var npc in s.Map?.Npcs ?? Array.Empty<ToolQL.Protocol.Messages.Npc>())
            Marker(canvas, npc.X, npc.Y, new SKColor(80, 80, 255), $"NPC {npc.Template}");
        foreach (var mob in s.Mobs)
            if (mob.Alive) Marker(canvas, mob.Info.X, mob.Info.Y, new SKColor(255, 60, 60), $"quái {mob.Info.Type} lv{mob.Info.Level}");
        foreach (var player in s.Players)
            Marker(canvas, player.X, player.Y, new SKColor(60, 200, 60), player.Info.Name);
        if (s.Me is { } me)
            Marker(canvas, me.X, me.Y, new SKColor(255, 220, 0), me.Name);
    }

    /// <summary>Khung màu đứng trên điểm <c>(x, y)</c> (giả định đó là vị trí chân; chưa đối chiếu với ảnh chụp) kèm tên.</summary>
    private static void Marker(SKCanvas canvas, float x, float y, SKColor color, string label)
    {
        using var fill = new SKPaint { Color = color.WithAlpha(150), IsAntialias = true };
        using var stroke = new SKPaint { Color = color, IsStroke = true, StrokeWidth = 1.5f, IsAntialias = true };
        var rect = new SKRect(x - MarkerWidth / 2f, y - MarkerHeight, x + MarkerWidth / 2f, y);
        canvas.DrawRect(rect, fill);
        canvas.DrawRect(rect, stroke);

        using var text = new SKPaint { Color = SKColors.Black, IsAntialias = true, TextSize = 10, TextAlign = SKTextAlign.Center };
        canvas.DrawText(label, x, y - MarkerHeight - 3, text);
    }
}
