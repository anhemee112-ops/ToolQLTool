using System.Text;
using ToolQL.Core;
using ToolQL.Protocol;
using ToolQL.Protocol.Resources;

namespace ToolQL.Probe;

/// <summary>
/// <c>probe images &lt;server&gt; --xac-nhan</c>: ĐĂNG NHẬP THẬT bằng acc thử MỘT lần, vào game, rồi xin một ít ảnh như client
/// thật làm khi vào map: <c>-67</c> (ảnh nhỏ, int id), <c>11</c> (gói sprite quái, short loại), <c>-32</c> (ảnh nền, short id).
/// Lưu gói trả về ra <c>%USERPROFILE%\.toolql\images\&lt;ngày-giờ&gt;\</c> (ngoài repo) và in bảng đối chiếu với
/// <c>NR_image</c>. Mục đích: kiểm khung 3 byte của <c>-67</c>/<c>-32</c>/<c>11</c> và định dạng ảnh (cột mốc R1).
/// Dừng khi captcha hoặc bị từ chối; không tự thử lại; tổng cộng khoảng 1 phút.
/// </summary>
public static class ImagesCommand
{
    private const int MaxSmallImages = 30;
    private static readonly TimeSpan LoginWait = TimeSpan.FromSeconds(40);
    private static readonly TimeSpan ReplyWait = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan Spacing = TimeSpan.FromMilliseconds(150);
    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private sealed record Reply(string Kind, int Id, int PayloadLength, bool IsPng, int Width, int Height, string Note);

    public static int Run(string serverQuery, bool confirmed)
    {
        var server = ServerList.Find(serverQuery);
        if (server is null)
        {
            Console.WriteLine($"Không tìm thấy server \"{serverQuery}\". Chạy `probe servers` để xem danh sách.");
            return 1;
        }
        if (!confirmed)
        {
            Console.WriteLine("Lệnh này ĐĂNG NHẬP THẬT một lần bằng acc thử rồi xin khoảng 30 ảnh nhỏ, 1 sprite quái, 1 ảnh nền.");
            Console.WriteLine($"  server : {server.Name} ({server.Host}:{server.Port})");
            Console.WriteLine("  rủi ro : tự động hóa trên server chính thức có thể bị khóa acc; chỉ một acc, một lượt, không tự nối lại.");
            Console.WriteLine("Chưa đọc file tài khoản và chưa nối mạng. Chủ repo đồng ý rồi mới chạy lại với --xac-nhan.");
            return 1;
        }

        TestAccount account;
        try
        {
            account = TestAccount.Load();
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException)
        {
            Console.WriteLine(ex.Message);
            return 1;
        }
        Console.WriteLine($"Tài khoản thử: {account.User} (mật khẩu không in ra). Server: {server.Name}.");

        string outDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".toolql", "images", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(outDir);
        using var sink = CaptureSink.CreateDefault();
        Console.WriteLine($"Gói thô: {sink.Directory}\nẢnh nhận về: {outDir}");

        var replies = new List<Reply>();
        var gate = new object();
        var options = new SessionOptions { Cache = new DataCache(DataCache.DefaultDirectory), Tap = sink.Record };
        using var session = new AccountSession(
            new AccountCredentials(account.User, account.Password), TcpConnector.Create(server.Host, server.Port), options);

        var terminal = new ManualResetEventSlim();
        session.StateChanged += (state, message) =>
        {
            Console.WriteLine($"trạng thái: {state}{(message is null ? "" : " - " + message)}");
            if (state is SessionState.NeedCaptcha or SessionState.Rejected or SessionState.NeedCharacter
                or SessionState.Faulted or SessionState.Stopped or SessionState.Reconnecting) terminal.Set();
        };
        session.PacketReceived += packet =>
        {
            Reply? reply = packet.Command switch
            {
                -67 => ParseSmall(packet.Payload, outDir),
                -32 => ParseBackground(packet.Payload, outDir),
                11 => ParseMobPack(packet.Payload, outDir),
                _ => null,
            };
            if (reply != null) lock (gate) replies.Add(reply);
        };

        session.Start();
        var loginDeadline = DateTime.UtcNow + LoginWait;
        while (session.State != SessionState.InGame && !terminal.IsSet && DateTime.UtcNow < loginDeadline) Thread.Sleep(100);
        if (session.State != SessionState.InGame)
        {
            Console.WriteLine($"Không vào được game (trạng thái {session.State}). Dừng, không thử lại.");
            return session.State == SessionState.NeedCaptcha ? 4 : session.State == SessionState.Rejected ? 5 : 6;
        }

        var snapshot = session.Game.Snapshot();
        var (images, parts) = LoadTables();
        var wanted = ChooseSmallIds(snapshot, parts, out string choice);
        Console.WriteLine(choice);

        int requested = 0;
        foreach (int id in wanted)
        {
            var w = new JavaStreamWriter();
            w.WriteInt(id);
            if (session.TrySend(-67, w.ToArray())) requested++;
            Thread.Sleep(Spacing);
        }
        int mobRequested = 0, bgRequested = 0;
        if (snapshot.Mobs.Count > 0)
        {
            var w = new JavaStreamWriter();
            w.WriteShort(snapshot.Mobs[0].Info.Type);
            if (session.TrySend(11, w.ToArray())) mobRequested = 1;
            Console.WriteLine($"Xin sprite quái loại {snapshot.Mobs[0].Info.Type} (gói 11).");
            Thread.Sleep(Spacing);
        }
        if (snapshot.Map is { Backgrounds.Length: > 0 } map)
        {
            var w = new JavaStreamWriter();
            w.WriteShort(map.Backgrounds[0].Id);
            if (session.TrySend(-32, w.ToArray())) bgRequested = 1;
            Console.WriteLine($"Xin ảnh nền id {map.Backgrounds[0].Id} (gói -32).");
        }
        Console.WriteLine($"Đã gửi: {requested} gói -67, {mobRequested} gói 11, {bgRequested} gói -32. Chờ trả lời tối đa {ReplyWait.TotalSeconds:0} giây ...");

        int expected = requested + mobRequested + bgRequested;
        var replyDeadline = DateTime.UtcNow + ReplyWait;
        while (DateTime.UtcNow < replyDeadline && !terminal.IsSet)
        {
            lock (gate) if (replies.Count >= expected) break;
            Thread.Sleep(100);
        }

        var after = session.Game.Snapshot();
        bool reconnected = session.State != SessionState.InGame;
        session.Stop();

        Print(replies, images, expected, after.ParseErrors, reconnected);
        return 0;
    }

    private static (ImageTable? Images, PartTable? Parts) LoadTables()
    {
        var cache = new DataCache(DataCache.DefaultDirectory);
        if (cache.StoredVersion("data-0") is not { } version || cache.TryLoad("data-0", version) is not { } raw) return (null, null);
        try
        {
            var data = NrData.Parse(raw);
            return (ImageTable.Parse(data.Image), PartTable.Parse(data.Part));
        }
        catch (InvalidDataException)
        {
            return (null, null);
        }
    }

    /// <summary>Id atlas 0-4, rồi mã sprite của bộ đầu/thân/chân của người chơi đầu tiên trong map (hoặc 252-254 đã thấy ở gói -90).</summary>
    private static List<int> ChooseSmallIds(GameSnapshot snapshot, PartTable? parts, out string explanation)
    {
        var ids = new List<int> { 0, 1, 2, 3, 4 };
        var sb = new StringBuilder("Xin -67 cho id 0-4");
        if (parts != null)
        {
            var sets = snapshot.Players.Count > 0
                ? new[] { snapshot.Players[0].Info.Head, snapshot.Players[0].Info.Body, snapshot.Players[0].Info.Leg }
                : new short[] { 252, 253, 254 };
            sb.Append(snapshot.Players.Count > 0 ? $", rồi mã sprite của bộ phần {string.Join("/", sets)} (người chơi trong map)"
                                                  : ", rồi mã sprite của bộ phần 252/253/254 (chưa có người trong map)");
            foreach (var set in sets)
            {
                if (set < 0 || set >= parts.Sets.Length) continue;
                foreach (var part in parts.Sets[set].Parts)
                    if (!ids.Contains(part.Sprite) && ids.Count < MaxSmallImages) ids.Add(part.Sprite);
            }
        }
        explanation = sb + $" (tổng {ids.Count} id).";
        return ids;
    }

    private static Reply ParseSmall(byte[] payload, string dir)
    {
        // Mô tả: int id, int độDài, rồi độDài byte PNG.
        if (payload.Length < 8) return new Reply("-67", -1, payload.Length, false, 0, 0, "payload < 8 byte");
        int id = ReadInt(payload, 0), length = ReadInt(payload, 4);
        bool lengthOk = length == payload.Length - 8;
        var png = payload.AsSpan(8);
        File.WriteAllBytes(Path.Combine(dir, $"small-{id}.bin"), payload);
        var (isPng, w, h) = Inspect(png);
        return new Reply("-67", id, payload.Length, isPng, w, h, lengthOk ? "độ dài khớp" : $"độ dài ghi {length}, thực còn {payload.Length - 8}");
    }

    private static Reply ParseBackground(byte[] payload, string dir)
    {
        // Mô tả: short id, int độDài, rồi PNG.
        if (payload.Length < 6) return new Reply("-32", -1, payload.Length, false, 0, 0, "payload < 6 byte");
        int id = (short)((payload[0] << 8) | payload[1]), length = ReadInt(payload, 2);
        File.WriteAllBytes(Path.Combine(dir, $"bg-{id}.bin"), payload);
        var (isPng, w, h) = Inspect(payload.AsSpan(6));
        return new Reply("-32", id, payload.Length, isPng, w, h, length == payload.Length - 6 ? "độ dài khớp" : $"độ dài ghi {length}, thực còn {payload.Length - 6}");
    }

    private static Reply ParseMobPack(byte[] payload, string dir)
    {
        // Mô tả: short loạiQuái, byte kiểu, khối dữ liệu (int + byte), khối PNG (int + byte), byte bD ...
        if (payload.Length < 11) return new Reply("11", -1, payload.Length, false, 0, 0, "payload < 11 byte");
        int type = (short)((payload[0] << 8) | payload[1]);
        File.WriteAllBytes(Path.Combine(dir, $"mob-{type}.bin"), payload);
        int dataLength = ReadInt(payload, 3);
        if (dataLength < 0 || 7 + dataLength + 4 > payload.Length)
            return new Reply("11", type, payload.Length, false, 0, 0, $"khối dữ liệu dài {dataLength} vượt payload");
        int pngLength = ReadInt(payload, 7 + dataLength);
        int pngStart = 7 + dataLength + 4;
        if (pngLength < 0 || pngStart + pngLength > payload.Length)
            return new Reply("11", type, payload.Length, false, 0, 0, $"khối PNG dài {pngLength} vượt payload");
        var (isPng, w, h) = Inspect(payload.AsSpan(pngStart, pngLength));
        int rest = payload.Length - (pngStart + pngLength);
        return new Reply("11", type, payload.Length, isPng, w, h, $"kiểu {payload[2]}, khối dữ liệu {dataLength} byte, PNG {pngLength} byte, còn {rest} byte sau PNG");
    }

    private static (bool IsPng, int Width, int Height) Inspect(ReadOnlySpan<byte> data)
    {
        if (data.Length < 24 || !data[..PngMagic.Length].SequenceEqual(PngMagic)) return (false, 0, 0);
        return (true, ReadInt(data, 16), ReadInt(data, 20));
    }

    private static int ReadInt(ReadOnlySpan<byte> b, int at) => (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];

    private static void Print(List<Reply> replies, ImageTable? images, int expected, int parseErrors, bool lostConnection)
    {
        Console.WriteLine();
        Console.WriteLine($"Nhận {replies.Count}/{expected} trả lời; lỗi đọc gói của GameState: {parseErrors}; mất kết nối giữa chừng: {(lostConnection ? "CÓ" : "không")}.");
        foreach (var r in replies.OrderBy(r => r.Kind).ThenBy(r => r.Id))
        {
            string size = r.IsPng ? $"PNG {r.Width}x{r.Height}" : "KHÔNG phải PNG";
            string table = "";
            if (r.Kind == "-67" && images != null && r.Id >= 0 && r.Id < images.Sprites.Length)
            {
                var s = images.Sprites[r.Id];
                // Ảnh tải về là sprite đã cắt sẵn: so kích thước PNG với rộng×cao trong bảng (x, y, atlas là toạ độ trong atlas, không dùng ở đây).
                bool same = r.IsPng && s.Width == r.Width && s.Height == r.Height;
                table = $" | bảng NR_image: {s.Width}x{s.Height}{(r.IsPng ? (same ? ", KHỚP kích thước ảnh" : ", LỆCH kích thước ảnh") : "")}";
            }
            else if (r.Kind == "-67") table = " | ngoài bảng NR_image";
            Console.WriteLine($"  {r.Kind} id {r.Id}: {r.PayloadLength} byte, {size}; {r.Note}{table}");
        }
    }
}
