using System.Text;

namespace ToolQL.Protocol;

/// <summary>
/// Đọc kiểu dữ liệu Java (<c>DataInputStream</c>): big-endian, UTF có tiền tố độ dài.
/// Đọc quá dữ liệu thì ném <see cref="EndOfStreamException"/> để phép kiểm "tiêu thụ đúng độ dài" phát hiện được.
/// </summary>
public sealed class JavaStreamReader
{
    private readonly Stream _stream;

    public JavaStreamReader(Stream stream) => _stream = stream;

    /// <summary>Số byte đã tiêu thụ kể từ lúc tạo bộ đọc.</summary>
    public long Position { get; private set; }

    public sbyte ReadSByte() => (sbyte)ReadByte();

    public int ReadByte()
    {
        int b = _stream.ReadByte();
        if (b < 0) throw Thieu(1, 0);
        Position++;
        return b;
    }

    public short ReadShort() => (short)ReadUShort();

    public int ReadUShort()
    {
        var b = ReadBytes(2);
        return (b[0] << 8) | b[1];
    }

    public int ReadInt()
    {
        var b = ReadBytes(4);
        return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
    }

    public long ReadLong()
    {
        var b = ReadBytes(8);
        long hi = (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
        long lo = (uint)((b[4] << 24) | (b[5] << 16) | (b[6] << 8) | b[7]);
        return (hi << 32) | lo;
    }

    /// <summary>Java coi mọi giá trị khác 0 là true.</summary>
    public bool ReadBool() => ReadByte() != 0;

    /// <summary>Độ dài ushort rồi UTF-8.</summary>
    public string ReadUtf()
    {
        int len = ReadUShort();
        return Encoding.UTF8.GetString(ReadBytes(len));
    }

    public byte[] ReadBytes(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var buf = new byte[count];
        int got = 0;
        while (got < count)
        {
            int n = _stream.Read(buf, got, count - got);
            if (n <= 0)
            {
                Position += got;
                throw Thieu(count, got);
            }
            got += n;
        }
        Position += count;
        return buf;
    }

    private static EndOfStreamException Thieu(int can, int co) =>
        new($"Hết dữ liệu: cần {can} byte nhưng chỉ có {co}.");
}
