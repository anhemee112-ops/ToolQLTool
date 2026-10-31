namespace ToolQL.Probe;

/// <summary>
/// Tài khoản thử đọc từ <c>%USERPROFILE%\.config\toolql\test-account.env</c> (ngoài repo).
/// Mật khẩu không bao giờ được in ra: <see cref="ToString"/> đã che.
/// </summary>
public sealed record TestAccount(string User, string Password, string? Server)
{
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "toolql", "test-account.env");

    /// <summary>Đọc các dòng <c>KEY=VALUE</c> (bỏ dòng trống và dòng bắt đầu bằng <c>#</c>). Thiếu tên hoặc mật khẩu thì ném lỗi.</summary>
    public static TestAccount Load(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path))
            throw new FileNotFoundException($"Không thấy file tài khoản thử: {path}");

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            values[line[..eq].Trim()] = Unquote(line[(eq + 1)..].Trim());
        }

        values.TryGetValue("TOOLQL_TEST_USER", out var user);
        values.TryGetValue("TOOLQL_TEST_PASS", out var pass);
        values.TryGetValue("TOOLQL_TEST_SERVER", out var server);
        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            throw new InvalidDataException($"File {path} thiếu TOOLQL_TEST_USER hoặc TOOLQL_TEST_PASS (đang trống).");
        return new TestAccount(user, pass, string.IsNullOrEmpty(server) ? null : server);
    }

    private static string Unquote(string v) =>
        v.Length >= 2 && (v[0] == '"' && v[^1] == '"' || v[0] == '\'' && v[^1] == '\'') ? v[1..^1] : v;

    public override string ToString() => $"TestAccount {{ User = {User}, Password = ***, Server = {Server} }}";
}
