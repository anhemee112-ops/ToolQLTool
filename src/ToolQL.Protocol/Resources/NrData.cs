using ToolQL.Protocol.Messages;

namespace ToolQL.Protocol.Resources;

// Bảng đồ họa trong gói -87 (khóa kho "NRdata"). Bố cục theo docs/protocol/resources.md (nguồn: cj_0.a, cu_0, at_0.cf).
// Mỗi bộ đọc trả ExtraBytes (số byte còn thừa): đọc đúng khi bằng 0.

/// <summary>
/// Gói <c>-87</c> (và khóa kho <c>NRdata</c>): byte phiên bản rồi 6 khối, mỗi khối là int độ dài + byte. Thứ tự:
/// dart (phi tiêu), arrow (mũi tên), effect (hiệu ứng), image (ảnh nhỏ), part (phần thân), skill (hoạt ảnh skill).
/// Chỉ tách khối; bên trong từng khối đọc bằng <see cref="ImageTable"/>, <see cref="PartTable"/>.
/// </summary>
public sealed record NrData(int Version, byte[] Dart, byte[] Arrow, byte[] Effect, byte[] Image, byte[] Part, byte[] Skill)
{
    public int ExtraBytes { get; init; }

    public static NrData Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-87", () =>
        {
            int version = r.ReadSByte();
            var blocks = new byte[6][];
            for (int i = 0; i < blocks.Length; i++)
            {
                int length = r.ReadInt();
                if (length < 0 || length > payload.Length - r.Position)
                    throw new InvalidDataException($"Gói -87: khối {i} dài {length}, còn {payload.Length - r.Position} byte.");
                blocks[i] = r.ReadBytes(length);
            }
            return new NrData(version, blocks[0], blocks[1], blocks[2], blocks[3], blocks[4], blocks[5])
            { ExtraBytes = Msg.Extra(payload, r) };
        });
    }
}

/// <summary>Vùng cắt của một sprite trong ảnh atlas: ubyte atlas, short x, y, rộng, cao.</summary>
public sealed record SpriteRect(int Atlas, short X, short Y, short Width, short Height);

/// <summary>
/// <c>NR_image</c>: <c>short n</c>, rồi n × <see cref="SpriteRect"/>. Mã sprite là chỉ số mục. Client bỏ một số mục
/// (xem resources.md); bộ đọc này giữ đủ n mục, việc lọc để nơi dùng quyết định.
/// </summary>
public sealed record ImageTable(SpriteRect[] Sprites)
{
    public int ExtraBytes { get; init; }

    public static ImageTable Parse(byte[] block)
    {
        var r = Msg.Open(block);
        return Msg.Guard("NR_image", () =>
        {
            int n = Msg.ShortCount(r, "NR_image", "sprite");
            var sprites = Msg.Repeat(n, () => new SpriteRect(r.ReadByte(), r.ReadShort(), r.ReadShort(), r.ReadShort(), r.ReadShort()));
            return new ImageTable(sprites) { ExtraBytes = Msg.Extra(block, r) };
        });
    }
}

/// <summary>Một phần của bộ phần thân: mã sprite (chỉ số vào <see cref="ImageTable"/>) và độ lệch x, y.</summary>
public sealed record BodyPartRef(short Sprite, sbyte OffsetX, sbyte OffsetY);

/// <summary>Bộ phần thân: <c>Kind</c> quyết định số phần tử (0 → 3, 1 → 17, 2 → 14, 3 → 2).</summary>
public sealed record BodyPartSet(int Kind, BodyPartRef[] Parts);

/// <summary><c>NR_part</c>: <c>short n</c>, rồi n × (byte loại, số phần tử theo loại × (short, sbyte, sbyte)).</summary>
public sealed record PartTable(BodyPartSet[] Sets)
{
    private static readonly Dictionary<int, int> PartsPerKind = new() { [0] = 3, [1] = 17, [2] = 14, [3] = 2 };

    public int ExtraBytes { get; init; }

    public static PartTable Parse(byte[] block)
    {
        var r = Msg.Open(block);
        return Msg.Guard("NR_part", () =>
        {
            int n = Msg.ShortCount(r, "NR_part", "bộ phần");
            var sets = Msg.Repeat(n, () =>
            {
                int kind = r.ReadSByte();
                if (!PartsPerKind.TryGetValue(kind, out int count))
                    throw new InvalidDataException($"NR_part: loại {kind} không biết.");
                return new BodyPartSet(kind, Msg.Repeat(count, () => new BodyPartRef(r.ReadShort(), r.ReadSByte(), r.ReadSByte())));
            });
            return new PartTable(sets) { ExtraBytes = Msg.Extra(block, r) };
        });
    }
}
