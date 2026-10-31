namespace ToolQL.Protocol.Messages;

/// <summary>
/// Tiện ích chung cho các bộ đọc gói nghiệp vụ. Mỗi bộ đọc trả cấu trúc kèm <c>ExtraBytes</c>
/// (số byte payload còn thừa sau khi đọc xong): đây là phép kiểm chính "bộ đọc tiêu thụ đúng độ dài payload".
/// Đọc thiếu byte hoặc gặp số lượng âm thì ném <see cref="InvalidDataException"/>.
/// </summary>
internal static class Msg
{
    public static JavaStreamReader Open(byte[] payload) => new(new MemoryStream(payload));

    /// <summary>Số byte payload chưa đọc.</summary>
    public static int Extra(byte[] payload, JavaStreamReader reader) => payload.Length - (int)reader.Position;

    /// <summary>Chạy bộ đọc, đổi lỗi hết dữ liệu thành <see cref="InvalidDataException"/> có tên gói.</summary>
    public static T Guard<T>(string name, Func<T> read)
    {
        try
        {
            return read();
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException($"Gói {name} thiếu dữ liệu: {ex.Message}", ex);
        }
    }

    /// <summary>Số lượng đọc bằng byte có dấu như client Java; âm thì client sẽ lỗi nên coi là dữ liệu hỏng.</summary>
    public static int Count(JavaStreamReader reader, string name, string what)
    {
        int n = reader.ReadSByte();
        if (n < 0) throw new InvalidDataException($"Gói {name}: số lượng {what} âm ({n}).");
        return n;
    }

    public static int ShortCount(JavaStreamReader reader, string name, string what)
    {
        int n = reader.ReadShort();
        if (n < 0) throw new InvalidDataException($"Gói {name}: số lượng {what} âm ({n}).");
        return n;
    }

    /// <summary>Đọc byte sub đứng đầu payload và kiểm đúng giá trị mong đợi.</summary>
    public static void ExpectSub(JavaStreamReader reader, int expected, string name)
    {
        int sub = reader.ReadSByte();
        if (sub != expected) throw new InvalidDataException($"Gói {name}: sub là {sub}.");
    }

    public static T[] Repeat<T>(int n, Func<T> read)
    {
        var a = new T[n];
        for (int i = 0; i < n; i++) a[i] = read();
        return a;
    }
}
