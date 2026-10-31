namespace ToolQL.Protocol;

/// <summary>
/// Gửi gói client → server: 1 byte mã lệnh, 2 byte độ dài (big-endian), payload.
/// Có khóa thì XOR từng byte theo thứ tự trên đường truyền (xem <c>docs/protocol/transport.md</c>).
/// </summary>
public sealed class PacketWriter
{
    private readonly Stream _stream;

    public PacketWriter(Stream stream) => _stream = stream;

    /// <summary>Khóa XOR; null nghĩa là chưa mã hóa. Gán sau khi bắt tay khóa xong.</summary>
    public XorCipher? Cipher { get; set; }

    public void Write(sbyte command, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > ushort.MaxValue)
            throw new ArgumentException($"Payload dài {payload.Length} byte, vượt giới hạn 65535 của độ dài 2 byte.", nameof(payload));

        var frame = new byte[3 + payload.Length];
        frame[0] = (byte)command;
        frame[1] = (byte)(payload.Length >> 8);
        frame[2] = (byte)payload.Length;
        payload.CopyTo(frame.AsSpan(3));

        if (Cipher is { } cipher)
            for (int i = 0; i < frame.Length; i++)
                frame[i] = cipher.Encrypt(frame[i]);

        _stream.Write(frame);
        _stream.Flush();
    }
}
