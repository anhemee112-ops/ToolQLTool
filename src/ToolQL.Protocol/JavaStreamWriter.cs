using System.Text;

namespace ToolQL.Protocol;

/// <summary>Ghi kiểu dữ liệu Java (<c>DataOutputStream</c>): big-endian, UTF có tiền tố độ dài.</summary>
public sealed class JavaStreamWriter
{
    private readonly MemoryStream _buffer = new();

    public void WriteByte(int value) => _buffer.WriteByte((byte)value);

    public void WriteSByte(int value) => _buffer.WriteByte((byte)value);

    public void WriteShort(int value)
    {
        WriteByte(value >> 8);
        WriteByte(value);
    }

    public void WriteUShort(int value) => WriteShort(value);

    public void WriteInt(int value)
    {
        WriteByte(value >> 24);
        WriteByte(value >> 16);
        WriteByte(value >> 8);
        WriteByte(value);
    }

    public void WriteLong(long value)
    {
        WriteInt((int)(value >> 32));
        WriteInt((int)value);
    }

    public void WriteBool(bool value) => WriteByte(value ? 1 : 0);

    /// <summary>Độ dài ushort rồi UTF-8; quá 65535 byte thì ném lỗi.</summary>
    public void WriteUtf(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > ushort.MaxValue)
            throw new ArgumentException($"Chuỗi UTF dài {bytes.Length} byte, vượt giới hạn 65535.", nameof(value));
        WriteUShort(bytes.Length);
        WriteBytes(bytes);
    }

    public void WriteBytes(ReadOnlySpan<byte> data) => _buffer.Write(data);

    public byte[] ToArray() => _buffer.ToArray();
}
