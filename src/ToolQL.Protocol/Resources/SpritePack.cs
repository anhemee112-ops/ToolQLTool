using ToolQL.Protocol.Messages;

namespace ToolQL.Protocol.Resources;

// Gói sprite "dữ liệu + ảnh" (quái, hiệu ứng). Bố cục theo docs/protocol/render.md (nguồn: cp.a). Mỗi bộ đọc trả
// ExtraBytes: đọc đúng khi bằng 0.

/// <summary>Hình con: vùng cắt <c>(X, Y, Width, Height)</c> trong ảnh PNG của gói.</summary>
public sealed record SubImage(int Id, int X, int Y, int Width, int Height);

/// <summary>Một mảnh của khung: đặt hình con số <see cref="SubImage"/> tại <c>(X, Y)</c> (tọa độ ngắn có dấu, tương đối điểm neo).</summary>
public sealed record FramePart(short X, short Y, int SubImage);

public sealed record SpriteFrame(FramePart[] Parts);

/// <summary>
/// Khối dữ liệu của gói sprite: <c>byte nCon</c>, n × hình con, <c>short nKhung</c>, n × khung, <c>short nSeq</c>, n × short.
/// Hình con: byte id, rồi x, y, rộng, cao. Với <c>kiểu</c> 0 và 1 cả bốn là ubyte; với kiểu khác x, y là short (rộng, cao vẫn
/// ubyte). Chỉ <c>kiểu</c> 0 đã kiểm bằng gói thật; kiểu khác mới theo mã. Danh sách <see cref="Sequence"/> giữ nguyên các số short
/// (số ≥ 0 là chỉ số khung, số âm là dấu chốt hoạt ảnh, xem render.md); chưa diễn giải.
/// </summary>
public sealed record SpritePack(SubImage[] SubImages, SpriteFrame[] Frames, short[] Sequence)
{
    public int ExtraBytes { get; init; }

    public static SpritePack Parse(byte[] block, int kind)
    {
        var r = Msg.Open(block);
        return Msg.Guard("sprite", () =>
        {
            bool shortXy = kind is not (0 or 1);
            int nSub = Msg.Count(r, "sprite", "hình con");
            var subs = Msg.Repeat(nSub, () =>
            {
                int id = r.ReadSByte();
                int x = shortXy ? r.ReadShort() : r.ReadByte();
                int y = shortXy ? r.ReadShort() : r.ReadByte();
                return new SubImage(id, x, y, r.ReadByte(), r.ReadByte());
            });

            int nFrames = Msg.ShortCount(r, "sprite", "khung");
            var frames = Msg.Repeat(nFrames, () =>
            {
                int m = Msg.Count(r, "sprite", "mảnh");
                return new SpriteFrame(Msg.Repeat(m, () => new FramePart(r.ReadShort(), r.ReadShort(), r.ReadSByte())));
            });

            int nSeq = Msg.ShortCount(r, "sprite", "danh sách hoạt ảnh");
            var sequence = Msg.Repeat(nSeq, () => r.ReadShort());
            return new SpritePack(subs, frames, sequence) { ExtraBytes = Msg.Extra(block, r) };
        });
    }
}

/// <summary>
/// Gói <c>11</c> (S→C): sprite quái. short loại quái, byte kiểu, khối dữ liệu (int độ dài + byte), khối PNG (int độ dài + byte),
/// byte cuối (<see cref="Tail"/>); nếu byte cuối là 1 hoặc 2 thì còn <see cref="Extra"/> (<c>byte n; n × (byte m, m × byte)</c>).
/// Bắt gói: 1 gói (loại 94, kiểu 0, byte cuối 0): tiêu thụ đúng cả gói, nhưng khối dữ liệu còn 2 byte thừa sau danh sách hoạt ảnh (xem verification.md). Phần <see cref="Extra"/> CHƯA từng gặp.
/// </summary>
public sealed record MobSpritePack(int Type, int Kind, SpritePack Pack, byte[] Png, int Tail, byte[][]? Extra)
{
    public const sbyte Command = 11;

    public int ExtraBytes { get; init; }

    public static MobSpritePack Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("11", () =>
        {
            int type = r.ReadShort();
            int kind = r.ReadSByte();
            byte[] data = ReadBlock(r, payload, "khối dữ liệu");
            byte[] png = ReadBlock(r, payload, "khối PNG");
            int tail = r.ReadSByte();
            byte[][]? extra = null;
            if (tail is 1 or 2)
            {
                int n = Msg.Count(r, "11", "bảng phụ");
                extra = Msg.Repeat(n, () => r.ReadBytes(Msg.Count(r, "11", "mục bảng phụ")));
            }
            return new MobSpritePack(type, kind, SpritePack.Parse(data, kind), png, tail, extra) { ExtraBytes = Msg.Extra(payload, r) };
        });
    }

    private static byte[] ReadBlock(JavaStreamReader r, byte[] payload, string what)
    {
        int length = r.ReadInt();
        if (length < 0 || length > payload.Length - r.Position)
            throw new InvalidDataException($"Gói 11: {what} dài {length}, còn {payload.Length - r.Position} byte.");
        return r.ReadBytes(length);
    }
}
