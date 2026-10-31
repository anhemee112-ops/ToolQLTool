using ToolQL.Protocol.Resources;

namespace ToolQL.Protocol.Tests;

/// <summary>Gói dựng tay theo docs/protocol/resources.md. Chứng minh bộ đọc khớp tài liệu; khớp dữ liệu thật xem `probe data`.</summary>
public class NrDataTests
{
    private static byte[] Block(Action<JavaStreamWriter> body)
    {
        var w = new JavaStreamWriter();
        body(w);
        return w.ToArray();
    }

    private static byte[] ImageBlock() => Block(w =>
    {
        w.WriteShort(2);
        w.WriteByte(3); w.WriteShort(10); w.WriteShort(20); w.WriteShort(30); w.WriteShort(40);
        w.WriteByte(255); w.WriteShort(0); w.WriteShort(0); w.WriteShort(1); w.WriteShort(1);
    });

    private static byte[] PartBlock() => Block(w =>
    {
        w.WriteShort(2);
        w.WriteSByte(3);                                   // loại 3 → 2 phần tử
        for (int i = 0; i < 2; i++) { w.WriteShort(100 + i); w.WriteSByte(-5); w.WriteSByte(7); }
        w.WriteSByte(0);                                   // loại 0 → 3 phần tử
        for (int i = 0; i < 3; i++) { w.WriteShort(200 + i); w.WriteSByte(1); w.WriteSByte(2); }
    });

    private static byte[] Payload(byte version, params byte[][] blocks)
    {
        var w = new JavaStreamWriter();
        w.WriteSByte(version);
        foreach (var b in blocks) { w.WriteInt(b.Length); w.WriteBytes(b); }
        return w.ToArray();
    }

    [Fact]
    public void Tach_sau_khoi_theo_do_dai_va_tieu_thu_het()
    {
        var empty = new byte[] { 0, 0 };
        var data = NrData.Parse(Payload(30, new byte[] { 1 }, new byte[] { 2, 2 }, empty, ImageBlock(), PartBlock(), new byte[] { 9, 9, 9 }));

        Assert.Equal(30, data.Version);
        Assert.Equal(new byte[] { 1 }, data.Dart);
        Assert.Equal(new byte[] { 2, 2 }, data.Arrow);
        Assert.Equal(empty, data.Effect);
        Assert.Equal(ImageBlock(), data.Image);
        Assert.Equal(PartBlock(), data.Part);
        Assert.Equal(new byte[] { 9, 9, 9 }, data.Skill);
        Assert.Equal(0, data.ExtraBytes);
    }

    [Fact]
    public void Khoi_thieu_byte_hoac_do_dai_am_la_loi_va_byte_thua_duoc_dem()
    {
        var ok = Payload(1, new byte[0], new byte[0], new byte[0], new byte[0], new byte[0], new byte[0]);
        Assert.Equal(2, NrData.Parse(ok.Concat(new byte[] { 7, 7 }).ToArray()).ExtraBytes);
        Assert.Throws<InvalidDataException>(() => NrData.Parse(ok[..^1]));

        var negative = Payload(1, new byte[0]);
        negative[1] = 0xFF;   // độ dài khối đầu thành số âm
        Assert.Throws<InvalidDataException>(() => NrData.Parse(negative));
    }

    [Fact]
    public void Bang_anh_nho_doc_dung()
    {
        var t = ImageTable.Parse(ImageBlock());
        Assert.Equal(2, t.Sprites.Length);
        Assert.Equal(new SpriteRect(3, 10, 20, 30, 40), t.Sprites[0]);
        Assert.Equal(255, t.Sprites[1].Atlas);   // atlas là ubyte, không âm
        Assert.Equal(0, t.ExtraBytes);
    }

    [Fact]
    public void Bang_anh_nho_cat_ngan_thi_loi()
    {
        var full = ImageBlock();
        for (int len = 0; len < full.Length; len++)
        {
            var cut = full[..len];
            Assert.Throws<InvalidDataException>(() => ImageTable.Parse(cut));
        }
    }

    [Fact]
    public void Bang_phan_than_so_phan_tu_theo_loai()
    {
        var t = PartTable.Parse(PartBlock());
        Assert.Equal(2, t.Sets.Length);
        Assert.Equal(3, t.Sets[0].Kind);
        Assert.Equal(2, t.Sets[0].Parts.Length);
        Assert.Equal(new BodyPartRef(101, -5, 7), t.Sets[0].Parts[1]);
        Assert.Equal(3, t.Sets[1].Parts.Length);
        Assert.Equal(0, t.ExtraBytes);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 17)]
    [InlineData(2, 14)]
    [InlineData(3, 2)]
    public void Bang_phan_than_dem_phan_tu_cho_tung_loai(int kind, int count)
    {
        var block = Block(w =>
        {
            w.WriteShort(1); w.WriteSByte(kind);
            for (int i = 0; i < count; i++) { w.WriteShort(i); w.WriteSByte(0); w.WriteSByte(0); }
        });
        var t = PartTable.Parse(block);
        Assert.Equal(count, t.Sets[0].Parts.Length);
        Assert.Equal(0, t.ExtraBytes);
    }

    [Fact]
    public void Bang_phan_than_loai_la_hoac_cat_ngan_thi_loi()
    {
        var unknown = Block(w => { w.WriteShort(1); w.WriteSByte(9); });
        Assert.Throws<InvalidDataException>(() => PartTable.Parse(unknown));

        var full = PartBlock();
        for (int len = 0; len < full.Length; len++)
        {
            var cut = full[..len];
            Assert.Throws<InvalidDataException>(() => PartTable.Parse(cut));
        }
    }
}
