using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ToolQL.Core;

/// <summary>Mã hóa mật khẩu để lưu xuống đĩa. Tách giao diện để test không phụ thuộc DPAPI.</summary>
public interface IPasswordProtector
{
    string Protect(string plain);

    /// <summary>Giải mã; ném <see cref="CryptographicException"/> nếu không giải được (sai user/máy hoặc dữ liệu hỏng).</summary>
    string Unprotect(string protectedText);
}

/// <summary>DPAPI phạm vi <c>CurrentUser</c>: chép file sang user hoặc máy khác thì không giải được, phải nhập lại mật khẩu.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class DpapiPasswordProtector : IPasswordProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ToolQL.account.v1");

    public string Protect(string plain) =>
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser));

    public string Unprotect(string protectedText)
    {
        try
        {
            return Encoding.UTF8.GetString(
                ProtectedData.Unprotect(Convert.FromBase64String(protectedText), Entropy, DataProtectionScope.CurrentUser));
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Mật khẩu đã lưu bị hỏng.", ex);
        }
    }
}

/// <summary>
/// Một acc đã lưu. <see cref="ProtectedPassword"/> là mật khẩu đã mã hóa (không phải mật khẩu thật); <c>ToString</c> không in nó.
/// </summary>
public sealed record AccountEntry(Guid Id, string User, string ProtectedPassword, string Server, string Note)
{
    /// <summary>Dấu ✓ của Auto login: acc có dấu này thì tool tự giữ cho luôn chạy khi công tắc Auto login bật. File cũ không có trường này thì là false.</summary>
    public bool Tick { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Id = ").Append(Id).Append(", User = ").Append(User).Append(", Server = ").Append(Server);
        return true;
    }
}

/// <summary>
/// Kho acc dạng JSON ngoài repo (mặc định <c>%APPDATA%\ToolQL\accounts.json</c>); mật khẩu mã hóa bằng
/// <see cref="IPasswordProtector"/>. Không bao giờ ghi mật khẩu thật xuống đĩa hay vào thông báo lỗi.
/// Không an toàn đa luồng: giao diện gọi từ luồng UI.
/// </summary>
public sealed class AccountStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly string _path;
    private readonly IPasswordProtector _protector;
    private readonly List<AccountEntry> _entries = new();

    public AccountStore(string path, IPasswordProtector protector)
    {
        _path = path;
        _protector = protector;
    }

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ToolQL", "accounts.json");

    public IReadOnlyList<AccountEntry> Entries => _entries;

    /// <summary>Đọc từ đĩa (chưa có file thì danh sách rỗng). File hỏng thì ném <see cref="InvalidDataException"/>, không ghi đè.</summary>
    public void Load()
    {
        _entries.Clear();
        if (!File.Exists(_path)) return;
        try
        {
            var list = JsonSerializer.Deserialize<List<AccountEntry>>(File.ReadAllText(_path, Encoding.UTF8));
            if (list is null || list.Any(e => string.IsNullOrWhiteSpace(e.User) || e.ProtectedPassword is null))
                throw new InvalidDataException("File acc thiếu dữ liệu: " + _path);
            _entries.AddRange(list);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"File acc không đọc được ({_path}): {ex.Message}", ex);
        }
    }

    public AccountEntry Add(string user, string password, string server, string note = "")
    {
        if (string.IsNullOrWhiteSpace(user)) throw new ArgumentException("Tên đăng nhập trống.", nameof(user));
        if (_entries.Any(e => e.User == user && e.Server == server))
            throw new InvalidOperationException($"Đã có acc {user} trên server {server}.");
        var entry = new AccountEntry(Guid.NewGuid(), user, _protector.Protect(password), server, note);
        _entries.Add(entry);
        Save();
        return entry;
    }

    /// <summary>Sửa server và ghi chú; <paramref name="newPassword"/> null nghĩa là giữ mật khẩu cũ.</summary>
    public AccountEntry Update(Guid id, string server, string note, string? newPassword = null)
    {
        int i = IndexOf(id);
        var old = _entries[i];
        if (_entries.Any(e => e.Id != id && e.User == old.User && e.Server == server))
            throw new InvalidOperationException($"Đã có acc {old.User} trên server {server}.");
        var entry = old with
        {
            Server = server,
            Note = note,
            ProtectedPassword = newPassword is null ? old.ProtectedPassword : _protector.Protect(newPassword),
        };
        _entries[i] = entry;
        Save();
        return entry;
    }

    /// <summary>Đặt hoặc bỏ dấu ✓ Auto login cho một hay nhiều acc, lưu một lần. Id không có thì bỏ qua.</summary>
    public void SetTick(IEnumerable<Guid> ids, bool tick)
    {
        bool changed = false;
        foreach (var id in ids)
        {
            int i = _entries.FindIndex(e => e.Id == id);
            if (i < 0 || _entries[i].Tick == tick) continue;
            _entries[i] = _entries[i] with { Tick = tick };
            changed = true;
        }
        if (changed) Save();
    }

    public void Remove(Guid id)
    {
        _entries.RemoveAt(IndexOf(id));
        Save();
    }

    /// <summary>
    /// Giải mã mật khẩu để đăng nhập. Không giải được (file chép từ user/máy khác) thì ném
    /// <see cref="InvalidDataException"/> báo phải nhập lại mật khẩu.
    /// </summary>
    public AccountCredentials GetCredentials(Guid id)
    {
        var entry = _entries[IndexOf(id)];
        try
        {
            return new AccountCredentials(entry.User, _protector.Unprotect(entry.ProtectedPassword));
        }
        catch (CryptographicException ex)
        {
            throw new InvalidDataException($"Không giải được mật khẩu của {entry.User}; hãy nhập lại mật khẩu.", ex);
        }
    }

    private int IndexOf(Guid id)
    {
        int i = _entries.FindIndex(e => e.Id == id);
        if (i < 0) throw new KeyNotFoundException("Không có acc với id " + id);
        return i;
    }

    /// <summary>Ghi qua file tạm rồi thay thế để mất điện giữa chừng không làm hỏng file cũ.</summary>
    private void Save()
    {
        string? dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        string temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_entries, Json), new UTF8Encoding(false));
        File.Move(temp, _path, overwrite: true);
    }
}
