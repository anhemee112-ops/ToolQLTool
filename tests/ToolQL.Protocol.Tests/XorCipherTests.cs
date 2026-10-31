using ToolQL.Protocol;
using Xunit;

namespace ToolQL.Protocol.Tests;

/// <summary>
/// Vectơ tính tay từ công thức trong docs/protocol/transport.md (mức tin cậy: từ-mã, chưa xác nhận với server).
/// </summary>
public class XorCipherTests
{
    [Fact]
    public void Bien_doi_khoa_tuan_tu()
    {
        // thô {01,02,04}: k1 = 02^01 = 03; k2 = 04^03 (đã đổi) = 07
        Assert.Equal(new byte[] { 0x01, 0x03, 0x07 }, XorCipher.TransformKey(new byte[] { 0x01, 0x02, 0x04 }));
        // thô {F0,0F}: k1 = 0F^F0 = FF
        Assert.Equal(new byte[] { 0xF0, 0xFF }, XorCipher.TransformKey(new byte[] { 0xF0, 0x0F }));
        // khóa 1 byte không đổi
        Assert.Equal(new byte[] { 0x5A }, XorCipher.TransformKey(new byte[] { 0x5A }));
    }

    [Fact]
    public void Bien_doi_khoa_khong_sua_mang_goc()
    {
        var raw = new byte[] { 1, 2, 4 };
        XorCipher.TransformKey(raw);
        Assert.Equal(new byte[] { 1, 2, 4 }, raw);
    }

    [Fact]
    public void Khoa_mot_byte_khong_doi_chi_so()
    {
        var c = new XorCipher(new byte[] { 0x5A });
        // 00^5A, FF^5A, 5A^5A
        Assert.Equal(0x5A, c.Encrypt(0x00));
        Assert.Equal(0xA5, c.Encrypt(0xFF));
        Assert.Equal(0x00, c.Encrypt(0x5A));
    }

    [Fact]
    public void Nhieu_byte_va_quay_vong_chi_so()
    {
        var c = new XorCipher(new byte[] { 0x01, 0x02, 0x04 }); // khóa đã đổi {01,03,07}
        // 10^01=0B, 20^03=17, 30^07=19, rồi quay về 01: 40^01=29
        var outp = new[] { c.Encrypt(10), c.Encrypt(20), c.Encrypt(30), c.Encrypt(40) };
        Assert.Equal(new byte[] { 11, 23, 25, 41 }, outp);
    }

    [Fact]
    public void Khoa_hai_byte_voi_gia_tri_zero()
    {
        var c = new XorCipher(new byte[] { 0xF0, 0x0F }); // khóa đã đổi {F0,FF}
        Assert.Equal(new byte[] { 0xF0, 0xFF, 0xF0 }, new[] { c.Encrypt(0), c.Encrypt(0), c.Encrypt(0) });
    }

    [Fact]
    public void Hai_chieu_co_chi_so_rieng()
    {
        var c = new XorCipher(new byte[] { 0x01, 0x02, 0x04 }); // {01,03,07}
        c.Encrypt(0); c.Encrypt(0); // R đã ở 2
        // Q vẫn ở 0: byte nhận đầu tiên dùng k[0]=01
        Assert.Equal(0x54, c.Decrypt(0x55));
        Assert.Equal(0x56 ^ 0x03, c.Decrypt(0x56)); // k[1]
        // R tiếp tục ở 2: k[2]=07
        Assert.Equal(0x07, c.Encrypt(0));
    }

    [Fact]
    public void Gui_roi_nhan_lai_cho_ra_ban_dau()
    {
        var a = new XorCipher(new byte[] { 9, 8, 7, 6 });
        var b = new XorCipher(new byte[] { 9, 8, 7, 6 });
        for (int i = 0; i < 20; i++)
            Assert.Equal((byte)(i * 13), b.Decrypt(a.Encrypt((byte)(i * 13))));
    }

    [Fact]
    public void Khoa_rong_thi_nem_loi()
    {
        Assert.Throws<System.ArgumentException>(() => new XorCipher(new byte[0]));
    }
}
