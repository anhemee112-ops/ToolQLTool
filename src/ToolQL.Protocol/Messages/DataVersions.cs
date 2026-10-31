namespace ToolQL.Protocol.Messages;

/// <summary>
/// Gói <c>-28</c> sub 4 (S→C): 4 phiên bản dữ liệu server đang có (nguồn: <c>cj_0.f</c> nhánh 4).
/// Phiên bản đọc bằng byte có dấu như client.
/// </summary>
/// <param name="Skipped">Byte thứ năm, client đọc rồi bỏ.</param>
/// <param name="Captions">Cuối gói: <c>n</c> số long (client lưu vào <c>cu_0.h</c>, nhãn log "CAPTION").</param>
public sealed record DataVersions(int Data, int Map, int Skill, int Item, int Skipped, long[] Captions)
{
    public const sbyte Command = -28;
    public const int Sub = 4;

    /// <summary>Số byte payload còn thừa sau khi đọc (0 là khớp tài liệu).</summary>
    public int ExtraBytes { get; init; }

    public static DataVersions Parse(byte[] payload)
    {
        var r = Msg.Open(payload);
        return Msg.Guard("-28 sub 4", () =>
        {
            int sub = r.ReadSByte();
            if (sub != Sub) throw new InvalidDataException($"Gói -28 sub 4: sub là {sub}.");
            int data = r.ReadSByte(), map = r.ReadSByte(), skill = r.ReadSByte(), item = r.ReadSByte();
            int skipped = r.ReadSByte();
            int n = Msg.Count(r, "-28 sub 4", "caption");
            var captions = Msg.Repeat(n, r.ReadLong);
            return new DataVersions(data, map, skill, item, skipped, captions) { ExtraBytes = Msg.Extra(payload, r) };
        });
    }
}
