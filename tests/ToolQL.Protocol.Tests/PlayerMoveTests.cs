using ToolQL.Protocol;
using ToolQL.Protocol.Messages;
using Xunit;

namespace ToolQL.Protocol.Tests;

/// <summary>Gói dựng tay theo messages.md; kích thước 8 và 4 byte khớp bắt gói (verification.md mục 9b).</summary>
public class PlayerMoveTests
{
    [Fact]
    public void Moved_doc_id_x_y_va_tieu_thu_het()
    {
        var w = new JavaStreamWriter();
        w.WriteInt(77); w.WriteShort(120); w.WriteShort(-5);
        var m = PlayerMoved.Parse(w.ToArray());
        Assert.Equal((77, (short)120, (short)-5), (m.Id, m.X, m.Y));
        Assert.Equal(0, m.ExtraBytes);
    }

    [Fact]
    public void Moved_thieu_byte_thi_nem_loi()
    {
        Assert.Throws<InvalidDataException>(() => PlayerMoved.Parse(new byte[] { 0, 0, 0, 1, 0 }));
    }

    [Fact]
    public void Left_doc_id_va_bao_byte_thua()
    {
        var w = new JavaStreamWriter();
        w.WriteInt(9); w.WriteByte(1);
        var m = PlayerLeft.Parse(w.ToArray());
        Assert.Equal(9, m.Id);
        Assert.Equal(1, m.ExtraBytes);
    }
}
