namespace ToolQL.Protocol.Messages;

/// <summary>Một cờ va chạm: giá trị cờ và danh sách mã ô lát (byte có dấu, giữ nguyên như server gửi) nhận cờ đó.</summary>
public sealed record TileFlag(int Value, sbyte[] TileCodes);

/// <summary>
/// Gói <c>-82</c> (S→C): bảng ô lát → cờ va chạm, <c>nBộ</c> bộ, mỗi bộ có danh sách cờ
/// (nguồn: <c>cj_0.b</c> nhánh -82, <c>docs/protocol/map.md</c>).
/// </summary>
public sealed record TileFlagTables(TileFlag[][] Sets)
{
    public const sbyte Command = -82;

    public int ExtraBytes { get; init; }

    public static TileFlagTables Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-82", () =>
        {
            int nSets = Msg.Count(r, "-82", "bộ");
            var sets = Msg.Repeat(nSets, () =>
            {
                int nFlags = Msg.Count(r, "-82", "cờ");
                return Msg.Repeat(nFlags, () =>
                {
                    int value = r.ReadInt();
                    int nCodes = Msg.Count(r, "-82", "mã ô lát");
                    return new TileFlag(value, Msg.Repeat(nCodes, r.ReadSByte));
                });
            });
            return new TileFlagTables(sets) { ExtraBytes = Msg.Extra(payload, r) };
        });
    }
}
