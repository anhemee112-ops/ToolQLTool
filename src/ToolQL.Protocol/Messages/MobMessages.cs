namespace ToolQL.Protocol.Messages;

// Gói về quái trong map (S→C). "Chỉ số quái" luôn là chỉ số trong danh sách quái của map hiện tại (do -24 nạp).
// Nguồn: cj_0.b, các nhánh -9, -12, -13, -11, -10. Phần cuối nhiều gói được client bọc try, nên có thể thiếu
// (trường tương ứng là null / mảng rỗng). Mỗi bộ đọc báo ExtraBytes để kiểm tiêu thụ đúng.

/// <summary>
/// <c>-9</c>: quái bị đánh. ubyte chỉ số, long HP còn lại, long sát thương, bool chí mạng, byte hiệu ứng. Khi sát thương
/// = 1 client dừng đọc ở đây (bỏ qua hai trường cuối) nhưng server VẪN gửi chúng (bắt gói: 62 gói, luôn 19 byte, kể cả
/// 24 gói có sát thương 1), nên bộ đọc đọc hai trường đó khi còn byte.
/// </summary>
public sealed record MobHit(int Index, long Hp, long Damage, bool? Crit, sbyte? Effect)
{
    public const sbyte Command = -9;

    public int ExtraBytes { get; init; }

    public static MobHit Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-9", () =>
        {
            int index = r.ReadByte();
            long hp = r.ReadLong();
            long damage = r.ReadLong();
            bool? crit = null;
            sbyte? effect = null;
            if (Msg.Extra(payload, r) >= 2)
            {
                crit = r.ReadBool();
                effect = (sbyte)r.ReadSByte();
            }
            return new MobHit(index, hp, damage, crit, effect) { ExtraBytes = Msg.Extra(payload, r) };
        });
    }
}

/// <summary>
/// <c>-12</c>: quái chết. ubyte chỉ số, long sát thương, bool chí mạng; rồi (tùy chọn, client bọc try) byte <c>n</c> và
/// <c>n</c> vật rơi: short id, short mẫu, short x, short y, int chủ. Bắt gói: 4 gói, đều 10 byte (không có phần vật rơi).
/// </summary>
public sealed record MobDeath(int Index, long Damage, bool Crit, DroppedItem[] Drops)
{
    public const sbyte Command = -12;

    public int ExtraBytes { get; init; }

    public static MobDeath Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-12", () =>
        {
            int index = r.ReadByte();
            long damage = r.ReadLong();
            bool crit = r.ReadBool();
            DroppedItem[] drops = Array.Empty<DroppedItem>();
            if (Msg.Extra(payload, r) >= 1)
            {
                int n = Msg.Count(r, "-12", "vật rơi");
                drops = Msg.Repeat(n, () =>
                {
                    short id = r.ReadShort(), template = r.ReadShort(), x = r.ReadShort(), y = r.ReadShort();
                    return new DroppedItem(id, template, x, y, r.ReadInt(), null);
                });
            }
            return new MobDeath(index, damage, crit, drops) { ExtraBytes = Msg.Extra(payload, r) };
        });
    }
}

/// <summary><c>-13</c>: quái hồi sinh. ubyte chỉ số, sbyte <c>dD</c>, sbyte <c>aK</c> (ý nghĩa chưa rõ), long HP.</summary>
public sealed record MobRespawn(int Index, sbyte UnknownDd, sbyte UnknownAk, long Hp)
{
    public const sbyte Command = -13;

    public int ExtraBytes { get; init; }

    public static MobRespawn Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-13", () =>
            new MobRespawn(r.ReadByte(), (sbyte)r.ReadSByte(), (sbyte)r.ReadSByte(), r.ReadLong()) { ExtraBytes = Msg.Extra(payload, r) });
    }
}

/// <summary><c>-11</c>: quái đánh MÌNH. ubyte chỉ số, long sát thương, long (tùy chọn; client gán vào <c>aR</c>, ý nghĩa chưa rõ).</summary>
public sealed record MobAttacksMe(int Index, long Damage, long? Extra)
{
    public const sbyte Command = -11;

    public int ExtraBytes { get; init; }

    public static MobAttacksMe Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-11", () =>
        {
            int index = r.ReadByte();
            long damage = r.ReadLong();
            long? extra = Msg.Extra(payload, r) >= 8 ? r.ReadLong() : null;
            return new MobAttacksMe(index, damage, extra) { ExtraBytes = Msg.Extra(payload, r) };
        });
    }
}

/// <summary>
/// <c>-10</c>: quái đánh người chơi khác. ubyte chỉ số quái, int id người, long sát thương (client tính HP người đó =
/// <c>HP - sát thương</c>), long tùy chọn.
/// </summary>
public sealed record MobAttacksPlayer(int Index, int PlayerId, long Damage, long? Extra)
{
    public const sbyte Command = -10;

    public int ExtraBytes { get; init; }

    public static MobAttacksPlayer Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-10", () =>
        {
            int index = r.ReadByte();
            int playerId = r.ReadInt();
            long damage = r.ReadLong();
            long? extra = Msg.Extra(payload, r) >= 8 ? r.ReadLong() : null;
            return new MobAttacksPlayer(index, playerId, damage, extra) { ExtraBytes = Msg.Extra(payload, r) };
        });
    }
}
