namespace ToolQL.Protocol;

/// <summary>Một server game. <paramref name="FallbackIp"/> là IP dự phòng ghi trong client (có thể đã cũ).</summary>
public sealed record GameServer(string Name, string Host, int Port, string FallbackIp);

/// <summary>Danh sách server trong client tham chiếu (xem <c>docs/protocol/servers.md</c>, từ-mã, chưa thử kết nối).</summary>
public static class ServerList
{
    public static readonly IReadOnlyList<GameServer> All = new GameServer[]
    {
        new("Vũ trụ 1", "dragon1.teamobi.com", 14445, "112.213.94.23"),
        new("Vũ trụ 2", "dragon2.teamobi.com", 14445, "210.211.109.199"),
        new("Vũ trụ 3", "dragon3.teamobi.com", 14445, "112.213.85.88"),
        new("Vũ trụ 4", "dragon4.teamobi.com", 14445, "27.0.12.164"),
        new("Vũ trụ 5", "dragon5.teamobi.com", 14445, "27.0.12.16"),
        new("Vũ trụ 6", "dragon6.teamobi.com", 14445, "27.0.12.173"),
        new("Vũ trụ 7", "dragon7.teamobi.com", 14445, "112.213.94.223"),
        new("Vũ trụ 8", "dragon10.teamobi.com", 14446, "27.0.14.66"),
        new("Vũ trụ 9", "dragon10.teamobi.com", 14447, "27.0.14.66"),
        new("Vũ trụ 10", "dragon10.teamobi.com", 14445, "27.0.14.66"),
        new("Vũ trụ 11", "dragon11.teamobi.com", 14445, "112.213.85.35"),
        new("Võ đài liên vũ trụ", "dragonwar.teamobi.com", 20000, "27.0.12.173"),
        new("Universe 1", "dragon.indonaga.com", 14445, "52.74.230.22"),
        new("Naga", "dragon.indonaga.com", 14446, "52.74.230.22"),
    };

    /// <summary>
    /// Tìm theo số thứ tự (bắt đầu từ 1), tên (không phân biệt hoa thường) hoặc tên miền (lấy mục đầu tiên).
    /// Trả null nếu không thấy.
    /// </summary>
    public static GameServer? Find(string query)
    {
        query = query.Trim();
        if (int.TryParse(query, out int index))
            return index >= 1 && index <= All.Count ? All[index - 1] : null;
        return All.FirstOrDefault(s => s.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
            ?? All.FirstOrDefault(s => s.Host.Equals(query, StringComparison.OrdinalIgnoreCase));
    }
}
