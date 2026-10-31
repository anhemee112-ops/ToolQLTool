using System.IO;
using ToolQL.Probe;
using ToolQL.Protocol;
using Xunit;

namespace ToolQL.Protocol.Tests;

public class ClientInfoTests
{
    [Fact]
    public void Goi_29_sub2_dung_thu_tu_trong_an_0_dF()
    {
        var payload = LoginCommand.BuildClientInfo();
        var r = new JavaStreamReader(new MemoryStream(payload));
        Assert.Equal(2, r.ReadSByte());   // sub
        Assert.Equal(1, r.ReadSByte());   // bQ.bB
        Assert.Equal(1, r.ReadSByte());   // aA.bH
        Assert.False(r.ReadBool());
        Assert.Equal(240, r.ReadInt());
        Assert.Equal(320, r.ReadInt());
        Assert.False(r.ReadBool());
        Assert.False(r.ReadBool());
        Assert.Equal("j2me|2.5.0", r.ReadUtf());
        Assert.Equal(payload.Length, r.Position); // tiêu thụ đúng, không thừa
    }
}
