using System.IO.Compression;
using SkiaSharp;
using ToolQL.Core;
using ToolQL.Protocol.Messages;
using ToolQL.Render;

namespace ToolQL.Render.Tests;

public class MapViewerTests
{
    private static GameSnapshot Snapshot(long version, int tileSetId = 9) => new(
        null,
        new MapState(1, "Thử", tileSetId, 2, 1, new byte[] { 1, 1 }, Array.Empty<Portal>(), Array.Empty<Npc>(), Array.Empty<MapBackground>()),
        Array.Empty<MobState>(), Array.Empty<DroppedItem>(), Array.Empty<PlayerState>(), new Dictionary<int, int>(), 0, version);

    private static byte[] StripPng(params SKColor[] colors)
    {
        using var bitmap = new SKBitmap(24, 24 * colors.Length);
        using var canvas = new SKCanvas(bitmap);
        for (int i = 0; i < colors.Length; i++)
        {
            using var paint = new SKPaint { Color = colors[i] };
            canvas.DrawRect(0, i * 24, 24, 24, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    private static string MakeJar(params (string Name, byte[] Data)[] entries)
    {
        string path = Path.Combine(Path.GetTempPath(), "toolql-viewer-" + Guid.NewGuid().ToString("N") + ".jar");
        using var stream = File.Create(path);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (name, data) in entries)
        {
            using var entry = zip.CreateEntry(name).Open();
            entry.Write(data);
        }
        return path;
    }

    [Fact]
    public void Chi_ve_lai_khi_trang_thai_da_doi_va_dem_so_lan_ve()
    {
        using var viewer = new MapViewer(tiles: null);
        Assert.Equal(0, viewer.DrawCount);

        using (var first = viewer.Render(Snapshot(1))) Assert.NotNull(first);
        Assert.Null(viewer.Render(Snapshot(1)));    // cùng phiên bản: không vẽ
        Assert.Null(viewer.Render(Snapshot(1)));
        Assert.Equal(1, viewer.DrawCount);

        using (var second = viewer.Render(Snapshot(2))) Assert.NotNull(second);
        Assert.Equal(2, viewer.DrawCount);
    }

    [Fact]
    public void Invalidate_buoc_ve_lai_du_phien_ban_khong_doi()
    {
        using var viewer = new MapViewer(tiles: null);
        using (viewer.Render(Snapshot(5))) { }
        Assert.Null(viewer.Render(Snapshot(5)));

        viewer.Invalidate();

        using var again = viewer.Render(Snapshot(5));
        Assert.NotNull(again);
        Assert.Equal(2, viewer.DrawCount);
    }

    [Fact]
    public void Chua_co_map_thi_van_ra_anh_trong_khong_nem_loi()
    {
        using var viewer = new MapViewer(tiles: null);
        var snapshot = Snapshot(1) with { Map = null };

        using var bitmap = viewer.Render(snapshot);

        Assert.NotNull(bitmap);
    }

    [Fact]
    public void Co_jar_thi_ve_o_lat_that_khong_co_jar_thi_chi_co_nen()
    {
        string jar = MakeJar(("x1/t/9.png", ClientImageCodec.Scramble(StripPng(new SKColor(255, 0, 0)))));
        try
        {
            using var withJar = new MapViewer(new TileCache(ClientAssets.OpenJar(jar)));
            using var without = new MapViewer(tiles: null);

            using var a = withJar.Render(Snapshot(1))!;
            using var b = without.Render(Snapshot(1))!;

            Assert.Equal(new SKColor(255, 0, 0), a.GetPixel(5, 5));
            Assert.Equal(MapRenderer.BackgroundColor, b.GetPixel(5, 5));
        }
        finally
        {
            File.Delete(jar);
        }
    }

    [Fact]
    public void Bo_o_lat_khong_co_trong_jar_thi_dung_none_va_chi_tim_mot_lan()
    {
        string jar = MakeJar(("x1/t/9.png", StripPng(new SKColor(0, 255, 0))));
        try
        {
            using var cache = new TileCache(ClientAssets.OpenJar(jar));

            Assert.NotNull(cache.Get(9));
            Assert.Same(cache.Get(9), cache.Get(9));    // dùng lại, không giải mã lại
            Assert.Null(cache.Get(77));
            Assert.Null(cache.Get(77));
        }
        finally
        {
            File.Delete(jar);
        }
    }
}

public class MapViewerOwnershipTests
{
    [Fact]
    public void Bo_ve_khong_so_huu_bo_o_lat_thi_dong_no_khong_dong_bo_o_lat_dung_chung()
    {
        string jar = Path.Combine(Path.GetTempPath(), "toolql-own-" + Guid.NewGuid().ToString("N") + ".jar");
        using (var stream = File.Create(jar))
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create))
        {
            using var entry = zip.CreateEntry("x1/t/9.png").Open();
            using var bitmap = new SkiaSharp.SKBitmap(24, 24);
            using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
            entry.Write(image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100).ToArray());
        }
        try
        {
            using var shared = new TileCache(ClientAssets.OpenJar(jar));
            var first = new MapViewer(shared, viewport: null, ownsTiles: false);
            var second = new MapViewer(shared, viewport: null, ownsTiles: false);

            first.Dispose();   // không được đóng bộ ô lát của người khác

            Assert.NotNull(shared.Get(9));
            second.Dispose();
            Assert.NotNull(shared.Get(9));
        }
        finally
        {
            File.Delete(jar);
        }
    }
}
