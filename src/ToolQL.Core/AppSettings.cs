using System.Text;
using System.Text.Json;

namespace ToolQL.Core;

/// <summary>
/// Cài đặt của ứng dụng, lưu JSON ngoài repo (mặc định <c>%APPDATA%\ToolQL\settings.json</c>).
/// Không chứa mật khẩu. <see cref="ClientJarPath"/> trỏ tới jar của client do người dùng cung cấp (lấy ô lát để vẽ map);
/// rỗng nghĩa là chưa chọn: khung xem vẫn vẽ được nhưng không có ô lát.
/// </summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public string ClientJarPath { get; set; } = "";

    // ----- Mở cửa sổ client (xem docs/superpowers/specs/2026-10-31-quan-ly-cua-so-client.md) -----

    /// <summary>javaw.exe (hoặc java.exe) dùng để chạy trình J2ME. Rỗng thì ToolQL thử tự tìm.</summary>
    public string JavaPath { get; set; } = "";

    /// <summary>Trình chạy J2ME (AngelAChip.jar).</summary>
    public string EmulatorJarPath { get; set; } = "";

    /// <summary>Jar game (Dragonboy250 v3.5.jar), do chủ repo cung cấp; không kèm trong gói.</summary>
    public string GameJarPath { get; set; } = "";

    /// <summary>Thư mục chứa <c>home\</c> mẫu (dữ liệu game đã tải) để sao chép cho từng acc. Rỗng thì dùng <c>%LOCALAPPDATA%\ToolQL\client-template</c>.</summary>
    public string TemplateDir { get; set; } = "";

    /// <summary>Kích thước màn hình game (điểm ảnh) truyền cho trình J2ME.</summary>
    public int ClientWidth { get; set; } = 240;

    public int ClientHeight { get; set; } = 320;

    /// <summary>Khoảng nghỉ giữa hai lần mở acc liên tiếp (ms). Máy yếu thì để lớn hơn.</summary>
    public int OpenDelayMs { get; set; } = 3000;

    /// <summary>
    /// Tự bấm nút "Chơi TK" của client sau khi mở acc (client chỉ hiện nút này chứ không tự đăng nhập). Bấm bằng chuột thật nên ToolQL
    /// đưa cửa sổ ra trước trong tích tắc; tắt thì phải tự bấm trong từng cửa sổ.
    /// </summary>
    public bool AutoPressPlay { get; set; } = true;

    /// <summary>Chờ bao lâu sau khi mở client thì bấm "Chơi TK" (ms), để client nạp xong menu.</summary>
    public int LoginClickDelayMs { get; set; } = 12000;

    /// <summary>
    /// Bảo trì hằng ngày của game: trong khung giờ này (giờ Việt Nam) ToolQL đóng mọi client và không tự mở lại; hết khung thì mở lại
    /// đúng các acc đã bị đóng. ToolQL KHÔNG đọc được thông báo bảo trì trong game nên chỉ theo giờ này; chỉnh cho khớp giờ thật của game.
    /// </summary>
    public bool MaintenanceEnabled { get; set; } = true;

    /// <summary>Giờ bắt đầu đóng client (HH:mm, giờ Việt Nam). Mặc định 03:30 theo giờ bảo trì thật chủ repo xác nhận (2026-10-31).</summary>
    public string MaintenanceStart { get; set; } = "03:30";

    /// <summary>Giờ mở lại các acc (HH:mm, giờ Việt Nam).</summary>
    public string MaintenanceEnd { get; set; } = "05:00";

    /// <summary>Luôn mở client ở đồ họa thấp nhất (ghi sẵn vào RMS của client mỗi lần mở). Mặc định bật theo yêu cầu chủ repo.</summary>
    public bool LowGraphics { get; set; } = true;

    /// <summary>Cách xếp bảng acc: <c>added</c> (theo thứ tự thêm), <c>server</c>, <c>alpha</c> (theo bảng chữ cái). Chỉ đổi thứ tự hiện, không đổi dữ liệu.</summary>
    public string SortMode { get; set; } = "added";

    public string EffectiveTemplateDir => TemplateDir.Length > 0 ? TemplateDir : Path.Combine(LocalDataDir, "client-template");

    /// <summary>Thư mục chứa dữ liệu riêng của từng acc (mỗi acc một thư mục con).</summary>
    public static string ClientsDir => Path.Combine(LocalDataDir, "clients");

    private static string LocalDataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ToolQL");

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ToolQL", "settings.json");

    /// <summary>Đọc từ đĩa; chưa có file thì trả cài đặt mặc định. File hỏng thì ném <see cref="InvalidDataException"/>, không ghi đè.</summary>
    public static AppSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path)) return new AppSettings();
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path, Encoding.UTF8))
                ?? throw new InvalidDataException("File cài đặt rỗng: " + path);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"File cài đặt không đọc được ({path}): {ex.Message}", ex);
        }
    }

    /// <summary>Ghi qua file tạm rồi thay thế để mất điện giữa chừng không làm hỏng file cũ.</summary>
    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, Json), new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }
}
