using SkiaSharp;
using ToolQL.Core;

namespace ToolQL.Render;

/// <summary>Bộ ô lát đã giải mã theo id, lấy từ jar của client. Id không có trong jar nhớ là null (không tìm lại). Sở hữu và đóng jar.</summary>
public sealed class TileCache : IDisposable
{
    private readonly ClientAssets _assets;
    private readonly Dictionary<int, TileSet?> _sets = new();

    public TileCache(ClientAssets assets) => _assets = assets;

    public TileSet? Get(int id)
    {
        if (!_sets.TryGetValue(id, out var set)) _sets[id] = set = _assets.LoadTileSet(id);
        return set;
    }

    public void Dispose()
    {
        foreach (var set in _sets.Values) set?.Dispose();
        _sets.Clear();
        _assets.Dispose();
    }
}

/// <summary>
/// Bộ vẽ cho khung xem MỘT acc: chỉ vẽ khi <see cref="GameSnapshot.Version"/> khác lần vẽ trước, nên trạng thái không đổi
/// thì không tốn CPU. <see cref="DrawCount"/> đếm số lần vẽ (kiểm "ngừng xem thì không vẽ"). Không an toàn đa luồng:
/// gọi từ luồng giao diện. Đổi acc đang xem thì tạo bộ vẽ mới hoặc gọi <see cref="Invalidate"/>
/// (phiên bản của hai acc có thể trùng số).
/// </summary>
public sealed class MapViewer : IDisposable
{
    private readonly TileCache? _tiles;
    private readonly Viewport? _viewport;
    private long? _lastVersion;

    private readonly bool _ownsTiles;

    /// <param name="viewport">Khung nhìn theo nhân vật; null thì vẽ cả map.</param>
    /// <param name="ownsTiles">True (mặc định): đóng <paramref name="tiles"/> khi bộ vẽ bị hủy. False khi nhiều bộ vẽ dùng chung một bộ ô lát.</param>
    public MapViewer(TileCache? tiles, Viewport? viewport = null, bool ownsTiles = true)
    {
        _tiles = tiles;
        _viewport = viewport;
        _ownsTiles = ownsTiles;
    }

    public int DrawCount { get; private set; }

    /// <summary>Vẽ lại; trả null nếu trạng thái chưa đổi từ lần vẽ trước. Người gọi giải phóng bitmap trả về.</summary>
    public SKBitmap? Render(GameSnapshot snapshot)
    {
        if (_lastVersion == snapshot.Version) return null;
        _lastVersion = snapshot.Version;
        DrawCount++;
        var tileSet = snapshot.Map is { } map ? _tiles?.Get(map.TileSetId) : null;
        return MapRenderer.Render(snapshot, tileSet, _viewport);
    }

    /// <summary>Lần <see cref="Render"/> kế tiếp vẽ lại dù phiên bản không đổi.</summary>
    public void Invalidate() => _lastVersion = null;

    public void Dispose()
    {
        if (_ownsTiles) _tiles?.Dispose();
    }
}
