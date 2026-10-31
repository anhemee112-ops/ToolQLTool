using System.Text;
using System.Text.RegularExpressions;
using ToolQL.Protocol;

namespace ToolQL.Core;

/// <summary>
/// Kho RMS dạng tệp của MicroEmulator (AngelChip), dùng để ghi sẵn tài khoản cho client trước khi mở. Định dạng đối chiếu từng byte với
/// tệp do chính client ghi (2026-10-31): UTF tên kho, int phiên bản (1), long thời điểm sửa (ms), int mã bản ghi kế tiếp (2),
/// rồi MỘT bản ghi: int mã (1), int độ dài, byte. Client đặt tên kho là <c>vj</c> + khóa và lưu mỗi khóa trong một tệp <c>vj&lt;khóa&gt;.rs</c>.
/// </summary>
public static class RmsFile
{
    private static readonly Regex KeyPattern = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    public static byte[] Build(string storeName, byte[] record, long modifiedMillis)
    {
        var w = new JavaStreamWriter();
        w.WriteUtf(storeName);
        w.WriteInt(1);                  // phiên bản
        w.WriteLong(modifiedMillis);
        w.WriteInt(2);                  // mã bản ghi kế tiếp
        w.WriteInt(1);                  // mã bản ghi
        w.WriteInt(record.Length);
        w.WriteBytes(record);
        return w.ToArray();
    }

    public static string PathOf(string suiteDir, string key) => Path.Combine(suiteDir, $"vj{CheckedKey(key)}.rs");

    /// <summary>Ghi kho <c>vj&lt;key&gt;.rs</c> với một bản ghi, qua tệp tạm rồi thay thế.</summary>
    public static void Write(string suiteDir, string key, byte[] record, DateTimeOffset modified)
    {
        string target = PathOf(suiteDir, key);
        Directory.CreateDirectory(suiteDir);
        string temp = target + ".tmp";
        File.WriteAllBytes(temp, Build("vj" + key, record, modified.ToUnixTimeMilliseconds()));
        File.Move(temp, target, overwrite: true);
    }

    public static void WriteString(string suiteDir, string key, string value, DateTimeOffset modified) =>
        Write(suiteDir, key, Encoding.UTF8.GetBytes(value), modified);

    public static void Delete(string suiteDir, string key)
    {
        string path = PathOf(suiteDir, key);
        if (File.Exists(path)) File.Delete(path);
    }

    private static string CheckedKey(string key)
    {
        if (string.IsNullOrEmpty(key) || !KeyPattern.IsMatch(key)) throw new ArgumentException("Khóa kho RMS không hợp lệ.", nameof(key));
        return key;
    }
}

/// <summary>
/// Thư mục dữ liệu riêng của một acc: <c>&lt;thư mục acc&gt;\home</c> là <c>user.home</c> của tiến trình client, trong đó MicroEmulator giữ
/// RMS ở <c>.microemulator\suite-null</c>. Lần đầu sao chép từ bản mẫu (dữ liệu game đã tải sẵn) để khỏi tải lại; mỗi lần mở ghi lại
/// tài khoản, mật khẩu và server. Mật khẩu nằm DẠNG CHỮ trong kho của client khi nó chạy (client đọc từ đó); <see cref="ClearCredentials"/>
/// xóa khi client dừng.
/// </summary>
public static class ClientDirectory
{
    public static string SuiteDir(string accountDir) => Path.Combine(accountDir, "home", ".microemulator", "suite-null");

    /// <param name="templateDir">Thư mục chứa <c>home\</c> mẫu (đã tải dữ liệu game).</param>
    /// <param name="serverIndex">Chỉ số server trong danh sách của client (0 = Vũ trụ 1). Mới xác nhận với 0.</param>
    /// <param name="writeCredentials">False: không ghi tài khoản và mật khẩu (xóa cái cũ) để client mở ra mà KHÔNG tự đăng nhập; dùng khi thử mở/xếp cửa sổ.</param>
    /// <param name="lowGraphics">True (mặc định): ghi sẵn đồ họa THẤP NHẤT mỗi lần mở (chủ repo muốn luôn mặc định thấp nhất): đúng các khóa mà menu Cấu hình của client ghi
    /// (<c>lowGraphic</c> = 1, không vẽ hào quang <c>isPaintAura</c>/<c>isPaintAura2</c> = 0).</param>
    public static void Prepare(string templateDir, string accountDir, string user, string password, int serverIndex, DateTimeOffset now,
        bool writeCredentials = true, bool lowGraphics = true)
    {
        string home = Path.Combine(accountDir, "home");
        if (!Directory.Exists(home))
        {
            string templateHome = Path.Combine(templateDir, "home");
            if (!Directory.Exists(templateHome))
                throw new InvalidOperationException($"Chưa có thư mục dữ liệu mẫu của client: {templateHome}. Chạy client một lần, bấm \"Tải dữ liệu\" rồi đặt thư mục đó làm mẫu trong Cài đặt.");
            string temp = home + ".tmp";
            if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);
            CopyDirectory(templateHome, temp);
            Directory.Move(temp, home);
        }

        string suite = SuiteDir(accountDir);
        if (writeCredentials)
        {
            RmsFile.WriteString(suite, "acc", user, now);
            RmsFile.WriteString(suite, "pass", password, now);
        }
        else
        {
            ClearCredentials(accountDir);
        }
        RmsFile.Write(suite, "svselect", new[] { (byte)serverIndex }, now);
        if (lowGraphics)
        {
            RmsFile.Write(suite, "lowGraphic", new byte[] { 1 }, now);
            RmsFile.Write(suite, "isPaintAura", new byte[] { 0 }, now);
            RmsFile.Write(suite, "isPaintAura2", new byte[] { 0 }, now);
        }
    }

    /// <summary>Xóa tài khoản và mật khẩu khỏi kho RMS (không đụng dữ liệu game). Thư mục không có thì bỏ qua.</summary>
    public static void ClearCredentials(string accountDir)
    {
        string suite = SuiteDir(accountDir);
        if (!Directory.Exists(suite)) return;
        RmsFile.Delete(suite, "acc");
        RmsFile.Delete(suite, "pass");
    }

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from)) File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        foreach (var dir in Directory.GetDirectories(from)) CopyDirectory(dir, Path.Combine(to, Path.GetFileName(dir)));
    }
}
