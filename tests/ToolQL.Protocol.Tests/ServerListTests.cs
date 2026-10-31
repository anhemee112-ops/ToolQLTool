using ToolQL.Protocol;
using Xunit;

namespace ToolQL.Protocol.Tests;

public class ServerListTests
{
    [Fact]
    public void Co_14_server_nhu_servers_md()
    {
        Assert.Equal(14, ServerList.All.Count);
    }

    [Fact]
    public void Tim_theo_so_thu_tu_ten_va_ten_mien()
    {
        Assert.Equal("Vũ trụ 1", ServerList.Find("1")!.Name);
        Assert.Equal("Naga", ServerList.Find("14")!.Name);
        Assert.Equal("Vũ trụ 11", ServerList.Find("vũ trụ 11")!.Name);
        Assert.Equal(14446, ServerList.Find("dragon10.teamobi.com")!.Port); // mục đầu tiên dùng tên miền đó
    }

    [Fact]
    public void Khong_thay_thi_tra_null()
    {
        Assert.Null(ServerList.Find("0"));
        Assert.Null(ServerList.Find("15"));
        Assert.Null(ServerList.Find("khong-co"));
    }

    [Fact]
    public void Perform_goi_onPacket_cho_ca_hai_chieu()
    {
        var w = new JavaStreamWriter();
        w.WriteByte(1); w.WriteByte(7); w.WriteUtf(""); w.WriteInt(0); w.WriteByte(0);
        var server = new MemoryStream();
        new PacketWriter(server).Write(-27, w.ToArray());
        server.Position = 0;

        var seen = new List<(PacketDirection, sbyte)>();
        KeyHandshake.Perform(new PacketWriter(new MemoryStream()), new PacketReader(server), (d, p) => seen.Add((d, p.Command)));

        Assert.Equal(new[] { (PacketDirection.ClientToServer, (sbyte)-27), (PacketDirection.ServerToClient, (sbyte)-27) }, seen);
    }
}
