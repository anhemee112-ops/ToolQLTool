using System.IO;
using ToolQL.Protocol;
using Xunit;

namespace ToolQL.Protocol.Tests;

/// <summary>Gói -27 dựng tay theo bố cục trong transport.md (từ-mã, chưa xác nhận với server).</summary>
public class KeyHandshakeTests
{
    private static byte[] DungGoi27(byte[] rawKey, string text = "abc", int number = 0x01020304, byte flag = 1, int extra = 0)
    {
        var w = new JavaStreamWriter();
        w.WriteByte(rawKey.Length);
        w.WriteBytes(rawKey);
        w.WriteUtf(text);
        w.WriteInt(number);
        w.WriteByte(flag);
        for (int i = 0; i < extra; i++) w.WriteByte(0);
        return w.ToArray();
    }

    [Fact]
    public void Parse_doc_dung_cac_truong()
    {
        var info = KeyHandshake.Parse(DungGoi27(new byte[] { 1, 2, 4 }, "máy chủ", 305419896, 1));
        Assert.Equal(new byte[] { 1, 2, 4 }, info.RawKey); // khóa thô, chưa biến đổi
        Assert.Equal("máy chủ", info.Text);
        Assert.Equal(305419896, info.Number);
        Assert.Equal(1, info.Flag);
        Assert.Equal(0, info.ExtraBytes);
    }

    [Fact]
    public void Parse_ghi_lai_byte_thua()
    {
        Assert.Equal(3, KeyHandshake.Parse(DungGoi27(new byte[] { 9 }, extra: 3)).ExtraBytes);
    }

    [Fact]
    public void Parse_khoa_do_dai_0_thi_nem_loi()
    {
        Assert.Throws<InvalidDataException>(() => KeyHandshake.Parse(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 }));
    }

    [Fact]
    public void Parse_thieu_byte_thi_nem_loi()
    {
        var full = DungGoi27(new byte[] { 1, 2, 4 });
        for (int len = 0; len < full.Length; len++)
        {
            var cut = full[..len];
            Assert.Throws<InvalidDataException>(() => KeyHandshake.Parse(cut));
        }
    }

    [Fact]
    public void Perform_gui_E5_00_00_roi_bat_ma_hoa_hai_chieu()
    {
        // Server trả gói -27 (chưa mã hóa), sau đó gửi thêm một gói đã mã hóa bằng khóa {01,03,07}.
        var server = new MemoryStream();
        new PacketWriter(server).Write(-27, DungGoi27(new byte[] { 1, 2, 4 }));
        var serverCipher = new XorCipher(new byte[] { 1, 2, 4 });
        new PacketWriter(server) { Cipher = serverCipher }.Write(-30, new byte[] { 5, 6 });
        server.Position = 0;

        var client = new MemoryStream();
        var writer = new PacketWriter(client);
        var reader = new PacketReader(server);

        var info = KeyHandshake.Perform(writer, reader);

        Assert.Equal(new byte[] { 0xE5, 0x00, 0x00 }, client.ToArray()); // gói đầu chưa mã hóa
        Assert.Equal("abc", info.Text);
        Assert.NotNull(writer.Cipher);
        Assert.NotNull(reader.Cipher);

        // Gói kế tiếp từ server giải mã đúng
        var next = reader.Read();
        Assert.Equal(-30, next.Command);
        Assert.Equal(new byte[] { 5, 6 }, next.Payload);

        // Gói gửi tiếp theo đã mã hóa: -30 rỗng → 0xE2^01, 00^03, 00^07
        client.SetLength(0);
        writer.Write(-30, new byte[0]);
        Assert.Equal(new byte[] { 0xE2 ^ 0x01, 0x03, 0x07 }, client.ToArray());
    }

    [Fact]
    public void Perform_goi_dau_khong_phai_27_thi_nem_loi()
    {
        var server = new MemoryStream();
        new PacketWriter(server).Write(-30, new byte[] { 1 });
        server.Position = 0;
        var ex = Assert.Throws<InvalidDataException>(() =>
            KeyHandshake.Perform(new PacketWriter(new MemoryStream()), new PacketReader(server)));
        Assert.Contains("-30", ex.Message);
    }
}
