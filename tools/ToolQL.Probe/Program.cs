using System.Net.Sockets;
using System.Text;
using ToolQL.Protocol;

namespace ToolQL.Probe;

/// <summary>
/// Chương trình thử giao thức (cột mốc 0). Mỗi lệnh dừng gọn, không tự nối lại.
/// Mã thoát: 0 = ổn, 1 = dùng sai, 2 = không nối được, 3 = lỗi giao thức (login: 4 captcha, 5 bị từ chối, 6 chưa tới map, 7 cần tạo nhân vật).
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public static class Program
{
    private const int ExitOk = 0, ExitUsage = 1, ExitConnect = 2, ExitProtocol = 3;
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ListenTime = TimeSpan.FromSeconds(5);

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length == 0) return Usage();

        switch (args[0])
        {
            case "servers":
                return ListServers();
            case "handshake" when args.Length == 2:
                return Handshake(args[1]);
            case "data" when args.Length == 2:
                return DataCommand.Run(args[1]);
            case "images" when args.Length >= 2:
                return ImagesCommand.Run(args[1], args.Skip(2).Contains("--xac-nhan"));
            case "mob" when args.Length == 2:
                return MobCommand.Run(args[1]);
            case "import-test-account" when args.Length == 2:
                return ImportTestAccountCommand.Run(args[1]);
            case "render" when args.Length is 4 or 5 && (args.Length == 4 || args[4] == "--khung"):
                return RenderCommand.Run(args[1], args[2], args[3], args.Length == 5);
            case "replay" when args.Length == 2:
                return ReplayCommand.Run(args[1]);
            case "login" when args.Length >= 2 && TryParseLoginFlags(args, out bool confirmed, out int listen):
                return LoginCommand.Run(args[1], confirmed, listen);
            default:
                return Usage();
        }
    }

    /// <summary>Cờ của `login`: `--xac-nhan` và `--nghe <giây>` (0-300, nghe thụ động sau khi nhận lưới).</summary>
    private static bool TryParseLoginFlags(string[] args, out bool confirmed, out int listen)
    {
        confirmed = false; listen = 0;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--xac-nhan") confirmed = true;
            else if (args[i] == "--nghe" && i + 1 < args.Length && int.TryParse(args[++i], out listen) && listen is >= 0 and <= 300) { }
            else return false;
        }
        return true;
    }

    private static int Usage()
    {
        Console.WriteLine("Cách dùng:");
        Console.WriteLine("  probe servers                 In danh sách server");
        Console.WriteLine("  probe handshake <server>      Nối, bắt tay khóa, nghe 5 giây, ngắt (KHÔNG đăng nhập)");
        Console.WriteLine("  probe data <data-0.vN.bin>    Đọc bảng đồ họa NRdata đã lưu trong kho (offline, không nối mạng)");
        Console.WriteLine("  probe images <server> --xac-nhan   ĐĂNG NHẬP THẬT một lần, xin vài ảnh (-67, 11, -32) và lưu ra ngoài repo");
        Console.WriteLine("  probe mob <mob-N.bin>         Đọc gói sprite quái (11) đã lưu (offline, không nối mạng)");
        Console.WriteLine("  probe render <packets.jsonl> <client.jar> <ra.png> [--khung]   Vẽ map từ tệp bắt gói + ô lát trong jar (offline)");
        Console.WriteLine("  probe replay <packets.jsonl>  Chạy lại bộ đọc trên tệp bắt gói (offline, không nối mạng)");
        Console.WriteLine("  probe login <server> --xac-nhan   ĐĂNG NHẬP THẬT bằng acc thử (chỉ chạy khi chủ repo đã đồng ý)");
        Console.WriteLine("      thêm --nghe <giây> để nghe thụ động thêm (tối đa 300) sau khi nhận lưới; không gửi gì");
        Console.WriteLine("<server> là số thứ tự, tên (ví dụ \"Vũ trụ 1\") hoặc tên miền.");
        return ExitUsage;
    }

    private static int ListServers()
    {
        for (int i = 0; i < ServerList.All.Count; i++)
        {
            var s = ServerList.All[i];
            Console.WriteLine($"{i + 1,2}. {s.Name,-20} {s.Host}:{s.Port}  (IP dự phòng {s.FallbackIp})");
        }
        return ExitOk;
    }

    /// <summary>Giai đoạn 1: chỉ nối và nhận khóa, không đăng nhập.</summary>
    private static int Handshake(string query)
    {
        var server = ServerList.Find(query);
        if (server is null)
        {
            Console.WriteLine($"Không tìm thấy server \"{query}\". Chạy `probe servers` để xem danh sách.");
            return ExitUsage;
        }

        Console.WriteLine($"Nối tới {server.Name} ({server.Host}:{server.Port}) ...");
        using var client = new TcpClient();
        try
        {
            using var cts = new CancellationTokenSource(ConnectTimeout);
            client.ConnectAsync(server.Host, server.Port, cts.Token).AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            Console.WriteLine($"Không nối được: {(ex is OperationCanceledException ? "quá thời gian chờ" : ex.Message)}");
            return ExitConnect;
        }

        using var sink = CaptureSink.CreateDefault();
        Console.WriteLine($"Đã nối. Lưu gói thô tại: {sink.Directory}");

        var stream = client.GetStream();
        var writer = new PacketWriter(stream);
        var reader = new PacketReader(stream);

        try
        {
            var info = KeyHandshake.Perform(writer, reader, sink.Record);
            Console.WriteLine("Bắt tay khóa thành công:");
            Console.WriteLine($"  độ dài khóa : {info.RawKey.Length} byte");
            Console.WriteLine($"  chuỗi UTF   : \"{info.Text}\"");
            Console.WriteLine($"  số int      : {info.Number}");
            Console.WriteLine($"  byte cờ     : {info.Flag}");
            Console.WriteLine($"  byte thừa   : {info.ExtraBytes}{(info.ExtraBytes != 0 ? "  <-- bố cục tài liệu chưa đủ" : "")}");
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or IOException)
        {
            Console.WriteLine($"Bắt tay khóa thất bại: {ex.Message}");
            return ExitProtocol;
        }

        Listen(reader, stream, sink);
        Console.WriteLine("Ngắt kết nối.");
        return ExitOk;
    }

    /// <summary>Nhận thêm gói trong 5 giây (chưa gửi gì), in tóm tắt từng gói.</summary>
    private static void Listen(PacketReader reader, NetworkStream stream, CaptureSink sink)
    {
        Console.WriteLine($"Nghe thêm {ListenTime.TotalSeconds:0} giây ...");
        var deadline = DateTime.UtcNow + ListenTime;
        int count = 0;
        while (true)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) break;
            stream.ReadTimeout = Math.Max(1, (int)remaining.TotalMilliseconds);
            try
            {
                var packet = reader.Read();
                sink.Record(PacketDirection.ServerToClient, packet);
                count++;
                string sub = packet.Payload.Length > 0 && Array.IndexOf(new sbyte[] { -29, -28, -30 }, packet.Command) >= 0
                    ? $" sub {(sbyte)packet.Payload[0]}" : "";
                Console.WriteLine($"  nhận mã {packet.Command}{sub}, {packet.Payload.Length} byte{(packet.UsedThreeByteLength ? " (độ dài 3 byte)" : "")}");
            }
            catch (IOException ex) when (ex.InnerException is SocketException { SocketErrorCode: SocketError.TimedOut })
            {
                break; // hết giờ nghe
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                Console.WriteLine($"  server ngắt kết nối hoặc lỗi đọc: {ex.Message}");
                break;
            }
        }
        if (count == 0) Console.WriteLine("  (không nhận thêm gói nào)");
    }
}
