namespace ToolQL.Protocol;

/// <summary>
/// Kết quả gói <c>-27</c> từ server. Ba trường cuối chưa rõ ý nghĩa (xem transport.md), chỉ ghi lại.
/// </summary>
/// <param name="RawKey">Khóa thô như server gửi (chưa biến đổi).</param>
/// <param name="Text">Chuỗi UTF (trong jar là <c>hero69.N</c>).</param>
/// <param name="Number">Số int (<c>hero69.bF</c>).</param>
/// <param name="Flag">Byte cuối (<c>hero69.bo</c>, client coi là boolean).</param>
/// <param name="ExtraBytes">Số byte thừa sau trường cuối; khác 0 nghĩa là bố cục trong tài liệu chưa đủ.</param>
public sealed record HandshakeInfo(byte[] RawKey, string Text, int Number, byte Flag, int ExtraBytes);

/// <summary>Bắt tay khóa: gửi <c>-27</c> rỗng, nhận <c>-27</c> mang khóa, bật mã hóa hai chiều.</summary>
public static class KeyHandshake
{
    public const sbyte Command = -27;

    /// <summary>Đọc payload gói <c>-27</c>. Khóa rỗng hoặc thiếu byte thì ném <see cref="InvalidDataException"/>.</summary>
    public static HandshakeInfo Parse(byte[] payload)
    {
        var reader = new JavaStreamReader(new MemoryStream(payload));
        try
        {
            int n = reader.ReadByte();
            if (n == 0)
                throw new InvalidDataException("Gói -27 có độ dài khóa bằng 0.");
            var key = reader.ReadBytes(n);
            string text = reader.ReadUtf();
            int number = reader.ReadInt();
            byte flag = (byte)reader.ReadByte();
            return new HandshakeInfo(key, text, number, flag, payload.Length - (int)reader.Position);
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException("Gói -27 thiếu dữ liệu: " + ex.Message, ex);
        }
    }

    /// <summary>
    /// Gửi <c>-27</c> rỗng, đọc gói kế tiếp (phải là <c>-27</c>), rồi gán cùng một <see cref="XorCipher"/>
    /// cho cả writer và reader (hai chỉ số riêng bên trong).
    /// </summary>
    /// <param name="onPacket">Gọi cho gói gửi đi và gói nhận về (để bắt gói), có thể null.</param>
    public static HandshakeInfo Perform(PacketWriter writer, PacketReader reader, Action<PacketDirection, Packet>? onPacket = null)
    {
        writer.Write(Command, ReadOnlySpan<byte>.Empty);
        onPacket?.Invoke(PacketDirection.ClientToServer, new Packet(Command, Array.Empty<byte>()));
        var packet = reader.Read();
        onPacket?.Invoke(PacketDirection.ServerToClient, packet);
        if (packet.Command != Command)
            throw new InvalidDataException($"Mong gói -27 nhưng nhận gói {packet.Command} ({packet.Payload.Length} byte).");

        var info = Parse(packet.Payload);
        var cipher = new XorCipher(info.RawKey);
        writer.Cipher = cipher;
        reader.Cipher = cipher;
        return info;
    }
}
