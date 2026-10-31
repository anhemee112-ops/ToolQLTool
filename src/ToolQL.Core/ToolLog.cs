using System.Text;

namespace ToolQL.Core;

/// <summary>
/// Nhật ký sự kiện của ToolQL (mở acc, bấm "Chơi TK", đóng popup cập nhật, mất kết nối, bảo trì...): mỗi sự kiện một dòng có giờ, để xem lại
/// sau một đêm treo. KHÔNG ghi mật khẩu; tên tài khoản do người gọi che bớt. Quá <c>maxBytes</c> thì chuyển tệp hiện tại thành <c>.1</c>
/// (chỉ giữ một tệp cũ). Lỗi ghi tệp bị nuốt: nhật ký không bao giờ được làm hỏng chương trình.
/// </summary>
public sealed class ToolLog
{
    private readonly string _path;
    private readonly TimeProvider _time;
    private readonly long _maxBytes;

    public ToolLog(string path, TimeProvider time, long maxBytes = 1_000_000)
    {
        _path = path;
        _time = time;
        _maxBytes = maxBytes;
    }

    public void Write(string message)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(_path) && new FileInfo(_path).Length >= _maxBytes) File.Move(_path, _path + ".1", overwrite: true);
            string line = $"{_time.GetLocalNow():yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}";
            File.AppendAllText(_path, line, new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // không ghi được thì thôi
        }
    }
}
