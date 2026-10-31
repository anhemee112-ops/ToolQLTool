namespace ToolQL.Protocol.Messages;

public sealed record Portal(short X1, short Y1, short X2, short Y2, bool FlagA, bool FlagB, string TargetName);

/// <summary>Quái trong map. Chỉ số trong danh sách (0, 1, 2...) là mã quái dùng ở các gói đánh/chết.</summary>
public sealed record Mob(
    int Index, bool[] Flags, short Type, int Level, long Hp, int Unused, long HpMax, short X, short Y,
    int UnknownJl, int UnknownAk, bool Special);

public sealed record Npc(int Index, short X, short Y, int Template, short Icon);

/// <summary>Vật rơi. <paramref name="Extra"/> chỉ có khi <paramref name="Value"/> bằng -2 (giá trị đặc biệt <c>dN[14]</c>, tính bằng mô phỏng bytecode).</summary>
public sealed record DroppedItem(short A, short B, short C, short D, int Value, short? Extra);

public sealed record MapBackground(short Id, short X, short Y);

/// <summary>Phần đầu gói <c>-24</c> (chỉ có trong <c>-24</c>, không có trong <c>-28</c> sub 10).</summary>
public sealed record MapHeader(int MapId, int UnknownN, int TileSetId, int UnknownDo, int Skipped, string Name, int UnknownF);

/// <summary>Thân gói <c>-24</c> (<c>cj_0.i</c>).</summary>
public sealed record MapBody(
    short X, short Y, Portal[] Portals, Mob[] Mobs, int SkippedByte, Npc[] Npcs, DroppedItem[] Items,
    MapBackground[] Backgrounds, (string Key, string Value)[] Keys, int UnknownDp, int FinalByte);

/// <summary>
/// Gói <c>-24</c> (S→C): thông tin map. Không chứa lưới ô lát. <paramref name="Bf"/> là byte cuối bọc try (có thể thiếu).
/// Nguồn: <c>cj_0.b</c> nhánh -24 và <c>cj_0.i</c>, đã đối chiếu lại với mã dịch ngược.
/// </summary>
public sealed record MapInfo(MapHeader Header, MapBody Body, int? Bf)
{
    public const sbyte Command = -24;

    public int ExtraBytes { get; init; }

    public static MapInfo Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-24", () =>
        {
            var header = new MapHeader(
                MapId: r.ReadByte(), UnknownN: r.ReadSByte(), TileSetId: r.ReadSByte(), UnknownDo: r.ReadSByte(),
                Skipped: r.ReadSByte(), Name: r.ReadUtf(), UnknownF: r.ReadSByte());
            var body = ReadBody(r);
            int? bf = payload.Length - r.Position >= 1 ? r.ReadSByte() : null;
            return new MapInfo(header, body, bf) { ExtraBytes = Msg.Extra(payload, r) };
        });
    }

    /// <summary>Đọc thân giống <c>cj_0.i</c>: x, y của mình, cổng, quái, NPC, vật rơi, hình nền, khóa/giá trị.</summary>
    internal static MapBody ReadBody(JavaStreamReader r)
    {
        const string name = "-24";
        short x = r.ReadShort();
        short y = r.ReadShort();

        var portals = Msg.Repeat(Msg.Count(r, name, "cổng"), () =>
            new Portal(r.ReadShort(), r.ReadShort(), r.ReadShort(), r.ReadShort(), r.ReadBool(), r.ReadBool(), r.ReadUtf()));

        int nMobs = Msg.Count(r, name, "quái");
        var mobs = new Mob[nMobs];
        for (int i = 0; i < nMobs; i++)
        {
            var flags = new[] { r.ReadBool(), r.ReadBool(), r.ReadBool(), r.ReadBool(), r.ReadBool() };
            mobs[i] = new Mob(i, flags, r.ReadShort(), r.ReadSByte(), r.ReadLong(), r.ReadSByte(), r.ReadLong(),
                r.ReadShort(), r.ReadShort(), r.ReadSByte(), r.ReadSByte(), r.ReadBool());
        }

        int skipped = r.ReadSByte();

        int nNpcs = Msg.Count(r, name, "NPC");
        var npcs = new Npc[nNpcs];
        for (int i = 0; i < nNpcs; i++)
            npcs[i] = new Npc(r.ReadSByte(), r.ReadShort(), r.ReadShort(), r.ReadSByte(), r.ReadShort());

        var items = Msg.Repeat(Msg.Count(r, name, "vật rơi"), () =>
        {
            short a = r.ReadShort(), b = r.ReadShort(), c = r.ReadShort(), d = r.ReadShort();
            int value = r.ReadInt();
            short? extra = value == -2 ? r.ReadShort() : null;
            return new DroppedItem(a, b, c, d, value, extra);
        });

        var backgrounds = Msg.Repeat(Msg.ShortCount(r, name, "hình nền"), () =>
            new MapBackground(r.ReadShort(), r.ReadShort(), r.ReadShort()));

        var keys = Msg.Repeat(Msg.ShortCount(r, name, "khóa"), () => (r.ReadUtf(), r.ReadUtf()));

        int dp = r.ReadSByte();
        int final = r.ReadSByte();
        return new MapBody(x, y, portals, mobs, skipped, npcs, items, backgrounds, keys, dp, final);
    }
}

/// <summary>
/// Gói <c>-28</c> sub 10 (S→C): sub, rộng, cao, lưới ô lát, rồi <b>1 byte cuối</b> (<see cref="Trailer"/>).
/// Rộng/cao đọc không dấu (như file <c>/mymap/&lt;id&gt;</c>). Thân giống <c>-24</c> không nằm trong gói này.
/// <para>
/// Bắt gói thật (map 23, lưới 32x20) có đúng 1 byte <c>00</c> sau lưới; ý nghĩa chưa rõ (có thể là <c>bf</c>).
/// </para>
/// </summary>
public sealed record MapGrid(int Width, int Height, byte[] Tiles)
{
    public const sbyte Command = -28;
    public const int Sub = 10;

    /// <summary>Byte ngay sau lưới nếu có (bắt gói: <c>00</c>); <c>null</c> nếu gói hết ngay sau lưới.</summary>
    public byte? Trailer { get; init; }

    public int ExtraBytes { get; init; }

    public static MapGrid Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-28 sub 10", () =>
        {
            int sub = r.ReadSByte();
            if (sub != Sub) throw new InvalidDataException($"Gói -28 sub 10: sub là {sub}.");
            int w = r.ReadByte();
            int h = r.ReadByte();
            var tiles = r.ReadBytes(w * h);
            byte? trailer = payload.Length - r.Position >= 1 ? (byte)r.ReadByte() : null;
            return new MapGrid(w, h, tiles) { Trailer = trailer, ExtraBytes = Msg.Extra(payload, r) };
        });
    }
}
