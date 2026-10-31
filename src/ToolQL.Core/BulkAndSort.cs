using ToolQL.Protocol;

namespace ToolQL.Core;

public sealed record ParsedAccount(string User, string Server, string Password, string Note);

public sealed record BulkParseResult(IReadOnlyList<ParsedAccount> Accounts, IReadOnlyList<string> Errors);

/// <summary>
/// Đọc danh sách acc dán vào hộp "Thêm nhiều tài khoản": mỗi dòng <c>TK|SV|MK</c> hoặc <c>TK|SV|MK|GHI CHÚ</c>, SV là SỐ của server
/// (từ 1, như cột SV trên bảng). Dòng trống và dòng bắt đầu bằng <c>#</c> bị bỏ qua. Giống Tool 2: CÓ MỘT DÒNG SAI LÀ KHÔNG TRẢ ACC NÀO,
/// mọi dòng sai được báo kèm số dòng. Thông báo lỗi không bao giờ chứa mật khẩu.
/// </summary>
public static class BulkAccountParser
{
    public static BulkParseResult Parse(string text)
    {
        var accounts = new List<ParsedAccount>();
        var errors = new List<string>();
        var firstLineOf = new Dictionary<(string, string), int>();
        var lines = text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            int number = i + 1;
            string line = lines[i].Trim('\r', ' ', '\t');
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var cols = line.Split('|');
            if (cols.Length > 4) { errors.Add($"Dòng {number}: thừa cột (chỉ có TK|SV|MK hoặc TK|SV|MK|GHI CHÚ)."); continue; }
            string user = cols[0].Trim();
            if (user.Length == 0) { errors.Add($"Dòng {number}: thiếu tên đăng nhập."); continue; }
            if (cols.Length < 3 || cols[2].Length == 0) { errors.Add($"Dòng {number}: thiếu mật khẩu."); continue; }
            if (!int.TryParse(cols[1].Trim(), out int sv))
            {
                errors.Add($"Dòng {number}: server \"{cols[1].Trim()}\" không phải số (1-{ServerList.All.Count}).");
                continue;
            }
            if (sv < 1 || sv > ServerList.All.Count)
            {
                errors.Add($"Dòng {number}: server {sv} không có (1-{ServerList.All.Count}).");
                continue;
            }

            string server = ServerList.All[sv - 1].Name;
            if (firstLineOf.TryGetValue((user, server), out int first))
            {
                errors.Add($"Dòng {number}: trùng acc {user} trên {server} với dòng {first}.");
                continue;
            }
            firstLineOf[(user, server)] = number;
            accounts.Add(new ParsedAccount(user, server, cols[2], cols.Length == 4 ? cols[3].Trim() : ""));
        }

        return errors.Count > 0 ? new BulkParseResult(Array.Empty<ParsedAccount>(), errors) : new BulkParseResult(accounts, errors);
    }
}

/// <summary>So sánh chuỗi mà dãy chữ số được coi là số (acc2 đứng trước acc10), không phân biệt hoa thường.</summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        x ??= "";
        y ??= "";
        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
            {
                int si = i, sj = j;
                while (i < x.Length && char.IsDigit(x[i])) i++;
                while (j < y.Length && char.IsDigit(y[j])) j++;
                string a = x[si..i].TrimStart('0'), b = y[sj..j].TrimStart('0');
                if (a.Length != b.Length) return a.Length.CompareTo(b.Length);   // không đổi sang số nên không tràn
                int c = string.CompareOrdinal(a, b);
                if (c != 0) return c;
            }
            else
            {
                int c = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                if (c != 0) return c;
                i++;
                j++;
            }
        }
        return (x.Length - i).CompareTo(y.Length - j);   // phần còn lại: chuỗi ngắn hơn đứng trước; giống nhau (không phân biệt hoa thường) thì bằng nhau
    }
}
