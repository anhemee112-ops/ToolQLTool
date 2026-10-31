namespace ToolQL.Render;

/// <summary>
/// Một số ảnh trong jar của client (<c>x1/t/*.png</c>, <c>x1/SmallImage/*.png</c>) bị XOR với khóa lặp 7 byte; ảnh khác là PNG thường.
/// Khóa suy ra từ 16 byte đầu so với chữ ký PNG và kiểm trên 4 tệp thật (xem docs/protocol/render.md).
/// Khóa và chữ ký khai báo dạng <see cref="ReadOnlySpan{T}"/>: Windows Defender từng báo nhầm bản dùng mảng tĩnh (xem verification.md).
/// </summary>
public static class ClientImageCodec
{
    private static ReadOnlySpan<byte> PngSignature => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static ReadOnlySpan<byte> Key => new byte[] { 0xEA, 0x02, 0x0C, 0x04, 0x05, 0x02, 0xF6 };

    /// <summary>Trả PNG chuẩn: giữ nguyên nếu đã là PNG, nếu không thì giải XOR; vẫn không ra PNG thì ném <see cref="InvalidDataException"/>.</summary>
    public static byte[] ToPng(byte[] data)
    {
        if (IsPng(data)) return data;
        var decoded = Scramble(data);
        if (!IsPng(decoded)) throw new InvalidDataException("Không phải PNG, kể cả sau khi giải XOR.");
        return decoded;
    }

    /// <summary>XOR với khóa lặp (phép nghịch đảo của chính nó). Dùng cho test và giải mã.</summary>
    public static byte[] Scramble(byte[] data)
    {
        var result = (byte[])data.Clone();
        var key = Key;
        for (int i = 0; i < result.Length; i++) result[i] ^= key[i % key.Length];
        return result;
    }

    private static bool IsPng(byte[] data) => data.AsSpan().StartsWith(PngSignature);
}
