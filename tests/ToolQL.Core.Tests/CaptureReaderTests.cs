namespace ToolQL.Core.Tests;

public class CaptureReaderTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "toolql-cap-" + Guid.NewGuid().ToString("N") + ".jsonl");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private static string Line(string dir, int cmd, string hex, bool masked = false) =>
        $"{{\"t\":\"2026-10-31T00:00:00+00:00\",\"dir\":\"{dir}\",\"cmd\":{cmd},\"sub\":null,\"len\":{hex.Length / 2},\"three\":false,\"masked\":{(masked ? "true" : "false")},\"hex\":\"{hex}\"}}";

    [Fact]
    public void Chi_lay_goi_server_gui_khong_bi_che_theo_thu_tu()
    {
        File.WriteAllLines(_path, new[]
        {
            Line("C->S", -29, "0102"),
            Line("S->C", -7, "00000007000A0014"),
            Line("S->C", -29, "", masked: true),
            "",
            Line("S->C", -6, "00000007"),
        });

        var packets = CaptureReader.ReadServerPackets(_path).ToArray();

        Assert.Equal(new sbyte[] { -7, -6 }, packets.Select(p => p.Command).ToArray());
        Assert.Equal(new byte[] { 0, 0, 0, 7 }, packets[1].Payload);
    }

    [Fact]
    public void Phat_lai_khi_gap_goi_27_thi_xoa_trang_thai_cua_lan_noi_truoc()
    {
        var map = Packets.Map(23);
        string mapHex = Convert.ToHexString(map);
        File.WriteAllLines(_path, new[]
        {
            Line("S->C", -27, "00"),
            Line("S->C", -24, mapHex),
            Line("S->C", -27, "00"),
        });

        Assert.Null(CaptureReader.Replay(_path).Snapshot().Map);   // lần nối cuối chưa có map

        File.WriteAllLines(_path, new[] { Line("S->C", -27, "00"), Line("S->C", -24, mapHex) });
        Assert.Equal(23, CaptureReader.Replay(_path).Snapshot().Map!.Id);
    }
}
