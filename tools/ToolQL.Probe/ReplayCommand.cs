using System.Text.Json;
using ToolQL.Core;
using ToolQL.Protocol;
using ToolQL.Protocol.Messages;

namespace ToolQL.Probe;

/// <summary>
/// `probe replay &lt;packets.jsonl&gt;`: chạy lại <see cref="GameState"/> trên tệp bắt gói, KHÔNG nối mạng và KHÔNG đăng nhập.
/// Báo số gói đã xử lý, gói chưa có bộ đọc, lỗi đọc, và phép kiểm id của gói <c>-90</c> với người chơi đang có trong map.
/// Mỗi gói <c>-27</c> từ server là một lần nối mới nên trạng thái được xóa.
/// </summary>
public static class ReplayCommand
{
    public static int Run(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine("Không thấy tệp: " + path);
            return 1;
        }

        var game = new GameState();
        int total = 0, skipped = 0, connections = 0, outfitKnown = 0, outfitUnknown = 0, parseErrors = 0;
        var unhandled = new Dictionary<int, int>();
        var errorKinds = new Dictionary<int, int>();
        var maps = new SortedDictionary<int, string>();
        var errorSamples = new Dictionary<int, string>();
        long lastVersion = game.Version;

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.GetProperty("dir").GetString() != "S->C" || root.GetProperty("masked").GetBoolean())
            {
                skipped++;
                continue;
            }

            sbyte command = (sbyte)root.GetProperty("cmd").GetInt32();
            byte[] payload = Convert.FromHexString(root.GetProperty("hex").GetString() ?? "");
            var packet = new Packet(command, payload);
            total++;

            if (command == -27)
            {
                connections++;
                parseErrors += game.Snapshot().ParseErrors;
                foreach (var (k, v) in game.Snapshot().UnhandledPackets) unhandled[k] = unhandled.GetValueOrDefault(k) + v;
                game.Reset();
                continue;
            }

            if (command == PlayerOutfitChanged.Command && payload.Length >= 5)
            {
                int id = (payload[1] << 24) | (payload[2] << 16) | (payload[3] << 8) | payload[4];
                if (game.Snapshot().Players.Any(p => p.Id == id)) outfitKnown++;
                else outfitUnknown++;
            }
            int errorsBefore = game.Snapshot().ParseErrors;
            game.Apply(packet);
            if (command == -24 && game.Snapshot().Map is { } m)
                maps[m.Id] = $"map {m.Id} \"{m.Name}\": bộ ô lát {m.TileSetId}";
            if (command == -28 && payload.Length > 0 && payload[0] == 10 && game.Snapshot().Map is { Tiles: { } tiles } g)
                maps[g.Id] = $"map {g.Id} \"{g.Name}\": bộ ô lát {g.TileSetId}, lưới {g.Width}x{g.Height}, mã ô lớn nhất {tiles.Max()}, số mã khác nhau {tiles.Distinct().Count()}, ô rỗng (0) {tiles.Count(t => t == 0)}/{tiles.Length}";
            if (game.Snapshot().ParseErrors > errorsBefore)
            {
                var key = GameState.Key(packet);
                errorKinds[key] = errorKinds.GetValueOrDefault(key) + 1;
                errorSamples.TryAdd(key, $"{payload.Length} byte, hex đầu {Convert.ToHexString(payload.AsSpan(0, Math.Min(payload.Length, 40)))}");
            }
        }

        var last = game.Snapshot();
        parseErrors += last.ParseErrors;
        foreach (var (k, v) in last.UnhandledPackets) unhandled[k] = unhandled.GetValueOrDefault(k) + v;

        Console.WriteLine($"Tệp: {path}");
        Console.WriteLine($"Gói S→C đã áp: {total} (bỏ qua {skipped} gói chiều C→S hoặc bị che), số lần nối: {connections}");
        Console.WriteLine($"Lỗi đọc gói (bộ đọc ném lỗi): {parseErrors}");
        foreach (var (key, count) in errorKinds)
            Console.WriteLine($"  lỗi ở {Describe(key)}: {count} gói; mẫu {errorSamples[key]}");
        foreach (var line in maps.Values) Console.WriteLine("  " + line);
        Console.WriteLine($"-90: id có trong map {outfitKnown}, id chưa biết {outfitUnknown}");
        Console.WriteLine($"Gói chưa có bộ đọc: {unhandled.Values.Sum()} gói, {unhandled.Count} loại (nhiều nhất trước):");
        foreach (var (key, count) in unhandled.OrderByDescending(p => p.Value).Take(30))
            Console.WriteLine($"  {Describe(key)}: {count}");
        return parseErrors == 0 ? 0 : 3;
    }

    /// <summary>Đọc các gói S→C (không bị che) của tệp bắt gói, theo thứ tự.</summary>
    public static IEnumerable<Packet> ReadServerPackets(string path) => CaptureReader.ReadServerPackets(path);

    /// <summary>Giải khóa đếm của <see cref="GameState.Key"/>: mã lệnh, hoặc <c>mã * 256 + sub</c> cho -28/-29/-30.</summary>
    private static string Describe(int key)
    {
        if (key is >= -128 and <= 127) return $"mã {key}";
        int command = (int)Math.Round(key / 256.0);
        return $"mã {command} sub {key - command * 256}";
    }
}
