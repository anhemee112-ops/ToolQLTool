using System.Net;
using System.Net.Sockets;
using ToolQL.Protocol;

namespace ToolQL.Core.Tests;

/// <summary>Một kết nối phía server giả: nói đúng khung gói và khóa XOR của giao thức.</summary>
internal sealed class ServerConn : IDisposable
{
    private readonly Stream _stream;
    private readonly PacketWriter _writer;
    private readonly List<string> _errors;
    private XorCipher? _cipher;

    public ServerConn(Stream stream, List<string> errors)
    {
        _stream = stream;
        _writer = new PacketWriter(stream);
        _errors = errors;
    }

    /// <summary>
    /// Đọc một gói từ client. Chiều client→server LUÔN dùng độ dài 2 byte (PacketWriter), kể cả mã -87;
    /// nên không dùng PacketReader (nó áp nhóm 3 byte của chiều server→client).
    /// </summary>
    public Packet Read()
    {
        sbyte command = (sbyte)ReadByte();
        int length = (ReadByte() << 8) | ReadByte();
        var payload = new byte[length];
        for (int i = 0; i < length; i++) payload[i] = (byte)ReadByte();
        return new Packet(command, payload);
    }

    private int ReadByte()
    {
        int b = _stream.ReadByte();
        if (b < 0) throw new EndOfStreamException("Client đã đóng kết nối.");
        return _cipher is { } cipher ? cipher.Decrypt((byte)b) : b;
    }

    /// <summary>Đọc một gói và kiểm mã lệnh (và sub nếu cho). Sai thì ghi lỗi và ném.</summary>
    public Packet Expect(sbyte command, int? sub = null)
    {
        var packet = Read();
        bool ok = packet.Command == command && (sub is null || (packet.Payload.Length > 0 && (sbyte)packet.Payload[0] == sub));
        if (!ok)
        {
            string got = $"{packet.Command}" + (packet.Payload.Length > 0 ? $"/{(sbyte)packet.Payload[0]}" : "");
            lock (_errors) _errors.Add($"Mong {command}{(sub is null ? "" : "/" + sub)}, nhận {got}");
            throw new InvalidDataException("gói không đúng thứ tự");
        }
        return packet;
    }

    /// <summary>Đọc bỏ mọi gói cho tới khi client đóng kết nối (tránh đóng khi còn dữ liệu chưa đọc, gây RST làm mất gói đã gửi).</summary>
    public void Drain()
    {
        while (true) Read();
    }

    public void Send(sbyte command, byte[] payload) => _writer.Write(command, payload);

    /// <summary>Gói dùng độ dài 3 byte (ví dụ mã 12): mỗi byte độ dài trừ 128 trước khi mã hóa (nghịch với PacketReader).</summary>
    public void SendThreeByte(sbyte command, byte[] payload)
    {
        var frame = new byte[4 + payload.Length];
        frame[0] = (byte)command;
        frame[1] = (byte)((payload.Length & 0xFF) - 128);
        frame[2] = (byte)(((payload.Length >> 8) & 0xFF) - 128);
        frame[3] = (byte)(((payload.Length >> 16) & 0xFF) - 128);
        payload.CopyTo(frame, 4);
        if (_cipher is { } cipher)
            for (int i = 0; i < frame.Length; i++) frame[i] = cipher.Encrypt(frame[i]);
        _stream.Write(frame);
        _stream.Flush();
    }

    /// <summary>Nhận -27 rỗng của client, trả gói -27 mang khóa, rồi bật mã hóa hai chiều.</summary>
    public void Handshake()
    {
        Expect(-27);
        var key = new byte[] { 0x11, 0x42, 0x07, 0x9C, 0x55 };
        var w = new JavaStreamWriter();
        w.WriteByte(key.Length);
        w.WriteBytes(key);
        w.WriteUtf("fake");
        w.WriteInt(12345);
        w.WriteByte(0);
        Send(-27, w.ToArray());
        _cipher = new XorCipher(key);
        _writer.Cipher = _cipher;
    }

    /// <summary>Nhận hai gói đăng nhập của client (-29 sub 2 rồi -29 sub 0), trả gói đăng nhập.</summary>
    public Packet ExpectLogin()
    {
        Expect(-29, 2);
        return Expect(-29, 0);
    }

    /// <summary>Chạy đúng chuỗi server thật đã chạy: phiên bản, bảng, gói 12, map, lưới. Đến khi client gửi -39.</summary>
    public void RunHappyPath(int mapId = 23)
    {
        Handshake();
        ExpectLogin();
        Send(-28, Packets.Versions());
        Expect(-87);
        Expect(-28, 6); Expect(-28, 7); Expect(-28, 8);
        SendThreeByte(12, new byte[] { 0, 24, 1 });
        Expect(-28, 13);
        Expect(-38);
        Send(-24, Packets.Map(mapId));
        var request = Expect(-28, 10);
        if (request.Payload.Length != 2 || request.Payload[1] != mapId)
            lock (_errors) _errors.Add("Yêu cầu lưới sai mapId");
        Send(-28, Packets.Grid(2, 2));
        Expect(-39);
    }

    public void Dispose() => _stream.Dispose();
}

/// <summary>Máy chủ giả trên loopback. Mỗi kết nối chạy kịch bản trên luồng riêng; kịch bản nhận chỉ số kết nối (từ 0).</summary>
internal sealed class FakeServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly Action<ServerConn, int> _script;
    private int _accepted;

    public List<string> Errors { get; } = new();
    public int Port { get; }
    public int Accepted => Volatile.Read(ref _accepted);

    public FakeServer(Action<ServerConn, int> script)
    {
        _script = script;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        new Thread(AcceptLoop) { IsBackground = true }.Start();
    }

    public Func<CancellationToken, Stream> Connector() => TcpConnector.Create("127.0.0.1", Port);

    private void AcceptLoop()
    {
        try
        {
            while (true)
            {
                var client = _listener.AcceptTcpClient();
                int index = Interlocked.Increment(ref _accepted) - 1;
                new Thread(() => Serve(client, index)) { IsBackground = true }.Start();
            }
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
        }
    }

    private void Serve(TcpClient client, int index)
    {
        using var conn = new ServerConn(client.GetStream(), Errors);
        try
        {
            _script(conn, index);
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or InvalidDataException or ObjectDisposedException)
        {
        }
        finally
        {
            client.Dispose();
        }
    }

    public void Dispose() => _listener.Stop();
}
