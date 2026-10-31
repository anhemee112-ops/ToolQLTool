using SkiaSharp;
using ToolQL.Core;
using ToolQL.Protocol;
using ToolQL.Render;

namespace ToolQL.Probe;

/// <summary>
/// `probe render &lt;packets.jsonl&gt; &lt;client.jar&gt; &lt;ra.png&gt; [--khung]`: dựng lại <see cref="GameState"/> từ tệp bắt gói (lần nối cuối),
/// lấy bộ ô lát từ jar của client và vẽ cả map ra một ảnh PNG bằng <see cref="MapRenderer"/>. KHÔNG nối mạng.
/// Có <c>--khung</c> thì vẽ khung nhìn 640×360 theo nhân vật (camera của client) thay vì cả map.
/// /// Thực thể mới là ký hiệu (khung màu + tên), chưa phải sprite.
/// </summary>
public static class RenderCommand
{
    public static int Run(string capturePath, string jarPath, string outPath, bool followCharacter = false)
    {
        if (!File.Exists(capturePath))
        {
            Console.WriteLine("Không thấy tệp bắt gói: " + capturePath);
            return 1;
        }

        var game = new GameState();
        foreach (var packet in ReplayCommand.ReadServerPackets(capturePath))
        {
            if (packet.Command == -27) game.Reset();   // lần nối mới
            else game.Apply(packet);
        }
        var snapshot = game.Snapshot();
        if (snapshot.Map is null)
        {
            Console.WriteLine("Tệp bắt gói chưa có gói map (-24) trong lần nối cuối.");
            return 3;
        }

        ClientAssets assets;
        try
        {
            assets = ClientAssets.OpenJar(jarPath);
        }
        catch (FileNotFoundException ex)
        {
            Console.WriteLine(ex.Message);
            return 1;
        }

        using (assets)
        {
            using var tiles = assets.LoadTileSet(snapshot.Map.TileSetId);
            Console.WriteLine($"Map {snapshot.Map.Id} \"{snapshot.Map.Name}\", lưới {snapshot.Map.Width}x{snapshot.Map.Height}, bộ ô lát {snapshot.Map.TileSetId}: " +
                              (tiles is null ? "KHÔNG có trong jar (bộ này lấy từ server), vẽ không có ô lát" : $"{tiles.Count} ô"));
            Console.WriteLine($"Thực thể: {snapshot.Mobs.Count(m => m.Alive)} quái sống, {snapshot.Map.Npcs.Length} NPC, {snapshot.Players.Count} người chơi, {(snapshot.Me is null ? "không có" : "có")} nhân vật mình.");

            using var bitmap = MapRenderer.Render(snapshot, tiles, followCharacter ? new Viewport(640, 360) : null);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
            File.WriteAllBytes(outPath, data.ToArray());
            Console.WriteLine($"Đã ghi {bitmap.Width}x{bitmap.Height} → {outPath}");
        }
        return 0;
    }
}
