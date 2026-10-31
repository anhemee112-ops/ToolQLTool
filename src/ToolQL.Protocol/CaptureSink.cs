using System.Text.Json;

namespace ToolQL.Protocol;

/// <summary>Hướng đi của gói.</summary>
public enum PacketDirection
{
    /// <summary>Client gửi cho server.</summary>
    ClientToServer,
    /// <summary>Server gửi cho client.</summary>
    ServerToClient,
}

/// <summary>
/// Ghi mỗi gói thành một dòng JSON (<c>packets.jsonl</c>): thời điểm, hướng, mã lệnh, sub, độ dài, hex payload đã giải mã.
/// Gói chứa mật khẩu (<c>-29</c> sub 0 và sub 1, chiều client → server) chỉ ghi mã lệnh và độ dài, <b>không ghi nội dung</b>.
/// Thư mục bắt gói phải nằm ngoài repo. An toàn khi gọi từ nhiều luồng (luồng đọc và luồng gửi).
/// </summary>
public sealed class CaptureSink : IDisposable
{
    /// <summary>Các mã lệnh có byte phụ (sub) đứng đầu payload (xem messages.md).</summary>
    private static readonly sbyte[] CommandsWithSub = { -29, -28, -30 };

    private readonly object _lock = new();
    private readonly StreamWriter _file;
    private readonly TimeProvider _time;

    public string Directory { get; }

    public CaptureSink(string directory, TimeProvider? time = null)
    {
        Directory = directory;
        _time = time ?? TimeProvider.System;
        System.IO.Directory.CreateDirectory(directory);
        _file = new StreamWriter(
            new FileStream(Path.Combine(directory, "packets.jsonl"), FileMode.Append, FileAccess.Write, FileShare.Read),
            new System.Text.UTF8Encoding(false));
    }

    /// <summary>Tạo sink ở <c>%USERPROFILE%\.toolql\captures\&lt;ngày-giờ&gt;\</c>.</summary>
    public static CaptureSink CreateDefault(TimeProvider? time = null)
    {
        time ??= TimeProvider.System;
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".toolql", "captures");
        return new CaptureSink(Path.Combine(root, time.GetLocalNow().ToString("yyyyMMdd-HHmmss")), time);
    }

    /// <summary>Gói chứa mật khẩu: không được ghi nội dung.</summary>
    public static bool ShouldMask(PacketDirection direction, Packet packet) =>
        direction == PacketDirection.ClientToServer
        && packet.Command == -29
        && packet.Payload.Length > 0
        && packet.Payload[0] is 0 or 1;

    public void Record(PacketDirection direction, Packet packet)
    {
        bool masked = ShouldMask(direction, packet);
        int? sub = Array.IndexOf(CommandsWithSub, packet.Command) >= 0 && packet.Payload.Length > 0
            ? (sbyte)packet.Payload[0]
            : null;

        var entry = new Dictionary<string, object?>
        {
            ["t"] = _time.GetUtcNow().ToString("O"),
            ["dir"] = direction == PacketDirection.ClientToServer ? "C->S" : "S->C",
            ["cmd"] = packet.Command,
            ["sub"] = sub,
            ["len"] = packet.Payload.Length,
            ["three"] = packet.UsedThreeByteLength,
            ["masked"] = masked,
            ["hex"] = masked ? null : Convert.ToHexString(packet.Payload),
        };

        string line = JsonSerializer.Serialize(entry);
        lock (_lock)
        {
            _file.WriteLine(line);
            _file.Flush();
        }
    }

    public void Dispose()
    {
        lock (_lock) _file.Dispose();
    }
}
