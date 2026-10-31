namespace ToolQL.Protocol.Messages;

// Gói cập nhật nhân vật của mình và người chơi khác (S→C). Nguồn: cj_0.j (nhánh -30, byte sub đứng đầu), cj_0.b
// (18, -8, -17, -64). Tên trường là cách gọi của ToolQL; tên trong jar để trong ghi chú khi chưa rõ ý nghĩa.

/// <summary>
/// <c>-30</c> sub 4: chỉ số của MÌNH. long <c>aI</c>, int <c>gF</c>, long HP, long MP, int <c>cQ</c>. Ba số <c>aI</c>,
/// <c>gF</c>, <c>cQ</c> là ba loại tiền/tài sản (client cộng vào khi nhặt vật rơi loại tương ứng) nhưng tên loại CHƯA
/// kiểm chứng nên đặt trung tính. Bắt gói: 6 gói, đều 33 byte.
/// </summary>
public sealed record MeStats(long MoneyA, int MoneyB, long Hp, long Mp, int MoneyC)
{
    public const sbyte Command = -30;
    public const int Sub = 4;

    public int ExtraBytes { get; init; }

    public static MeStats Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-30 sub 4", () =>
        {
            Msg.ExpectSub(r, Sub, "-30 sub 4");
            return new MeStats(r.ReadLong(), r.ReadInt(), r.ReadLong(), r.ReadLong(), r.ReadInt()) { ExtraBytes = Msg.Extra(payload, r) };
        });
    }
}

/// <summary><c>-30</c> sub 5: HP của mình. long HP.</summary>
public sealed record MeHp(long Hp)
{
    public int ExtraBytes { get; init; }

    public static MeHp Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-30 sub 5", () =>
        {
            Msg.ExpectSub(r, 5, "-30 sub 5");
            return new MeHp(r.ReadLong()) { ExtraBytes = Msg.Extra(payload, r) };
        });
    }
}

/// <summary><c>-30</c> sub 6: MP của mình. long MP.</summary>
public sealed record MeMp(long Mp)
{
    public int ExtraBytes { get; init; }

    public static MeMp Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-30 sub 6", () =>
        {
            Msg.ExpectSub(r, 6, "-30 sub 6");
            return new MeMp(r.ReadLong()) { ExtraBytes = Msg.Extra(payload, r) };
        });
    }
}

/// <summary>
/// <c>-30</c> sub 7: người chơi khác thay đổi diện mạo/thông tin. int id, int <c>cC</c>, thông tin người chơi
/// (<see cref="PlayerInfo"/>), rồi (tùy chọn) short, byte, short. Bắt gói: 6 gói, 59-66 byte.
/// </summary>
public sealed record PlayerInfoChanged(int Id, int Kind, PlayerInfo Info, (short, sbyte, short)? Tail)
{
    public int ExtraBytes { get; init; }

    public static PlayerInfoChanged Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-30 sub 7", () =>
        {
            Msg.ExpectSub(r, 7, "-30 sub 7");
            int id = r.ReadInt();
            int kind = r.ReadInt();
            var info = PlayerInfo.Read(r, "-30 sub 7");
            (short, sbyte, short)? tail = Msg.Extra(payload, r) >= 5 ? (r.ReadShort(), (sbyte)r.ReadSByte(), r.ReadShort()) : null;
            return new PlayerInfoChanged(id, kind, info, tail) { ExtraBytes = Msg.Extra(payload, r) };
        });
    }
}

/// <summary><c>-30</c> sub 8: một byte trạng thái của người chơi (<c>cn</c>, ý nghĩa chưa rõ). int id, byte. Bắt gói: 5 gói, 6 byte; xuất hiện kèm mỗi gói <c>-5</c>.</summary>
public sealed record PlayerStatusByte(int Id, sbyte Value)
{
    public int ExtraBytes { get; init; }

    public static PlayerStatusByte Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-30 sub 8", () =>
        {
            Msg.ExpectSub(r, 8, "-30 sub 8");
            return new PlayerStatusByte(r.ReadInt(), (sbyte)r.ReadSByte()) { ExtraBytes = Msg.Extra(payload, r) };
        });
    }
}

/// <summary>
/// HP của một người chơi (có thể là mình) trong các nhánh <c>-30</c>: sub 9 (HP, HP tối đa), sub 13 (thêm 2 short),
/// sub 14 (HP, byte, HP tối đa tùy chọn; 49 gói, 22 byte, là gói thường gặp nhất khi đang đánh nhau), sub 15
/// (HP, HP tối đa, x, y). Trường không có trong nhánh đó là null.
/// </summary>
public sealed record PlayerHpUpdate(int Sub, int Id, long Hp, long? HpMax, sbyte? Flag, short? A, short? B)
{
    public int ExtraBytes { get; init; }

    public static PlayerHpUpdate Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        string name = "-30 hp";
        return Msg.Guard(name, () =>
        {
            int sub = r.ReadSByte();
            int id = r.ReadInt();
            long hp = r.ReadLong();
            switch (sub)
            {
                case 9:
                    return new PlayerHpUpdate(sub, id, hp, r.ReadLong(), null, null, null) { ExtraBytes = Msg.Extra(payload, r) };
                case 13:
                case 15:
                    return new PlayerHpUpdate(sub, id, hp, r.ReadLong(), null, r.ReadShort(), r.ReadShort()) { ExtraBytes = Msg.Extra(payload, r) };
                case 14:
                {
                    sbyte flag = (sbyte)r.ReadSByte();
                    long? max = Msg.Extra(payload, r) >= 8 ? r.ReadLong() : null;
                    return new PlayerHpUpdate(sub, id, hp, max, flag, null, null) { ExtraBytes = Msg.Extra(payload, r) };
                }
                default:
                    throw new InvalidDataException($"Gói {name}: sub {sub} không phải 9, 13, 14, 15.");
            }
        });
    }
}

/// <summary><c>18</c>: vị trí và HP nhiều người chơi cùng lúc. byte n; n × (int id, short x, short y, long HP).</summary>
public sealed record PlayerPositions(PlayerPosition[] Players)
{
    public const sbyte Command = 18;

    public int ExtraBytes { get; init; }

    public static PlayerPositions Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("18", () =>
        {
            int n = Msg.Count(r, "18", "người chơi");
            var players = Msg.Repeat(n, () => new PlayerPosition(r.ReadInt(), r.ReadShort(), r.ReadShort(), r.ReadLong()));
            return new PlayerPositions(players) { ExtraBytes = Msg.Extra(payload, r) };
        });
    }
}

public sealed record PlayerPosition(int Id, short X, short Y, long Hp);

/// <summary><c>-8</c>: người chơi khác bị đặt lại vị trí. int id, byte <c>au</c>, short x, short y.</summary>
public sealed record PlayerRelocated(int Id, sbyte UnknownAu, short X, short Y)
{
    public const sbyte Command = -8;

    public int ExtraBytes { get; init; }

    public static PlayerRelocated Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-8", () =>
            new PlayerRelocated(r.ReadInt(), (sbyte)r.ReadSByte(), r.ReadShort(), r.ReadShort()) { ExtraBytes = Msg.Extra(payload, r) });
    }
}

/// <summary><c>-17</c>: MÌNH bị đặt lại vị trí. byte <c>au</c>, short x, short y, long <c>as</c> tùy chọn.</summary>
public sealed record MeRelocated(sbyte UnknownAu, short X, short Y, long? UnknownAs)
{
    public const sbyte Command = -17;

    public int ExtraBytes { get; init; }

    public static MeRelocated Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-17", () =>
        {
            sbyte au = (sbyte)r.ReadSByte();
            short x = r.ReadShort(), y = r.ReadShort();
            long? asValue = Msg.Extra(payload, r) >= 8 ? r.ReadLong() : null;
            return new MeRelocated(au, x, y, asValue) { ExtraBytes = Msg.Extra(payload, r) };
        });
    }
}

/// <summary>
/// <c>-64</c>: một người chơi (có thể là mình) đổi món đeo lưng/phụ kiện (<c>bY</c>): int id, short. Doc cũ ghi
/// "cập nhật túi"; theo mã client thì chỉ gán <c>bY</c> của người chơi đó. Bắt gói: 6 gói, đều 6 byte.
/// </summary>
public sealed record PlayerBagChanged(int Id, short BagId)
{
    public const sbyte Command = -64;

    public int ExtraBytes { get; init; }

    public static PlayerBagChanged Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-64", () => new PlayerBagChanged(r.ReadInt(), r.ReadShort()) { ExtraBytes = Msg.Extra(payload, r) });
    }
}

/// <summary><c>-7</c>: người chơi khác di chuyển. int id, short x, short y. Bắt gói: 312 gói, luôn 8 byte.</summary>
public sealed record PlayerMoved(int Id, short X, short Y)
{
    public const sbyte Command = -7;

    public int ExtraBytes { get; init; }

    public static PlayerMoved Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-7", () => new PlayerMoved(r.ReadInt(), r.ReadShort(), r.ReadShort()) { ExtraBytes = Msg.Extra(payload, r) });
    }
}

/// <summary><c>-6</c>: người chơi khác rời map. int id. Bắt gói: 90 gói, luôn 4 byte.</summary>
public sealed record PlayerLeft(int Id)
{
    public const sbyte Command = -6;

    public int ExtraBytes { get; init; }

    public static PlayerLeft Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-6", () => new PlayerLeft(r.ReadInt()) { ExtraBytes = Msg.Extra(payload, r) });
    }
}

/// <summary>
/// <c>-90</c> (S→C): thay đổi trang phục của một người chơi. Có HAI dạng, đều thấy trong bắt gói (3 tệp, 1103 gói):
/// <list type="bullet">
/// <item>12 byte (1079 gói), byte <c>Kind</c> = 1: byte, int id, short đầu, short thân, short chân, byte cuối.
/// Bộ ba short là các số liền nhau (ví dụ 252, 253, 254), hợp với id hình đầu/thân/chân.</item>
/// <item>5 byte (24 gói), <c>Kind</c> = -1: chỉ int id; <see cref="Head"/>, <see cref="Body"/>, <see cref="Leg"/>,
/// <see cref="Last"/> là null. Ý nghĩa chưa rõ (id có thể âm, tức không phải người chơi).</item>
/// </list>
/// Bố cục dạng 12 byte theo bảng mã (<c>cj_0.b</c>); dạng 5 byte chỉ biết từ bắt gói. "Đầu/thân/chân" là suy luận từ
/// dữ liệu, chưa đối chiếu với hình vẽ.
/// </summary>
public sealed record PlayerOutfitChanged(sbyte Kind, int Id, short? Head, short? Body, short? Leg, sbyte? Last)
{
    public const sbyte Command = -90;

    public int ExtraBytes { get; init; }

    public static PlayerOutfitChanged Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-90", () =>
        {
            sbyte kind = (sbyte)r.ReadSByte();
            int id = r.ReadInt();
            if (kind == -1) return new PlayerOutfitChanged(kind, id, null, null, null, null) { ExtraBytes = Msg.Extra(payload, r) };
            return new PlayerOutfitChanged(kind, id, r.ReadShort(), r.ReadShort(), r.ReadShort(), (sbyte)r.ReadSByte())
            { ExtraBytes = Msg.Extra(payload, r) };
        });
    }
}
