using System.Text.Json;
using ToolQL.Protocol;

namespace ToolQL.Core;

/// <summary>Đọc tệp bắt gói <c>packets.jsonl</c> do <see cref="CaptureSink"/> ghi (xem docs/protocol/verification.md).</summary>
public static class CaptureReader
{
    /// <summary>Các gói S→C (không bị che) theo thứ tự trong tệp. Gói <c>-27</c> mở đầu mỗi lần nối.</summary>
    public static IEnumerable<Packet> ReadServerPackets(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.GetProperty("dir").GetString() != "S->C" || root.GetProperty("masked").GetBoolean()) continue;
            yield return new Packet((sbyte)root.GetProperty("cmd").GetInt32(), Convert.FromHexString(root.GetProperty("hex").GetString() ?? ""));
        }
    }

    /// <summary>Phát lại cả tệp vào một <see cref="GameState"/> mới; mỗi gói <c>-27</c> (lần nối mới) xóa trạng thái, nên kết quả là lần nối cuối.</summary>
    public static GameState Replay(string path)
    {
        var game = new GameState();
        foreach (var packet in ReadServerPackets(path))
        {
            if (packet.Command == -27) game.Reset();
            else game.Apply(packet);
        }
        return game;
    }
}
