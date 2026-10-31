namespace ToolQL.Protocol.Messages;

/// <summary>Một dòng option của vật phẩm (<c>cj_0.a(cN)</c>): short mã + int giá trị. Mã -1 client bỏ qua nhưng vẫn đọc đủ.</summary>
public sealed record ItemOption(short Id, int Value);

/// <summary>Một ô vật phẩm (trang bị, túi hoặc rương). Ô trống (mã -1) được biểu diễn bằng null.</summary>
public sealed record ItemSlot(short ItemId, int Quantity, string Text1, string Text2, ItemOption[] Options);

/// <summary>
/// Gói <c>-30</c> sub 0 (S→C): nhân vật của mình (nguồn: <c>cj_0.j</c> nhánh 0, đối chiếu lại với mã).
/// Tên trường <c>Unknown*</c> là trường chưa rõ nghĩa; tên trong jar ghi ở comment.
/// </summary>
public sealed record CharacterInfo(
    int Id,
    int UnknownEa,         // ea
    int Race,              // ht (client log "chung toc")
    short Head,            // gD
    string Name,           // gu
    int UnknownAu,         // au
    int UnknownAl,         // al
    long Power,            // as (đoán là sức mạnh)
    short UnknownDz,       // dz
    short UnknownDx,       // dx
    int ClassIndex,        // chỉ số bảng cu_0.a
    short[] SkillIds,      // danh sách short sau byte n
    long LongAi,           // aI
    int IntCq,             // cQ
    int IntGf,             // gF
    ItemSlot?[] Equipment, // c[]
    ItemSlot?[] Bag,       // a[]
    ItemSlot?[] Chest,     // b[]
    (short, short)[] Pairs,
    short[] ThreeShorts,
    int Bm,                // byte so với 1
    int ServerTimeSeconds, // giờ server (giây), client trừ để ra độ lệch
    int UnknownAz,         // az
    (short, sbyte, short)? Tail) // y, u, w: bọc try ở client, có thể thiếu
{
    public const sbyte Command = -30;
    public const int Sub = 0;

    public int ExtraBytes { get; init; }

    public static CharacterInfo Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-30 sub 0", () =>
        {
            int sub = r.ReadSByte();
            if (sub != Sub) throw new InvalidDataException($"Gói -30: sub là {sub}, không phải 0.");

            int id = r.ReadInt();
            int ea = r.ReadSByte();
            int race = r.ReadSByte();
            short head = (short)r.ReadShort();
            string name = r.ReadUtf();
            int au = r.ReadSByte();
            int al = r.ReadSByte();
            long power = r.ReadLong();
            short dz = (short)r.ReadShort();
            short dx = (short)r.ReadShort();
            int classIndex = r.ReadSByte();
            int nSkills = Msg.Count(r, "-30 sub 0", "skill");
            var skills = Msg.Repeat(nSkills, () => (short)r.ReadShort());
            long aI = r.ReadLong();
            int cQ = r.ReadInt();
            int gF = r.ReadInt();

            // Trang bị: số lượng đọc không dấu; túi và rương: byte có dấu (như client).
            var equipment = ReadSlots(r, equipmentOptionCountUnsigned: true);
            var bag = ReadSlots(r, equipmentOptionCountUnsigned: false);
            var chest = ReadSlots(r, equipmentOptionCountUnsigned: false);

            int m = Msg.ShortCount(r, "-30 sub 0", "cặp short");
            var pairs = Msg.Repeat(m, () => ((short)r.ReadShort(), (short)r.ReadShort()));
            var three = new[] { (short)r.ReadShort(), (short)r.ReadShort(), (short)r.ReadShort() };
            int bm = r.ReadSByte();
            int serverTime = r.ReadInt();
            int az = r.ReadSByte();

            // Phần cuối bọc try ở client: short, byte, short (5 byte), có thể thiếu.
            (short, sbyte, short)? tail = null;
            if (payload.Length - r.Position >= 5)
                tail = ((short)r.ReadShort(), r.ReadSByte(), (short)r.ReadShort());

            return new CharacterInfo(id, ea, race, head, name, au, al, power, dz, dx, classIndex, skills, aI, cQ, gF,
                equipment, bag, chest, pairs, three, bm, serverTime, az, tail)
            { ExtraBytes = Msg.Extra(payload, r) };
        });
    }

    private static ItemSlot?[] ReadSlots(JavaStreamReader r, bool equipmentOptionCountUnsigned)
    {
        int n = Msg.Count(r, "-30 sub 0", "ô vật phẩm");
        return Msg.Repeat<ItemSlot?>(n, () =>
        {
            short itemId = (short)r.ReadShort();
            if (itemId == -1) return null; // client bỏ qua, không đọc thêm gì
            int quantity = r.ReadInt();
            string t1 = r.ReadUtf();
            string t2 = r.ReadUtf();
            int nOptions = equipmentOptionCountUnsigned ? r.ReadByte() : Msg.Count(r, "-30 sub 0", "option");
            var options = Msg.Repeat(nOptions, () => new ItemOption((short)r.ReadShort(), r.ReadInt()));
            return new ItemSlot(itemId, quantity, t1, t2, options);
        });
    }
}
