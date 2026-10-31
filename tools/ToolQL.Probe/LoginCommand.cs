using System.Net.Sockets;
using ToolQL.Protocol;
using ToolQL.Protocol.Messages;

namespace ToolQL.Probe;

/// <summary>
/// <c>probe login &lt;server&gt; --xac-nhan</c>: giai đoạn 2-3 của cột mốc 0. ĐĂNG NHẬP THẬT bằng acc thử.
/// Chỉ gửi gói tối thiểu (đăng nhập, xin bảng lệch, báo tải xong, xin lưới ô lát), ghi phản hồi, không thử gói lạ,
/// không tự nối lại. Gặp -85 (captcha) hoặc bị từ chối thì dừng.
/// </summary>
public static class LoginCommand
{
    // Mã thoát riêng (xem Program).
    public const int ExitOk = 0, ExitUsage = 1, ExitConnect = 2, ExitProtocol = 3, ExitCaptcha = 4, ExitRejected = 5, ExitIncomplete = 6, ExitNeedCharacter = 7;

    private const string ProtocolVersion = "2.5.0";
    // Hai byte cuối của gói đăng nhập: ý nghĩa chưa rõ (login.md: "bg[6]/cờ" và "mã ngôn ngữ?"). Dùng 0, 0.
    private const int UnknownByte4 = 0, UnknownByte5 = 0;

    private static readonly TimeSpan Overall = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan GridWait = TimeSpan.FromSeconds(8);

    /// <summary>Payload -29 sub 2; dùng chung với lõi (<see cref="ToolQL.Core.ClientInfo"/>) để hai bên không lệch nhau.</summary>
    public static byte[] BuildClientInfo() => ToolQL.Core.ClientInfo.Build();

    public static int Run(string serverQuery, bool confirmed, int listenSeconds = 0)
    {
        var server = ServerList.Find(serverQuery);
        if (server is null)
        {
            Console.WriteLine($"Không tìm thấy server \"{serverQuery}\". Chạy `probe servers` để xem danh sách.");
            return ExitUsage;
        }

        if (!confirmed)
        {
            Console.WriteLine("Lệnh này ĐĂNG NHẬP THẬT lên server bằng tài khoản thử.");
            Console.WriteLine($"  server : {server.Name} ({server.Host}:{server.Port})");
            Console.WriteLine("  rủi ro : tự động hóa trên server chính thức có thể bị khóa acc; mỗi lần chỉ một acc.");
            Console.WriteLine("  dừng   : server từ chối, gửi captcha (-85) hoặc ngắt thì dừng, không thử lại.");
            Console.WriteLine("Chưa đọc file tài khoản và chưa nối mạng. Chủ repo đồng ý rồi mới chạy lại với --xac-nhan.");
            return ExitUsage;
        }

        TestAccount account;
        try
        {
            account = TestAccount.Load();
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException)
        {
            Console.WriteLine(ex.Message);
            return ExitUsage;
        }

        Console.WriteLine($"Tài khoản thử: {account.User} (mật khẩu không in ra).");
        if (account.Server is { } wanted && ServerList.Find(wanted) is { } expected && expected != server)
            Console.WriteLine($"  Cảnh báo: file ghi server \"{wanted}\" nhưng đang nối \"{server.Name}\".");

        Console.WriteLine($"Nối tới {server.Name} ({server.Host}:{server.Port}) ...");
        using var client = new TcpClient();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
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
        var session = new Session(writer, reader, stream, sink, TimeSpan.FromSeconds(listenSeconds));

        try
        {
            KeyHandshake.Perform(writer, reader, sink.Record);
            Console.WriteLine("Bắt tay khóa xong.");

            // Client thật gửi -29 sub 2 (setType) ngay khi nối xong, TRƯỚC đăng nhập (an_0.dF).
            session.Send(-29, BuildClientInfo());
            Console.WriteLine("Đã gửi thông tin client (-29 sub 2).");

            var login = new JavaStreamWriter();
            login.WriteSByte(0);
            login.WriteUtf(account.User);
            login.WriteUtf(account.Password);
            login.WriteUtf(ProtocolVersion);
            login.WriteSByte(UnknownByte4);
            login.WriteSByte(UnknownByte5);
            session.Send(-29, login.ToArray());
            Console.WriteLine("Đã gửi gói đăng nhập (-29 sub 0). Chờ phản hồi ...");

            return session.Receive();
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or IOException)
        {
            Console.WriteLine($"Lỗi giao thức hoặc mất kết nối: {ex.Message}");
            session.PrintSummary();
            return ExitProtocol;
        }
        finally
        {
            Console.WriteLine("Ngắt kết nối.");
        }
    }

    /// <summary>Trạng thái một lần chạy: gửi, nhận, theo dõi bảng đã xin và tóm tắt bộ đọc.</summary>
    private sealed class Session
    {
        private readonly PacketWriter _writer;
        private readonly PacketReader _reader;
        private readonly NetworkStream _stream;
        private readonly CaptureSink _sink;
        private readonly List<string> _summary = new();
        private bool _sentDone;
        private bool _gotMap;
        private readonly TimeSpan _listen;
        private DateTime _gridDeadline = DateTime.MaxValue;
        private bool _gridDone;
        private bool _sentFinishMap;

        public Session(PacketWriter writer, PacketReader reader, NetworkStream stream, CaptureSink sink, TimeSpan listen)
        {
            _writer = writer; _reader = reader; _stream = stream; _sink = sink; _listen = listen;
        }

        public void Send(sbyte command, byte[] payload)
        {
            _writer.Write(command, payload);
            _sink.Record(PacketDirection.ClientToServer, new Packet(command, payload));
        }

        public int Receive()
        {
            var start = DateTime.UtcNow;
            int count = 0;
            while (true)
            {
                var now = DateTime.UtcNow;
                if (_gotMap && (_gridDone || now >= _gridDeadline)) return Finish(_listen > TimeSpan.Zero ? Listen() : ExitOk);
                var left = start + Overall - now;
                if (left <= TimeSpan.Zero)
                {
                    Console.WriteLine("Hết 60 giây.");
                    return Finish(_gotMap ? ExitOk : ExitIncomplete);
                }
                var wait = left < Idle ? left : Idle;
                if (_gotMap && _gridDeadline - now < wait) wait = _gridDeadline - now;
                _stream.ReadTimeout = Math.Max(1, (int)wait.TotalMilliseconds);

                Packet packet;
                try
                {
                    packet = _reader.Read();
                }
                catch (IOException ex) when (ex.InnerException is SocketException { SocketErrorCode: SocketError.TimedOut })
                {
                    if (_gotMap) continue; // vòng lặp đầu sẽ xử lý hạn chờ lưới
                    Console.WriteLine(count == 0
                        ? "Server im lặng sau khi đăng nhập (không có gói nào). Có thể cần gói -29 sub 2 (thông tin client); chưa gửi vì chưa rõ giá trị."
                        : $"Không có gói mới trong {Idle.TotalSeconds:0} giây.");
                    return Finish(ExitIncomplete);
                }
                catch (Exception ex) when (ex is EndOfStreamException or IOException)
                {
                    Console.WriteLine($"Server ngắt kết nối: {ex.Message}");
                    return Finish(ExitRejected);
                }

                count++;
                _sink.Record(PacketDirection.ServerToClient, packet);
                int? stop = Handle(packet);
                if (stop is { } code) return Finish(code);
            }
        }

        /// <summary>
        /// Nghe thụ động sau khi nhận lưới (quan sát ping và -5). KHÔNG gửi gì. Ghi lý do thoát vào exit.txt.
        /// Gặp -85 (captcha) hoặc -26 thì dừng như bình thường.
        /// </summary>
        private int Listen()
        {
            Console.WriteLine($"Nghe thụ động thêm {_listen.TotalSeconds:0} giây (không gửi gì) ...");
            var start = DateTime.UtcNow;
            var deadline = start + _listen;
            var counts = new SortedDictionary<int, int>();
            string reason;
            int code = ExitOk;
            while (true)
            {
                var left = deadline - DateTime.UtcNow;
                if (left <= TimeSpan.Zero) { reason = "hết giờ nghe, server vẫn giữ kết nối"; break; }
                _stream.ReadTimeout = Math.Max(1, (int)left.TotalMilliseconds);
                Packet packet;
                try
                {
                    packet = _reader.Read();
                }
                catch (IOException ex) when (ex.InnerException is SocketException { SocketErrorCode: SocketError.TimedOut })
                {
                    reason = "hết giờ nghe, server vẫn giữ kết nối";
                    break;
                }
                catch (Exception ex) when (ex is EndOfStreamException or IOException)
                {
                    reason = $"server ngắt kết nối sau {(DateTime.UtcNow - start).TotalSeconds:0.0} giây nghe: {ex.Message}";
                    code = ExitRejected;
                    break;
                }

                _sink.Record(PacketDirection.ServerToClient, packet);
                counts[packet.Command] = counts.GetValueOrDefault(packet.Command) + 1;
                if (packet.Command == -85) { reason = "server gửi -85 (captcha), dừng"; code = ExitCaptcha; break; }
                if (packet.Command == -26) { reason = $"server gửi -26: \"{ReadServerMessage(packet)}\", dừng"; code = ExitRejected; break; }
                if (packet.Command == -5)
                {
                    try
                    {
                        var a = PlayerAppear.Parse(packet.Payload);
                        _summary.Add($"mã -5, {packet.Payload.Length} byte: người chơi xuất hiện — {Verdict(a.ExtraBytes)}");
                    }
                    catch (InvalidDataException ex)
                    {
                        _summary.Add($"mã -5, {packet.Payload.Length} byte: BỘ ĐỌC LỖI — {ex.Message}");
                    }
                }
            }

            string byCode = counts.Count == 0 ? "(không có)" : string.Join(", ", counts.Select(kv => $"{kv.Key}×{kv.Value}"));
            Console.WriteLine($"Kết thúc nghe: {reason}.");
            Console.WriteLine($"  Số gói nhận thêm theo mã: {byCode}");
            try
            {
                File.WriteAllText(Path.Combine(_sink.Directory, "exit.txt"),
                    $"{reason}\nnghe {(DateTime.UtcNow - start).TotalSeconds:0.0} giây\ngói theo mã: {byCode}\n");
            }
            catch (IOException) { }
            return code;
        }

        private int Finish(int code)
        {
            PrintSummary();
            return code;
        }

        public void PrintSummary()
        {
            Console.WriteLine("--- Tóm tắt các gói đã nhận (theo thứ tự) ---");
            foreach (var line in _summary) Console.WriteLine("  " + line);
            if (_summary.Count == 0) Console.WriteLine("  (không có)");
        }

        private static string Verdict(int extra) => extra == 0 ? "tiêu thụ đúng" : extra > 0 ? $"THỪA {extra} byte" : $"THIẾU {-extra} byte";

        /// <summary>Xử lý một gói; trả mã thoát nếu phải dừng.</summary>
        private int? Handle(Packet p)
        {
            sbyte? sub = p.Payload.Length > 0 && p.Command is -29 or -28 or -30 or 12 ? (sbyte)p.Payload[0] : null;
            string tag = $"mã {p.Command}{(sub is { } s ? $" sub {s}" : "")}, {p.Payload.Length} byte";

            if (p.Command == -85)
            {
                _summary.Add($"{tag}: CAPTCHA (không đọc nội dung)");
                Console.WriteLine("Cần captcha. Dừng, không xử lý.");
                return ExitCaptcha;
            }

            if (p.Command == 2)
            {
                // Theo mã client, mã 2 (không sub) mở màn hình tạo nhân vật (aj_0). Probe không tạo nhân vật.
                _summary.Add($"{tag}: server yêu cầu tạo nhân vật (đoán từ mã client)");
                Console.WriteLine("Server gửi gói mã 2: acc có vẻ chưa có nhân vật. Dừng, không tạo nhân vật.");
                return ExitNeedCharacter;
            }

            if (p.Command == -26)
            {
                string text = ReadServerMessage(p);
                _summary.Add($"{tag}: thông báo của server \"{text}\" (có vẻ lỗi/mất kết nối, đoán)");
                Console.WriteLine($"Server báo: \"{text}\". Coi như bị từ chối, dừng.");
                return ExitRejected;
            }

            try
            {
                switch (p.Command, sub)
                {
                    case (-28, 4):
                        return OnVersions(p, tag);
                    case (-29, 2):
                    {
                        // Quan sát ở lần chạy 2: server trả danh sách server (UTF "Tên:host:cổng:loại:?:?,...,0,0"),
                        // KHÔNG phải lỗi đăng nhập như đoán ban đầu.
                        string list = ReadServerMessage(p);
                        int entries = list.Split(',').Count(e => e.Count(ch => ch == ':') >= 5);
                        _summary.Add($"{tag}: danh sách server ({entries} mục) — đã đọc hết UTF");
                        break;
                    }
                    case (-30, 0):
                    {
                        var c = CharacterInfo.Parse(p.Payload);
                        _summary.Add($"{tag}: nhân vật \"{c.Name}\" chủng tộc {c.Race}, {c.SkillIds.Length} skill, " +
                                     $"trang bị {c.Equipment.Count(x => x != null)}, túi {c.Bag.Count(x => x != null)}, rương {c.Chest.Count(x => x != null)} — {Verdict(c.ExtraBytes)}");
                        break;
                    }
                    case (-82, _):
                    {
                        var t = TileFlagTables.Parse(p.Payload);
                        _summary.Add($"{tag}: bảng cờ ô lát, {t.Sets.Length} bộ — {Verdict(t.ExtraBytes)}");
                        break;
                    }
                    case (-24, _):
                        return OnMap(p, tag);
                    case (-28, 10):
                    {
                        var g = MapGrid.Parse(p.Payload);
                        _gridDone = true;
                        _summary.Add($"{tag}: lưới ô lát {g.Width}x{g.Height} — {Verdict(g.ExtraBytes)}");
                        if (!_sentFinishMap)
                        {
                            // Client thật gửi -39 rỗng (finishLoadMap, an_0.dS) khi nạp xong map. Giả thuyết: server chỉ
                            // gửi -5 (người chơi khác) sau gói này. Gửi một lần.
                            _sentFinishMap = true;
                            Send(-39, Array.Empty<byte>());
                            Console.WriteLine("Đã nhận lưới, gửi -39 rỗng (báo tải xong map), một lần.");
                        }
                        break;
                    }
                    case (-5, _):
                    {
                        var a = PlayerAppear.Parse(p.Payload);
                        _summary.Add($"{tag}: người chơi \"{a.Info.Name}\" xuất hiện — {Verdict(a.ExtraBytes)}");
                        break;
                    }
                    case (-87, _) or (-28, 6) or (-28, 7):
                        _summary.Add($"{tag}: bảng dữ liệu (lưu thô trong file bắt gói; chưa có bộ đọc)");
                        break;
                    case (-28, 8) or (12, 0):
                        return OnItemPart(p, tag);
                    default:
                        _summary.Add($"{tag}: chưa có bộ đọc");
                        break;
                }
            }
            catch (InvalidDataException ex)
            {
                _summary.Add($"{tag}: BỘ ĐỌC LỖI — {ex.Message}");
            }
            return null;
        }

        private int? OnVersions(Packet p, string tag)
        {
            var v = DataVersions.Parse(p.Payload);
            _summary.Add($"{tag}: phiên bản data={v.Data} map={v.Map} skill={v.Skill} item={v.Item}, {v.Captions.Length} caption — {Verdict(v.ExtraBytes)}");
            // Chưa có bảng cục bộ nên coi cả bốn bảng là lệch (như client lần đầu chạy).
            Send(-87, Array.Empty<byte>());
            foreach (byte sub in new byte[] { 6, 7, 8 }) Send(-28, new[] { sub });
            Console.WriteLine("Đã xin bảng dữ liệu (-87, -28 sub 6/7/8); chờ server gửi đủ rồi mới báo tải xong.");
            return null;
        }

        /// <summary>
        /// Bảng item đến thành nhiều phần (payload: sub, phiên bản, loại bảng): loại 0, 100, 101 qua <c>-28</c> sub 8,
        /// loại 1 qua gói <c>12</c> sub 0 ("extra big"). Client thật chỉ báo tải xong (<c>-28</c> sub 13) sau gói <c>12</c>
        /// (<c>cj_0.b</c> case 12 gọi <c>cu_0.cQ</c> → <c>an_0.dR</c>), nên probe cũng gửi một lần ngay sau gói này.
        /// </summary>
        private int? OnItemPart(Packet p, string tag)
        {
            string type = p.Payload.Length >= 3 ? p.Payload[2].ToString() : "?";
            _summary.Add($"{tag}: phần bảng item, loại {type} (lưu thô trong file bắt gói; chưa có bộ đọc)");
            if (p.Command == 12 && !_sentDone)
            {
                _sentDone = true;
                Send(-28, new byte[] { 13 });
                // Client thật còn gửi -38 rỗng (an_0.dI) khi cả bốn cờ tải dữ liệu của màn hình đăng nhập đã tắt,
                // tức ngay sau gói 12; ý nghĩa của -38 chưa rõ.
                Send(-38, Array.Empty<byte>());
                Console.WriteLine("Đã nhận gói 12 (phần item lớn), gửi -28 sub 13 (báo tải xong) rồi -38 rỗng, mỗi gói một lần.");
            }
            return null;
        }

        private int? OnMap(Packet p, string tag)
        {
            var m = MapInfo.Parse(p.Payload);
            _gotMap = true;
            _summary.Add($"{tag}: map {m.Header.MapId} \"{m.Header.Name}\", {m.Body.Mobs.Length} quái, {m.Body.Npcs.Length} NPC, " +
                         $"{m.Body.Portals.Length} cổng, {m.Body.Items.Length} vật rơi — {Verdict(m.ExtraBytes)}");
            Console.WriteLine($"Nhận thông tin map {m.Header.MapId}. Xin lưới ô lát (-28 sub 10) vì chưa có lưới cục bộ.");
            Send(-28, new byte[] { 10, (byte)m.Header.MapId });
            _gridDeadline = DateTime.UtcNow + GridWait;
            return null;
        }

        private static string ReadServerMessage(Packet p)
        {
            try
            {
                var r = new JavaStreamReader(new MemoryStream(p.Payload));
                if (p.Command == -29) r.ReadSByte();
                return r.ReadUtf();
            }
            catch (EndOfStreamException)
            {
                return "(không đọc được nội dung)";
            }
        }
    }
}
