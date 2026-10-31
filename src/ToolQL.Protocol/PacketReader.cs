namespace ToolQL.Protocol;

/// <summary>
/// Nhận gói server → client. Chạy đồng bộ trên <see cref="Stream"/> (dùng trên luồng riêng).
/// Độ dài 2 byte, trừ nhóm mã lệnh <see cref="ThreeByteLengthCommands"/> dùng 3 byte.
/// </summary>
public sealed class PacketReader
{
    /// <summary>Nhóm mã lệnh có độ dài 3 byte (theo transport.md, từ-mã).</summary>
    public static readonly sbyte[] ThreeByteLengthCommands = { -32, -66, 11, -67, -74, -87, 66, 12 };

    private readonly Stream _stream;

    public PacketReader(Stream stream) => _stream = stream;

    /// <summary>Khóa XOR; null nghĩa là chưa giải mã. Gán sau khi nhận gói <c>-27</c>.</summary>
    public XorCipher? Cipher { get; set; }

    /// <summary>Đọc một gói đầy đủ. Hết dữ liệu giữa chừng thì ném <see cref="EndOfStreamException"/>.</summary>
    public Packet Read()
    {
        sbyte command = (sbyte)ReadWireByte();
        bool threeByte = Array.IndexOf(ThreeByteLengthCommands, command) >= 0;

        int length;
        if (threeByte)
        {
            // Nhánh nghi vấn: theo transport.md, mỗi byte giải XOR rồi cộng 128 (mod 256),
            // ghép ((b2<<8)+b1)<<8)+b0 với thứ tự đọc b0, b1, b2. Cần xác nhận bằng bắt gói.
            int b0 = (byte)(ReadWireByte() + 128);
            int b1 = (byte)(ReadWireByte() + 128);
            int b2 = (byte)(ReadWireByte() + 128);
            length = (((b2 << 8) + b1) << 8) + b0;
        }
        else
        {
            int hi = ReadWireByte();
            int lo = ReadWireByte();
            length = (hi << 8) | lo;
        }

        var payload = new byte[length];
        int got = 0;
        while (got < length)
        {
            int n = _stream.Read(payload, got, length - got);
            if (n <= 0)
                throw new EndOfStreamException($"Hết dữ liệu giữa payload: cần {length} byte, có {got}.");
            got += n;
        }
        if (Cipher is { } cipher)
            for (int i = 0; i < payload.Length; i++)
                payload[i] = cipher.Decrypt(payload[i]);

        return new Packet(command, payload, threeByte);
    }

    private int ReadWireByte()
    {
        int b = _stream.ReadByte();
        if (b < 0) throw new EndOfStreamException("Hết dữ liệu khi đọc khung gói.");
        return Cipher is { } cipher ? cipher.Decrypt((byte)b) : b;
    }
}
