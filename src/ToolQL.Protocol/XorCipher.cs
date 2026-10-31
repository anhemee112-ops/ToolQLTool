namespace ToolQL.Protocol;

/// <summary>
/// Mã hóa dòng XOR của gói tin (theo <c>docs/protocol/transport.md</c>, mức tin cậy: từ-mã).
/// Hai chỉ số riêng cho chiều gửi và chiều nhận, cùng một khóa.
/// </summary>
public sealed class XorCipher
{
    private readonly byte[] _key;
    private int _sendIndex;
    private int _receiveIndex;

    /// <param name="rawKey">Khóa thô như server gửi trong gói <c>-27</c>; lớp tự biến đổi.</param>
    public XorCipher(byte[] rawKey)
    {
        if (rawKey is null || rawKey.Length == 0)
            throw new ArgumentException("Khóa rỗng.", nameof(rawKey));
        _key = TransformKey(rawKey);
    }

    /// <summary>Biến đổi khóa thô: <c>k[i+1] ^= k[i]</c> tuần tự (dùng giá trị đã đổi). Không sửa mảng gốc.</summary>
    public static byte[] TransformKey(byte[] rawKey)
    {
        var key = (byte[])rawKey.Clone();
        for (int i = 0; i < key.Length - 1; i++)
            key[i + 1] ^= key[i];
        return key;
    }

    /// <summary>Mã hóa một byte gửi đi (chỉ số chiều gửi).</summary>
    public byte Encrypt(byte value)
    {
        byte result = (byte)(value ^ _key[_sendIndex]);
        _sendIndex = (_sendIndex + 1) % _key.Length;
        return result;
    }

    /// <summary>Giải mã một byte nhận về (chỉ số chiều nhận).</summary>
    public byte Decrypt(byte value)
    {
        byte result = (byte)(value ^ _key[_receiveIndex]);
        _receiveIndex = (_receiveIndex + 1) % _key.Length;
        return result;
    }
}
