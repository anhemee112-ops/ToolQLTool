namespace ToolQL.Protocol.Messages;

// Gói về vật phẩm rơi trong map (S→C). Nguồn: cj_0.b nhánh 68, -20, -21. Trong DroppedItem: A = id vật rơi (dùng cho
// -20 và -21), B = mẫu vật phẩm, C = x, D = y, Value = id chủ (người được nhặt), Extra = short thêm khi Value = -2.

/// <summary>
/// <c>68</c>: vật rơi xuất hiện: short id, short mẫu, short x, short y, int chủ; nếu chủ = -2 thì thêm short.
/// Client bỏ qua nếu id đã có trong map. Chưa thấy trong bắt gói (map thử không có quái chết).
/// </summary>
public sealed record ItemAppeared(DroppedItem Item)
{
    public const sbyte Command = 68;

    public int ExtraBytes { get; init; }

    public static ItemAppeared Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("68", () =>
        {
            short id = r.ReadShort(), template = r.ReadShort(), x = r.ReadShort(), y = r.ReadShort();
            int owner = r.ReadInt();
            short? extra = owner == -2 ? r.ReadShort() : null;
            return new ItemAppeared(new DroppedItem(id, template, x, y, owner, extra)) { ExtraBytes = Msg.Extra(payload, r) };
        });
    }
}

/// <summary><c>-21</c>: vật rơi biến mất khỏi map. short id.</summary>
public sealed record ItemRemoved(short Id)
{
    public const sbyte Command = -21;

    public int ExtraBytes { get; init; }

    public static ItemRemoved Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-21", () => new ItemRemoved(r.ReadShort()) { ExtraBytes = Msg.Extra(payload, r) });
    }
}

/// <summary>
/// <c>-20</c>: kết quả nhặt vật rơi. short id, UTF thông báo, short số lượng (tùy chọn). Với một số loại vật phẩm client
/// đọc thêm short nữa theo loại của mẫu (cộng tiền ...); bộ đọc này KHÔNG biết loại nên phần còn lại báo ở
/// <see cref="ExtraBytes"/>. Chưa thấy trong bắt gói.
/// </summary>
public sealed record ItemPicked(short Id, string Message, short? Amount)
{
    public const sbyte Command = -20;

    public int ExtraBytes { get; init; }

    public static ItemPicked Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-20", () =>
        {
            short id = r.ReadShort();
            string message = r.ReadUtf();
            short? amount = Msg.Extra(payload, r) >= 2 ? r.ReadShort() : null;
            return new ItemPicked(id, message, amount) { ExtraBytes = Msg.Extra(payload, r) };
        });
    }
}
