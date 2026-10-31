using ToolQL.Protocol;
using ToolQL.Protocol.Messages;

namespace ToolQL.Core;

/// <summary>
/// Kho tệp các bảng dữ liệu server gửi (map, skill, item ...), lưu NGUYÊN payload theo phiên bản của <c>-28</c> sub 4.
/// Nằm ngoài repo (mặc định <c>%LOCALAPPDATA%\ToolQL\cache</c>). Mới chỉ lưu; ToolQL chưa dùng để bỏ qua việc xin bảng,
/// vì chưa kiểm chứng server có cho vào game khi không xin (xem plan v0.1, C4).
/// </summary>
public sealed class DataCache
{
    private readonly string _directory;

    public DataCache(string directory) => _directory = directory;

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ToolQL", "cache");

    public string Directory => _directory;

    /// <summary>Lưu payload của bảng <paramref name="name"/> ở phiên bản <paramref name="version"/>, xóa các phiên bản cũ của bảng đó.</summary>
    public void Save(string name, int version, byte[] payload)
    {
        CheckName(name);
        System.IO.Directory.CreateDirectory(_directory);
        string target = PathOf(name, version);
        string temp = target + ".tmp";
        File.WriteAllBytes(temp, payload);
        File.Move(temp, target, overwrite: true);
        foreach (var old in System.IO.Directory.GetFiles(_directory, name + ".v*.bin"))
            if (!string.Equals(old, target, StringComparison.OrdinalIgnoreCase)) File.Delete(old);
    }

    /// <summary>Đọc bảng đúng phiên bản; null nếu chưa có.</summary>
    public byte[]? TryLoad(string name, int version)
    {
        CheckName(name);
        string path = PathOf(name, version);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    /// <summary>Phiên bản đang lưu của bảng; null nếu chưa có.</summary>
    public int? StoredVersion(string name)
    {
        CheckName(name);
        if (!System.IO.Directory.Exists(_directory)) return null;
        foreach (var file in System.IO.Directory.GetFiles(_directory, name + ".v*.bin"))
        {
            string tail = Path.GetFileNameWithoutExtension(file)[(name.Length + 2)..];
            if (int.TryParse(tail, out int v)) return v;
        }
        return null;
    }

    private string PathOf(string name, int version) => Path.Combine(_directory, $"{name}.v{version}.bin");

    /// <summary>Tên chỉ gồm chữ thường, số và dấu gạch ngang: không cho ký tự đường dẫn lọt vào.</summary>
    private static void CheckName(string name)
    {
        if (name.Length == 0 || !name.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
            throw new ArgumentException("Tên bảng không hợp lệ: " + name, nameof(name));
    }
}

/// <summary>
/// Ghi các gói bảng nhận được trong MỘT lần nối vào <see cref="DataCache"/>. Tên tệp: loại bảng + số thứ tự gói cùng
/// loại (sub 8 về nhiều lần: <c>item-0</c>, <c>item-1</c> ...). Payload lưu thô, chưa diễn giải.
/// </summary>
public sealed class TableRecorder
{
    private readonly DataCache _cache;
    private readonly Dictionary<string, int> _counts = new();

    public TableRecorder(DataCache cache) => _cache = cache;

    /// <summary>Lưu nếu <paramref name="packet"/> là gói bảng; trả true khi đã lưu. Lỗi ghi tệp không làm hỏng phiên.</summary>
    public bool Record(Packet packet, DataVersions? versions)
    {
        if (versions is null) return false;
        var (kind, version) = Classify(packet, versions);
        if (kind is null) return false;

        int n = _counts.GetValueOrDefault(kind);
        _counts[kind] = n + 1;
        try
        {
            _cache.Save($"{kind}-{n}", version, packet.Payload);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static (string? Kind, int Version) Classify(Packet p, DataVersions v)
    {
        sbyte? sub = p.Payload.Length > 0 ? (sbyte)p.Payload[0] : null;
        return (p.Command, sub) switch
        {
            (-87, _) => ("data", v.Data),
            (-28, 6) => ("map", v.Map),
            (-28, 7) => ("skill", v.Skill),
            (-28, 8) => ("item", v.Item),
            (12, _) => ("itemlist", v.Item),
            _ => (null, 0),
        };
    }
}
