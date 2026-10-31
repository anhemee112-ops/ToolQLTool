using System.Text;
using ToolQL.Core;
using ToolQL.Probe;
using ToolQL.Protocol;

namespace ToolQL.Host;

/// <summary>
/// Chương trình dòng lệnh tạm của M1: giữ MỘT acc bằng <see cref="AccountSession"/> và in trạng thái game định kỳ.
/// Không có giao diện, không vẽ, không auto. Dừng khi hết giờ, Ctrl+C, hoặc phiên vào trạng thái cuối
/// (captcha, bị từ chối, thiếu nhân vật, lỗi bộ đọc). Không bao giờ in mật khẩu.
/// Mã thoát: 0 = chạy hết giờ ổn, 1 = dùng sai, 4 = cần captcha, 5 = bị từ chối, 6 = hết giờ mà chưa ở trong game
/// (ví dụ đang nối lại), 7 = cần tạo nhân vật, 8 = lỗi bộ đọc.
/// </summary>
public static class Program
{
    private static readonly TimeSpan PrintEvery = TimeSpan.FromSeconds(5);

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (!TryParse(args, out var o)) return Usage();

        var server = ServerList.Find(o.Server);
        if (server is null)
        {
            Console.WriteLine($"Không tìm thấy server \"{o.Server}\". Chạy `probe servers` để xem danh sách.");
            return 1;
        }

        if (!o.Confirmed)
        {
            Console.WriteLine("Lệnh này ĐĂNG NHẬP THẬT và GIỮ kết nối bằng acc thử.");
            Console.WriteLine($"  server : {server.Name} ({server.Host}:{server.Port})");
            Console.WriteLine($"  thời gian giữ: {o.Minutes} phút");
            Console.WriteLine("  rủi ro : tự động hóa trên server chính thức có thể bị khóa acc; mỗi lần chỉ một acc.");
            Console.WriteLine("  dừng   : captcha (-85) hoặc bị từ chối thì dừng, không thử lại. Mất mạng thì nối lại sau tối thiểu 40 giây, chờ tăng dần.");
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

        CaptureSink? sink = o.Capture ? CaptureSink.CreateDefault() : null;
        if (sink != null) Console.WriteLine("Lưu gói thô tại: " + sink.Directory);

        var cache = new DataCache(DataCache.DefaultDirectory);
        var options = new SessionOptions { Cache = cache, Tap = sink is null ? null : sink.Record };
        using var session = new AccountSession(
            new AccountCredentials(account.User, account.Password), TcpConnector.Create(server.Host, server.Port), options);

        var started = DateTime.UtcNow;
        var done = new ManualResetEventSlim();
        SessionState? terminal = null;
        session.StateChanged += (state, message) =>
        {
            Console.WriteLine($"[{Elapsed(started)}] trạng thái: {state}{(message is null ? "" : " - " + message)}");
            if (state is SessionState.NeedCaptcha or SessionState.Rejected or SessionState.NeedCharacter
                or SessionState.Faulted or SessionState.Stopped)
            {
                terminal = state;
                done.Set();
            }
        };
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("Ctrl+C: dừng phiên ...");
            done.Set();
        };

        session.Start();
        var deadline = started + TimeSpan.FromMinutes(o.Minutes);
        while (!done.IsSet && DateTime.UtcNow < deadline)
        {
            done.Wait(PrintEvery);
            if (session.State == SessionState.InGame) PrintStatus(started, session);
        }

        bool timedOut = !done.IsSet;
        var final = session.Game.Snapshot();
        var finalState = session.State;
        session.Stop();

        Console.WriteLine();
        Console.WriteLine(timedOut
            ? $"Hết giờ ({o.Minutes} phút), trạng thái cuối {finalState}."
            : $"Dừng sớm sau {Elapsed(started)}, trạng thái cuối {terminal ?? finalState}.");
        PrintSummary(final, cache);
        sink?.Dispose();

        if (timedOut) return finalState == SessionState.InGame ? 0 : 6;
        return terminal switch
        {
            SessionState.NeedCaptcha => 4,
            SessionState.Rejected => 5,
            SessionState.NeedCharacter => 7,
            SessionState.Faulted => 8,
            _ => 0,
        };
    }

    private static void PrintStatus(DateTime started, AccountSession session)
    {
        var s = session.Game.Snapshot();
        var me = s.Me;
        int aliveMobs = s.Mobs.Count(m => m.Alive);
        Console.WriteLine(
            $"[{Elapsed(started)}] map {(s.Map is null ? "?" : $"{s.Map.Id} \"{s.Map.Name}\"")}" +
            $" | mình {(me is null ? "?" : $"{me.Name} ({me.X},{me.Y}) HP {me.Hp}")}" +
            $" | quái {aliveMobs}/{s.Mobs.Count} | người {s.Players.Count} | vật rơi {s.Items.Count}" +
            $" | gói chưa đọc {s.UnhandledPackets.Values.Sum()} | lỗi đọc {s.ParseErrors}");
    }

    private static void PrintSummary(GameSnapshot s, DataCache cache)
    {
        Console.WriteLine($"Lỗi đọc gói: {s.ParseErrors}. Bảng dữ liệu đã lưu ở: {cache.Directory}");
        if (s.UnhandledPackets.Count == 0) return;
        Console.WriteLine("Gói chưa có bộ đọc (mã: số lần), nhiều nhất trước:");
        foreach (var (key, count) in s.UnhandledPackets.OrderByDescending(p => p.Value).Take(25))
            Console.WriteLine($"  {Describe(key)}: {count}");
    }

    /// <summary>Giải khóa đếm của <see cref="GameState.Key"/>: mã lệnh, hoặc <c>mã * 256 + sub</c> cho -28/-29/-30.</summary>
    private static string Describe(int key)
    {
        if (key is >= -128 and <= 127) return $"mã {key}";
        int command = (int)Math.Round(key / 256.0);
        return $"mã {command} sub {key - command * 256}";
    }

    private static string Elapsed(DateTime started) => (DateTime.UtcNow - started).ToString(@"mm\:ss");

    private sealed record Options(string Server, bool Confirmed, int Minutes, bool Capture);

    /// <summary>`host &lt;server&gt; [--xac-nhan] [--phut N] [--bat-goi]`; N từ 1 đến 120, mặc định 10.</summary>
    private static bool TryParse(string[] args, out Options options)
    {
        options = new Options("", false, 10, false);
        if (args.Length == 0) return false;
        string server = args[0];
        bool confirmed = false, capture = false;
        int minutes = 10;
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--xac-nhan": confirmed = true; break;
                case "--bat-goi": capture = true; break;
                case "--phut" when i + 1 < args.Length && int.TryParse(args[++i], out minutes) && minutes is >= 1 and <= 120: break;
                default: return false;
            }
        }
        options = new Options(server, confirmed, minutes, capture);
        return true;
    }

    private static int Usage()
    {
        Console.WriteLine("Cách dùng: host <server> [--xac-nhan] [--phut N] [--bat-goi]");
        Console.WriteLine("  <server>    số thứ tự, tên (ví dụ \"Vũ trụ 1\") hoặc tên miền (xem `probe servers`)");
        Console.WriteLine("  --xac-nhan  ĐĂNG NHẬP THẬT bằng acc thử (chỉ khi chủ repo đã đồng ý); thiếu cờ này chỉ in cảnh báo");
        Console.WriteLine("  --phut N    giữ kết nối N phút (1-120, mặc định 10)");
        Console.WriteLine("  --bat-goi   lưu gói thô vào ~/.toolql/captures (gói đăng nhập được che)");
        return 1;
    }
}
