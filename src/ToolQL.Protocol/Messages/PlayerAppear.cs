namespace ToolQL.Protocol.Messages;

/// <summary>Trạng thái đang có của người chơi (mỗi mục: byte, int, int, short; tên trong jar <c>bG</c>).</summary>
public sealed record PlayerStatus(sbyte A, int B, int C, short D);

/// <summary>Thông tin người chơi khác (<c>cj_0.a(ce, cN)</c>), dùng chung cho gói <c>-5</c>.</summary>
public sealed record PlayerInfo(
    int UnknownDt, bool UnknownCf, int PkType, int ClassIndex, int Race, short Head, string Name,
    long Hp, long HpMax, short Body, short Leg, short Bag, int Skipped, short X, short Y,
    short UnknownDz, short UnknownDx, PlayerStatus[] Statuses)
{
    internal static PlayerInfo Read(JavaStreamReader r, string name)
    {
        int dt = r.ReadSByte();
        bool cf = r.ReadBool();
        int pk = r.ReadSByte();
        int cls = r.ReadSByte();
        int race = r.ReadSByte();
        short head = r.ReadShort();
        string playerName = r.ReadUtf();
        long hp = r.ReadLong();
        long hpMax = r.ReadLong();
        short body = r.ReadShort(), leg = r.ReadShort(), bag = r.ReadShort();
        int skipped = r.ReadSByte();
        short x = r.ReadShort(), y = r.ReadShort(), dz = r.ReadShort(), dx = r.ReadShort();
        var statuses = Msg.Repeat(Msg.Count(r, name, "trạng thái"), () =>
            new PlayerStatus(r.ReadSByte(), r.ReadInt(), r.ReadInt(), r.ReadShort()));
        return new PlayerInfo(dt, cf, pk, cls, race, head, playerName, hp, hpMax, body, leg, bag, skipped, x, y, dz, dx, statuses);
    }
}

/// <summary>
/// Gói <c>-5</c> (S→C): người chơi khác xuất hiện trong map (nguồn: <c>cj_0.b</c> nhánh -5; luồng <c>goto</c> của CFR
/// đã được đọc lại, nhánh lỗi của <c>cj_0.a</c> không mô hình hóa). Phần cuối bọc try ở client nên có thể thiếu.
/// </summary>
public sealed record PlayerAppear(
    int Id, int Kind, PlayerInfo Info, int Effect, int UnknownAr, short State, int UnknownAt, int UnknownBm,
    (short, sbyte, short)? Tail)
{
    public const sbyte Command = -5;

    public int ExtraBytes { get; init; }

    public static PlayerAppear Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-5", () =>
        {
            int id = r.ReadInt();
            int kind = r.ReadInt();
            var info = PlayerInfo.Read(r, "-5");
            int effect = r.ReadSByte();
            int ar = r.ReadSByte();
            short state = r.ReadShort();
            int at = r.ReadSByte();
            int bm = r.ReadSByte();
            (short, sbyte, short)? tail = null;
            if (payload.Length - r.Position >= 5)
                tail = (r.ReadShort(), r.ReadSByte(), r.ReadShort());
            return new PlayerAppear(id, kind, info, effect, ar, state, at, bm, tail) { ExtraBytes = Msg.Extra(payload, r) };
        });
    }
}
