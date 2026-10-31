using System.IO;
using ToolQL.Protocol;
using Xunit;

namespace ToolQL.Protocol.Tests;

/// <summary>
/// Chứng minh mã khớp transport.md. Nhánh độ dài 3 byte chỉ kiểm theo đúng mô tả trong tài liệu,
/// CHƯA xác nhận với server.
/// </summary>
public class PacketTests
{
    private static readonly byte[] RawKey = { 0x01, 0x02, 0x04 }; // khóa đã đổi {01,03,07}

    private static byte[] Gui(sbyte cmd, byte[] payload, XorCipher? c = null)
    {
        var ms = new MemoryStream();
        new PacketWriter(ms) { Cipher = c }.Write(cmd, payload);
        return ms.ToArray();
    }

    private static Packet Nhan(byte[] wire, XorCipher? c = null) =>
        new PacketReader(new MemoryStream(wire)) { Cipher = c }.Read();

    [Fact]
    public void Goi_khoi_dau_27_rong_la_E5_00_00()
    {
        Assert.Equal(new byte[] { 0xE5, 0x00, 0x00 }, Gui(-27, new byte[0]));
    }

    [Fact]
    public void Gui_khong_khoa()
    {
        Assert.Equal(new byte[] { 0x0A, 0x00, 0x02, 0xAA, 0xBB }, Gui(10, new byte[] { 0xAA, 0xBB }));
    }

    [Fact]
    public void Gui_co_khoa_xor_ca_ma_lenh_do_dai_va_payload()
    {
        // khóa {01,03,07}: E5^01=E4, 00^03=03, 02^07=05, AA^01=AB, BB^03=B8
        var wire = Gui(-27, new byte[] { 0xAA, 0xBB }, new XorCipher(RawKey));
        Assert.Equal(new byte[] { 0xE4, 0x03, 0x05, 0xAB, 0xB8 }, wire);
    }

    [Fact]
    public void Gui_lien_tiep_giu_chi_so_khoa()
    {
        var ms = new MemoryStream();
        var w = new PacketWriter(ms) { Cipher = new XorCipher(RawKey) };
        w.Write(0, new byte[0]); // dùng 3 byte khóa: 01,03,07
        w.Write(0, new byte[0]); // quay lại 01,03,07
        Assert.Equal(new byte[] { 0x01, 0x03, 0x07, 0x01, 0x03, 0x07 }, ms.ToArray());
    }

    [Fact]
    public void Nhan_khong_khoa()
    {
        var p = Nhan(new byte[] { 0xE5, 0x00, 0x02, 0x11, 0x22 });
        Assert.Equal(-27, p.Command);
        Assert.Equal(new byte[] { 0x11, 0x22 }, p.Payload);
        Assert.False(p.UsedThreeByteLength);
    }

    [Fact]
    public void Nhan_co_khoa_la_nghich_dao_cua_gui()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7 };
        var p = Nhan(Gui(-30, payload, new XorCipher(RawKey)), new XorCipher(RawKey));
        Assert.Equal(-30, p.Command);
        Assert.Equal(payload, p.Payload);
    }

    [Fact]
    public void Payload_rong()
    {
        var p = Nhan(Gui(5, new byte[0]));
        Assert.Equal(5, p.Command);
        Assert.Empty(p.Payload);
    }

    [Fact]
    public void Do_dai_2_byte_toi_da_65535()
    {
        var payload = new byte[65535];
        payload[65534] = 9;
        var p = Nhan(Gui(5, payload));
        Assert.Equal(65535, p.Payload.Length);
        Assert.Equal(9, p.Payload[65534]);
    }

    [Fact]
    public void Gui_payload_qua_65535_thi_nem_loi()
    {
        Assert.Throws<System.ArgumentException>(() => Gui(5, new byte[70000]));
    }

    [Fact]
    public void Nhom_3_byte_theo_mo_ta_tai_lieu_payload_70000()
    {
        // 70000 = 0x011170 → b0=70, b1=11, b2=01; trên dây mỗi byte trừ 128 (đọc lại = cộng 128 mod 256)
        var wire = new byte[4 + 70000];
        wire[0] = 0x0B; wire[1] = 0xF0; wire[2] = 0x91; wire[3] = 0x81;
        wire[4 + 69999] = 0x7E;
        var p = Nhan(wire);
        Assert.Equal(11, p.Command);
        Assert.True(p.UsedThreeByteLength);
        Assert.Equal(70000, p.Payload.Length);
        Assert.Equal(0x7E, p.Payload[69999]);
    }

    [Fact]
    public void Nhom_3_byte_co_khoa()
    {
        // Dựng tay: khung rõ (cmd -32, độ dài 3 = {83,80,80} trên dây sau trừ 128 → b0=3), payload {7,8,9}, rồi XOR theo khóa.
        var clear = new byte[] { 0xE0, 0x83, 0x80, 0x80, 7, 8, 9 };
        var enc = new XorCipher(RawKey);
        var wire = new byte[clear.Length];
        for (int i = 0; i < clear.Length; i++) wire[i] = enc.Encrypt(clear[i]);
        var p = Nhan(wire, new XorCipher(RawKey));
        Assert.Equal(-32, p.Command);
        Assert.True(p.UsedThreeByteLength);
        Assert.Equal(new byte[] { 7, 8, 9 }, p.Payload);
    }

    [Fact]
    public void Moi_ma_trong_nhom_3_byte_duoc_nhan_ra()
    {
        foreach (sbyte cmd in PacketReader.ThreeByteLengthCommands)
        {
            var p = Nhan(new byte[] { (byte)cmd, 0x80, 0x80, 0x80 });
            Assert.True(p.UsedThreeByteLength);
            Assert.Empty(p.Payload);
        }
    }

    [Fact]
    public void Thieu_du_lieu_thi_nem_loi()
    {
        Assert.Throws<EndOfStreamException>(() => Nhan(new byte[0]));
        Assert.Throws<EndOfStreamException>(() => Nhan(new byte[] { 5, 0 }));
        Assert.Throws<EndOfStreamException>(() => Nhan(new byte[] { 5, 0, 4, 1, 2 }));
    }

    [Fact]
    public void Doc_hai_goi_lien_tiep()
    {
        var ms = new MemoryStream();
        var w = new PacketWriter(ms);
        w.Write(1, new byte[] { 1 });
        w.Write(2, new byte[] { 2, 2 });
        ms.Position = 0;
        var r = new PacketReader(ms);
        Assert.Equal(1, r.Read().Command);
        Assert.Equal(new byte[] { 2, 2 }, r.Read().Payload);
    }
}
