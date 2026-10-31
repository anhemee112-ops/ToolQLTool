using ToolQL.Protocol.Resources;

namespace ToolQL.Protocol.Tests;

/// <summary>Gói dựng tay theo docs/protocol/render.md ("Gói sprite dữ liệu + ảnh"). Khớp dữ liệu thật xem `probe mob`.</summary>
public class SpritePackTests
{
    /// <summary>2 hình con, 2 khung (2 mảnh và 1 mảnh), danh sách hoạt ảnh [0, 1, -498].</summary>
    private static byte[] DataBlock(int kind = 0)
    {
        var w = new JavaStreamWriter();
        w.WriteSByte(2);
        w.WriteByte(0);
        if (kind is 0 or 1) { w.WriteByte(0); w.WriteByte(0); } else { w.WriteShort(0); w.WriteShort(0); }
        w.WriteByte(49); w.WriteByte(25);
        w.WriteByte(1);
        if (kind is 0 or 1) { w.WriteByte(49); w.WriteByte(0); } else { w.WriteShort(49); w.WriteShort(0); }
        w.WriteByte(30); w.WriteByte(20);

        w.WriteShort(2);
        w.WriteSByte(2);
        w.WriteShort(-41); w.WriteShort(-33); w.WriteSByte(0);
        w.WriteShort(-49); w.WriteShort(-15); w.WriteSByte(1);
        w.WriteSByte(1);
        w.WriteShort(5); w.WriteShort(6); w.WriteSByte(1);

        w.WriteShort(3);
        w.WriteShort(0); w.WriteShort(1); w.WriteShort(-498);
        return w.ToArray();
    }

    [Fact]
    public void Khoi_du_lieu_kieu_0_doc_dung_va_tieu_thu_het()
    {
        var p = SpritePack.Parse(DataBlock(), 0);

        Assert.Equal(2, p.SubImages.Length);
        Assert.Equal(new SubImage(0, 0, 0, 49, 25), p.SubImages[0]);
        Assert.Equal(new SubImage(1, 49, 0, 30, 20), p.SubImages[1]);
        Assert.Equal(2, p.Frames.Length);
        Assert.Equal(new FramePart(-41, -33, 0), p.Frames[0].Parts[0]);
        Assert.Equal(new FramePart(-49, -15, 1), p.Frames[0].Parts[1]);
        Assert.Single(p.Frames[1].Parts);
        Assert.Equal(new short[] { 0, 1, -498 }, p.Sequence);
        Assert.Equal(0, p.ExtraBytes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Kieu_khac_0_doc_x_y_hinh_con_theo_kieu(int kind)
    {
        var p = SpritePack.Parse(DataBlock(kind), kind);   // kiểu 1: x, y ubyte; kiểu khác: short
        Assert.Equal(new SubImage(1, 49, 0, 30, 20), p.SubImages[1]);
        Assert.Equal(0, p.ExtraBytes);
    }

    [Fact]
    public void Byte_thua_duoc_dem_va_cat_ngan_la_loi()
    {
        var full = DataBlock();
        Assert.Equal(2, SpritePack.Parse(full.Concat(new byte[] { 9, 9 }).ToArray(), 0).ExtraBytes);
        for (int len = 0; len < full.Length; len++)
        {
            var cut = full[..len];
            Assert.Throws<InvalidDataException>(() => SpritePack.Parse(cut, 0));
        }
    }

    [Fact]
    public void So_luong_am_la_loi()
    {
        var w = new JavaStreamWriter();
        w.WriteSByte(-1);
        Assert.Throws<InvalidDataException>(() => SpritePack.Parse(w.ToArray(), 0));
    }

    private static byte[] MobPayload(byte[] data, byte[] png, int tail, byte[]? extra = null)
    {
        var w = new JavaStreamWriter();
        w.WriteShort(94); w.WriteSByte(0);
        w.WriteInt(data.Length); w.WriteBytes(data);
        w.WriteInt(png.Length); w.WriteBytes(png);
        w.WriteSByte(tail);
        if (extra != null) w.WriteBytes(extra);
        return w.ToArray();
    }

    [Fact]
    public void Goi_11_tach_khoi_du_lieu_png_va_byte_cuoi()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 };
        var m = MobSpritePack.Parse(MobPayload(DataBlock(), png, tail: 0));

        Assert.Equal((94, 0), (m.Type, m.Kind));
        Assert.Equal(png, m.Png);
        Assert.Equal(2, m.Pack.Frames.Length);
        Assert.Equal(0, m.Tail);
        Assert.Null(m.Extra);
        Assert.Equal(0, m.ExtraBytes);
    }

    [Fact]
    public void Goi_11_byte_cuoi_1_hoac_2_doc_them_bang_phu()
    {
        // n = 2; (m = 2: 7, 8); (m = 0)
        var extra = new byte[] { 2, 2, 7, 8, 0 };
        var m = MobSpritePack.Parse(MobPayload(DataBlock(), new byte[] { 1 }, tail: 1, extra));

        Assert.Equal(1, m.Tail);
        Assert.Equal(2, m.Extra!.Length);
        Assert.Equal(new byte[] { 7, 8 }, m.Extra[0]);
        Assert.Empty(m.Extra[1]);
        Assert.Equal(0, m.ExtraBytes);
    }

    [Fact]
    public void Goi_11_cat_ngan_hoac_do_dai_sai_la_loi()
    {
        var full = MobPayload(DataBlock(), new byte[] { 1, 2, 3 }, tail: 0);
        for (int len = 0; len < full.Length; len++)
        {
            var cut = full[..len];
            Assert.Throws<InvalidDataException>(() => MobSpritePack.Parse(cut));
        }

        var bad = MobPayload(DataBlock(), new byte[] { 1 }, tail: 0);
        bad[3] = 0x7F;   // độ dài khối dữ liệu vượt quá gói
        Assert.Throws<InvalidDataException>(() => MobSpritePack.Parse(bad));
    }
}
