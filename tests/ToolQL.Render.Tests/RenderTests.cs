using System.IO.Compression;
using SkiaSharp;
using ToolQL.Core;
using ToolQL.Protocol.Messages;
using ToolQL.Render;

namespace ToolQL.Render.Tests;

/// <summary>Ảnh và jar dựng tay (không dùng tài nguyên của client). Khớp tài nguyên thật xem `probe render`.</summary>
public class RenderTests
{
    private static readonly SKColor Red = new(255, 0, 0), Green = new(0, 255, 0), Blue = new(0, 0, 255);

    /// <summary>Dải dọc 24 × (24 · màu.Length), mỗi ô một màu đặc.</summary>
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

    // ---------- giải mã ảnh XOR ----------

    [Fact]
    public void Anh_PNG_thuong_duoc_giu_nguyen()
    {
        var png = StripPng(Red);
        Assert.Equal(png, ClientImageCodec.ToPng(png));
    }

    [Fact]
    public void Anh_bi_XOR_duoc_giai_ra_dung_PNG()
    {
        var png = StripPng(Red, Green);
        var scrambled = ClientImageCodec.Scramble(png);

        Assert.NotEqual(png, scrambled);
        Assert.Equal(0x63, scrambled[0]);   // "cRBC": đúng như tệp thật trong jar
        Assert.Equal(png, ClientImageCodec.ToPng(scrambled));
    }

    [Fact]
    public void Du_lieu_khong_phai_anh_la_loi()
    {
        Assert.Throws<InvalidDataException>(() => ClientImageCodec.ToPng(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
        Assert.Throws<InvalidDataException>(() => ClientImageCodec.ToPng(Array.Empty<byte>()));
    }

    // ---------- jar ----------

    private static string MakeJar(params (string Name, byte[] Data)[] entries)
    {
        string path = Path.Combine(Path.GetTempPath(), "toolql-jar-" + Guid.NewGuid().ToString("N") + ".jar");
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
    public void Doc_bo_o_lat_tu_jar_ca_anh_XOR_va_anh_thuong()
    {
        string jar = MakeJar(
            ("x1/t/9.png", ClientImageCodec.Scramble(StripPng(Red, Green, Blue))),
            ("x1/t/2.png", StripPng(Blue)));
        try
        {
            using var assets = ClientAssets.OpenJar(jar);
            using var nine = assets.LoadTileSet(9)!;
            using var two = assets.LoadTileSet(2)!;

            Assert.Equal(3, nine.Count);
            Assert.Equal(1, two.Count);
            Assert.Null(assets.LoadTileSet(77));    // không có trong jar
            Assert.Null(assets.ReadPng("x1/t/77.png"));
        }
        finally
        {
            File.Delete(jar);
        }
    }

    [Fact]
    public void Jar_khong_ton_tai_la_loi_ro_rang()
    {
        Assert.Throws<FileNotFoundException>(() => ClientAssets.OpenJar(Path.Combine(Path.GetTempPath(), "khong-co.jar")));
    }

    // ---------- ô lát ----------

    [Fact]
    public void Ma_o_tinh_tu_1_va_ngoai_dai_thi_khong_co()
    {
        using var set = TileSet.FromPng(StripPng(Red, Green, Blue));

        Assert.True(set.TryGetSource(1, out var first));
        Assert.Equal(new SKRectI(0, 0, 24, 24), first);
        Assert.True(set.TryGetSource(3, out var last));
        Assert.Equal(new SKRectI(0, 48, 24, 72), last);
        Assert.False(set.TryGetSource(0, out _));    // 0 là ô rỗng
        Assert.False(set.TryGetSource(4, out _));
        Assert.False(set.TryGetSource(-1, out _));
    }

    // ---------- vẽ map ----------

    private static GameSnapshot SnapshotOf(int width, int height, byte[] tiles, params MobState[] mobs) => new(
        null,
        new MapState(1, "Thử", 9, width, height, tiles, Array.Empty<Portal>(), Array.Empty<Npc>(), Array.Empty<MapBackground>()),
        mobs, Array.Empty<DroppedItem>(), Array.Empty<PlayerState>(), new Dictionary<int, int>(), 0, 1);

    [Fact]
    public void Ve_luoi_dung_o_dung_vi_tri_va_bo_trong_o_rong()
    {
        using var set = TileSet.FromPng(StripPng(Red, Green, Blue));
        // 2 × 2: [1 0 / 3 2]
        var snapshot = SnapshotOf(2, 2, new byte[] { 1, 0, 3, 2 });

        using var map = MapRenderer.Render(snapshot, set);

        Assert.Equal((48, 48), (map.Width, map.Height));
        Assert.Equal(Red, map.GetPixel(5, 5));         // ô (0,0) mã 1
        Assert.Equal(Blue, map.GetPixel(5, 29));       // ô (0,1) mã 3
        Assert.Equal(Green, map.GetPixel(29, 29));     // ô (1,1) mã 2
        Assert.Equal(MapRenderer.BackgroundColor, map.GetPixel(29, 5));   // ô (1,0) rỗng
    }

    [Fact]
    public void Ve_khong_co_bo_o_lat_hoac_chua_co_luoi_van_ra_anh()
    {
        var snapshot = SnapshotOf(2, 1, new byte[] { 1, 1 });
        using (var map = MapRenderer.Render(snapshot, tiles: null))
            Assert.Equal((48, 24), (map.Width, map.Height));

        var noGrid = snapshot with { Map = snapshot.Map! with { Tiles = null, Width = 0, Height = 0 } };
        using (var map = MapRenderer.Render(noGrid, tiles: null))
            Assert.True(map.Width > 0 && map.Height > 0);

        using var empty = MapRenderer.Render(snapshot with { Map = null }, tiles: null);
        Assert.True(empty.Width > 0 && empty.Height > 0);
    }

    [Fact]
    public void Ve_quai_con_song_co_danh_dau_con_quai_chet_thi_khong()
    {
        var info = new Mob(0, Array.Empty<bool>(), 1, 1, 100, 0, 100, 36, 48, 0, 0, false);
        var alive = SnapshotOf(4, 4, new byte[16], new MobState(info, 100, true));
        var dead = SnapshotOf(4, 4, new byte[16], new MobState(info, 0, false));

        using var a = MapRenderer.Render(alive, tiles: null);
        using var d = MapRenderer.Render(dead, tiles: null);

        bool aliveDiffers = Enumerable.Range(0, a.Width).Any(x => Enumerable.Range(0, a.Height).Any(y => a.GetPixel(x, y) != MapRenderer.BackgroundColor));
        bool deadDiffers = Enumerable.Range(0, d.Width).Any(x => Enumerable.Range(0, d.Height).Any(y => d.GetPixel(x, y) != MapRenderer.BackgroundColor));
        Assert.True(aliveDiffers);
        Assert.False(deadDiffers);
    }
}
