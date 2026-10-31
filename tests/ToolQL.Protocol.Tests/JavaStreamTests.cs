using System.IO;
using ToolQL.Protocol;
using Xunit;

namespace ToolQL.Protocol.Tests;

public class JavaStreamTests
{
    private static JavaStreamReader Doc(byte[] data) => new(new MemoryStream(data));

    [Fact]
    public void Byte_co_dau_va_khong_dau()
    {
        var w = new JavaStreamWriter();
        w.WriteSByte(-128); w.WriteSByte(127); w.WriteByte(255);
        var r = Doc(w.ToArray());
        Assert.Equal(-128, r.ReadSByte());
        Assert.Equal(127, r.ReadSByte());
        Assert.Equal(255, r.ReadByte());
    }

    [Fact]
    public void Short_big_endian_va_gia_tri_am()
    {
        var w = new JavaStreamWriter();
        w.WriteShort(0x0102); w.WriteShort(-2); w.WriteUShort(65535);
        Assert.Equal(new byte[] { 0x01, 0x02, 0xFF, 0xFE, 0xFF, 0xFF }, w.ToArray());
        var r = Doc(w.ToArray());
        Assert.Equal(0x0102, r.ReadShort());
        Assert.Equal(-2, r.ReadShort());
        Assert.Equal(65535, r.ReadUShort());
    }

    [Fact]
    public void Int_va_long_big_endian()
    {
        var w = new JavaStreamWriter();
        w.WriteInt(0x01020304); w.WriteInt(int.MinValue);
        w.WriteLong(0x0102030405060708L); w.WriteLong(-1L);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, w.ToArray()[..4]);
        var r = Doc(w.ToArray());
        Assert.Equal(0x01020304, r.ReadInt());
        Assert.Equal(int.MinValue, r.ReadInt());
        Assert.Equal(0x0102030405060708L, r.ReadLong());
        Assert.Equal(-1L, r.ReadLong());
    }

    [Fact]
    public void Bool_la_mot_byte()
    {
        var w = new JavaStreamWriter();
        w.WriteBool(true); w.WriteBool(false);
        Assert.Equal(new byte[] { 1, 0 }, w.ToArray());
        var r = Doc(new byte[] { 1, 0, 7 });
        Assert.True(r.ReadBool());
        Assert.False(r.ReadBool());
        Assert.True(r.ReadBool()); // khác 0 là true, như Java
    }

    [Fact]
    public void Utf_tieng_Viet_vong_ghi_doc()
    {
        var w = new JavaStreamWriter();
        w.WriteUtf("Ngọc Rồng Online"); w.WriteUtf("");
        var bytes = w.ToArray();
        Assert.Equal(0, bytes[0]);
        Assert.Equal(bytes[1], (byte)System.Text.Encoding.UTF8.GetByteCount("Ngọc Rồng Online"));
        var r = Doc(bytes);
        Assert.Equal("Ngọc Rồng Online", r.ReadUtf());
        Assert.Equal("", r.ReadUtf());
    }

    [Fact]
    public void Utf_qua_dai_thi_nem_loi()
    {
        var w = new JavaStreamWriter();
        Assert.Throws<System.ArgumentException>(() => w.WriteUtf(new string('a', 65536)));
    }

    [Fact]
    public void Doc_n_byte()
    {
        var r = Doc(new byte[] { 1, 2, 3, 4, 5 });
        Assert.Equal(new byte[] { 1, 2, 3 }, r.ReadBytes(3));
        Assert.Equal(new byte[0], r.ReadBytes(0));
        Assert.Equal(new byte[] { 4, 5 }, r.ReadBytes(2));
    }

    [Fact]
    public void Dem_so_byte_da_tieu_thu()
    {
        var r = Doc(new byte[] { 0, 1, 0, 0, 0, 2, 9, 9 });
        r.ReadShort(); r.ReadInt();
        Assert.Equal(6, r.Position);
        r.ReadBytes(2);
        Assert.Equal(8, r.Position);
    }

    [Fact]
    public void Doc_qua_du_lieu_thi_nem_loi_ro_rang()
    {
        var r = Doc(new byte[] { 1, 2, 3 });
        var ex = Assert.Throws<EndOfStreamException>(() => r.ReadInt());
        Assert.Contains("4", ex.Message);
        Assert.Throws<EndOfStreamException>(() => Doc(new byte[2]).ReadBytes(3));
        Assert.Throws<EndOfStreamException>(() => Doc(new byte[0]).ReadByte());
        // Độ dài UTF nói 5 byte nhưng chỉ còn 2
        Assert.Throws<EndOfStreamException>(() => Doc(new byte[] { 0, 5, 65, 66 }).ReadUtf());
    }

    [Fact]
    public void Doc_so_byte_am_thi_nem_loi()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => Doc(new byte[1]).ReadBytes(-1));
    }
}
