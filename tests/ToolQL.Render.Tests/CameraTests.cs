using SkiaSharp;
using ToolQL.Core;
using ToolQL.Protocol.Messages;
using ToolQL.Render;

namespace ToolQL.Render.Tests;

/// <summary>
/// Số liệu tính tay theo thuật toán của client (<c>cu_0.l</c>): x = chânX − rộng/2 + (rộng/6)·hướng, y = chânY − (cao·2/3) − 1;
/// x trong [24, (cột − 1)·24 − rộng], y trong [0, (hàng − 1)·24 − cao] (kẹp cận trên sau cận dưới).
/// </summary>
public class CameraTests
{
    // Màn hình 480×320, map 60×24: tâm ngang 240, 2/3 cao = 640/3 = 213, bu = 480/6 = 80,
    // dq = 59·24 − 480 = 936, iH = 23·24 − 320 = 232.
    private static CameraPosition At(int x, int y, int facing = 0, int cols = 60, int rows = 24) =>
        Camera.Follow(x, y, facing, 480, 320, cols, rows);

    [Fact]
    public void Dat_nhan_vat_giua_ngang_va_chan_o_hai_phan_ba_chieu_cao()
    {
        var c = At(1063, 432);
        Assert.Equal(new CameraPosition(1063 - 240, 432 - 213 - 1), c);   // (823, 218), đều trong giới hạn
    }

    [Fact]
    public void Huong_nhin_lech_camera_mot_phan_sau_chieu_rong()
    {
        Assert.Equal(823 + 80, At(1063, 432, facing: 1).X);
        Assert.Equal(823 - 80, At(1063, 432, facing: -1).X);
    }

    [Fact]
    public void Ke_cac_mep_trai_tren_phai_duoi()
    {
        Assert.Equal(24, At(50, 432).X);        // thô −190 → cận dưới 24 (cột 0 không bao giờ hiện)
        Assert.Equal(936, At(1400, 432).X);     // thô 1160 → cận trên (cột cuối không bao giờ hiện)
        Assert.Equal(0, At(1063, 100).Y);       // thô −114 → 0
        Assert.Equal(232, At(1063, 700).Y);     // thô 486 → 232
    }

    [Fact]
    public void Map_hep_hon_man_hinh_thi_can_tren_thang_nhu_client_va_co_the_am()
    {
        // 10 cột: dq = 9·24 − 480 = −264; client đặt iR = dq (âm) vì kẹp cận trên chạy sau cận dưới.
        Assert.Equal(-264, At(100, 100, cols: 10, rows: 24).X);
        // 5 hàng: iH = 4·24 − 320 = −224.
        Assert.Equal(-224, At(100, 100, cols: 60, rows: 5).Y);
    }

    // ---------- vẽ theo khung nhìn ----------

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

    private static GameSnapshot WorldWithMe(short footX, short footY)
    {
        // 20×20, toàn ô mã 1 (đỏ), riêng ô (cột 11, hàng 6) mã 2 (xanh lá).
        var tiles = Enumerable.Repeat((byte)1, 400).ToArray();
        tiles[6 * 20 + 11] = 2;
        var me = new MeState(1, "mình", 0, 0, footX, footY, 100, 100, 0, 0, 0,
            Array.Empty<ItemSlot?>(), Array.Empty<ItemSlot?>(), Array.Empty<ItemSlot?>(), Array.Empty<short>());
        return new GameSnapshot(
            me,
            new MapState(1, "Thử", 9, 20, 20, tiles, Array.Empty<Portal>(), Array.Empty<Npc>(), Array.Empty<MapBackground>()),
            Array.Empty<MobState>(), Array.Empty<DroppedItem>(), Array.Empty<PlayerState>(), new Dictionary<int, int>(), 0, 1);
    }

    [Fact]
    public void Ve_theo_khung_nhin_ra_dung_kich_thuoc_va_dung_o_theo_camera()
    {
        using var set = TileSet.FromPng(StripPng(new SKColor(255, 0, 0), new SKColor(0, 255, 0)));
        // Khung 120×96: di = 192/3 = 64; chân (300, 200) → camera (300 − 60, 200 − 64 − 1) = (240, 135).
        var snapshot = WorldWithMe(300, 200);

        using var view = MapRenderer.Render(snapshot, set, new Viewport(120, 96));

        Assert.Equal((120, 96), (view.Width, view.Height));
        // Ô (cột 11, hàng 6) phủ thế giới x 264..288, y 144..168 → khung x 24..48, y 9..33.
        Assert.Equal(new SKColor(0, 255, 0), view.GetPixel(30, 20));
        // Điểm khung (5, 5) = thế giới (245, 140) = cột 10, hàng 5 → mã 1 (đỏ).
        Assert.Equal(new SKColor(255, 0, 0), view.GetPixel(5, 5));
    }

    [Fact]
    public void Ngoai_map_thi_de_mau_nen_khi_map_hep_hon_khung()
    {
        using var set = TileSet.FromPng(StripPng(new SKColor(255, 0, 0)));
        // Map 20×20 = 480×480, khung 600×200: dq = 19·24 − 600 = −144 → camera x âm, bên trái map là nền.
        var snapshot = WorldWithMe(240, 240);

        using var view = MapRenderer.Render(snapshot, set, new Viewport(600, 200));

        Assert.Equal(MapRenderer.BackgroundColor, view.GetPixel(3, 100));   // thế giới x = −147: ngoài map
    }

    [Fact]
    public void Khong_co_nhan_vat_thi_camera_o_giua_map_va_van_ve_duoc()
    {
        using var set = TileSet.FromPng(StripPng(new SKColor(255, 0, 0), new SKColor(0, 255, 0)));
        var snapshot = WorldWithMe(0, 0) with { Me = null };

        using var view = MapRenderer.Render(snapshot, set, new Viewport(120, 96));

        Assert.Equal((120, 96), (view.Width, view.Height));
        Assert.Equal(new SKColor(255, 0, 0), view.GetPixel(60, 48));   // giữa khung: thuộc map, ô mã 1
    }

    [Fact]
    public void Bo_ve_khung_nhin_cua_MapViewer_dung_khung_da_cho()
    {
        using var viewer = new MapViewer(tiles: null, new Viewport(200, 100));
        using var bitmap = viewer.Render(WorldWithMe(300, 200))!;
        Assert.Equal((200, 100), (bitmap.Width, bitmap.Height));
    }
}
