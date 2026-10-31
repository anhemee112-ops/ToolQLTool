using System.IO;
using System.Linq;
using System.Text.Json;
using ToolQL.Protocol;
using Xunit;

namespace ToolQL.Protocol.Tests;

public class CaptureSinkTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "toolql-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private string[] Dong() => File.ReadAllLines(Path.Combine(_dir, "packets.jsonl"));

    [Fact]
    public void Ghi_moi_goi_mot_dong_json_day_du_truong()
    {
        using (var sink = new CaptureSink(_dir))
        {
            sink.Record(PacketDirection.ServerToClient, new Packet(-28, new byte[] { 4, 0xAB, 0xCD }));
            sink.Record(PacketDirection.ClientToServer, new Packet(-27, new byte[0]));
        }
        var lines = Dong();
        Assert.Equal(2, lines.Length);

        using var a = JsonDocument.Parse(lines[0]);
        var r = a.RootElement;
        Assert.Equal("S->C", r.GetProperty("dir").GetString());
        Assert.Equal(-28, r.GetProperty("cmd").GetInt32());
        Assert.Equal(4, r.GetProperty("sub").GetInt32());
        Assert.Equal(3, r.GetProperty("len").GetInt32());
        Assert.Equal("04ABCD", r.GetProperty("hex").GetString());
        Assert.False(r.GetProperty("masked").GetBoolean());
        Assert.True(DateTimeOffset.TryParse(r.GetProperty("t").GetString(), out _));

        using var b = JsonDocument.Parse(lines[1]);
        Assert.Equal("C->S", b.RootElement.GetProperty("dir").GetString());
        Assert.Equal(JsonValueKind.Null, b.RootElement.GetProperty("sub").ValueKind); // -27 không có sub
        Assert.Equal("", b.RootElement.GetProperty("hex").GetString());
    }

    [Fact]
    public void Goi_dang_nhap_bi_che_va_mat_khau_khong_xuat_hien()
    {
        var w = new JavaStreamWriter();
        w.WriteByte(0);
        w.WriteUtf("tenthu"); w.WriteUtf("matkhau-bi-mat-123"); w.WriteUtf("2.5.0");
        w.WriteByte(1); w.WriteByte(2);
        var payload = w.ToArray();

        using (var sink = new CaptureSink(_dir))
            sink.Record(PacketDirection.ClientToServer, new Packet(-29, payload));

        string text = File.ReadAllText(Path.Combine(_dir, "packets.jsonl"));
        Assert.DoesNotContain("matkhau-bi-mat-123", text);
        Assert.DoesNotContain("tenthu", text);
        Assert.DoesNotContain(Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes("matkhau-bi-mat-123")), text);
        Assert.DoesNotContain(Convert.ToHexString(payload), text);

        using var doc = JsonDocument.Parse(text);
        Assert.True(doc.RootElement.GetProperty("masked").GetBoolean());
        Assert.Equal(payload.Length, doc.RootElement.GetProperty("len").GetInt32());
        Assert.Equal(-29, doc.RootElement.GetProperty("cmd").GetInt32());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("hex").ValueKind);
    }

    [Theory]
    [InlineData(PacketDirection.ClientToServer, -29, 0, true)]
    [InlineData(PacketDirection.ClientToServer, -29, 1, true)]   // đăng ký cũng mang mật khẩu
    [InlineData(PacketDirection.ClientToServer, -29, 2, false)]  // khai báo thông tin client
    [InlineData(PacketDirection.ServerToClient, -29, 0, false)]
    [InlineData(PacketDirection.ClientToServer, -28, 0, false)]
    public void Quy_tac_che(PacketDirection dir, int cmd, int sub, bool expected)
    {
        Assert.Equal(expected, CaptureSink.ShouldMask(dir, new Packet((sbyte)cmd, new byte[] { (byte)sub, 1, 2 })));
    }

    [Fact]
    public void Goi_29_rong_khong_bi_che_va_khong_loi()
    {
        Assert.False(CaptureSink.ShouldMask(PacketDirection.ClientToServer, new Packet(-29, new byte[0])));
    }

    [Fact]
    public void Sub_co_dau_va_chi_gan_cho_ma_lenh_co_sub()
    {
        using (var sink = new CaptureSink(_dir))
        {
            sink.Record(PacketDirection.ServerToClient, new Packet(-30, new byte[] { 0xFF }));
            sink.Record(PacketDirection.ServerToClient, new Packet(-24, new byte[] { 7 }));
        }
        var lines = Dong();
        using var a = JsonDocument.Parse(lines[0]);
        Assert.Equal(-1, a.RootElement.GetProperty("sub").GetInt32());
        using var b = JsonDocument.Parse(lines[1]);
        Assert.Equal(JsonValueKind.Null, b.RootElement.GetProperty("sub").ValueKind);
    }

    [Fact]
    public void Ghi_nhieu_luong_khong_hong_dong()
    {
        using (var sink = new CaptureSink(_dir))
        {
            Parallel.For(0, 200, i =>
                sink.Record(PacketDirection.ServerToClient, new Packet(5, new byte[] { (byte)i })));
        }
        var lines = Dong();
        Assert.Equal(200, lines.Length);
        Assert.All(lines, l => JsonDocument.Parse(l).Dispose());
    }

    [Fact]
    public void Tao_thu_muc_neu_chua_co()
    {
        var sub = Path.Combine(_dir, "a", "b");
        using (var sink = new CaptureSink(sub)) { }
        Assert.True(Directory.Exists(sub));
    }
}
