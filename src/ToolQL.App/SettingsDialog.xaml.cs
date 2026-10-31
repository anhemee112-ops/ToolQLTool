using System.IO;
using System.Windows;
using Microsoft.Win32;
using ToolQL.Core;

namespace ToolQL.App;

public partial class SettingsDialog : Window
{
    public SettingsDialog(AppSettings settings)
    {
        InitializeComponent();
        JavaBox.Text = settings.JavaPath.Length > 0 ? settings.JavaPath : JavaFinder.Find() ?? "";
        EmulatorBox.Text = settings.EmulatorJarPath;
        GameBox.Text = settings.GameJarPath;
        TemplateBox.Text = settings.TemplateDir;
        WidthBox.Text = settings.ClientWidth.ToString();
        HeightBox.Text = settings.ClientHeight.ToString();
        DelayBox.Text = settings.OpenDelayMs.ToString();
        AutoPlayBox.IsChecked = settings.AutoPressPlay;
        ClickDelayBox.Text = settings.LoginClickDelayMs.ToString();
        LowGraphicsBox.IsChecked = settings.LowGraphics;
        MaintenanceBox.IsChecked = settings.MaintenanceEnabled;
        MaintStartBox.Text = settings.MaintenanceStart;
        MaintEndBox.Text = settings.MaintenanceEnd;
    }

    /// <summary>Ghi các giá trị đã nhập vào <paramref name="settings"/> (chỉ gọi sau khi hộp thoại trả true).</summary>
    public void ApplyTo(AppSettings settings)
    {
        settings.JavaPath = JavaBox.Text.Trim();
        settings.EmulatorJarPath = EmulatorBox.Text.Trim();
        settings.GameJarPath = GameBox.Text.Trim();
        settings.TemplateDir = TemplateBox.Text.Trim();
        settings.ClientWidth = int.Parse(WidthBox.Text);
        settings.ClientHeight = int.Parse(HeightBox.Text);
        settings.OpenDelayMs = int.Parse(DelayBox.Text);
        settings.AutoPressPlay = AutoPlayBox.IsChecked == true;
        settings.LoginClickDelayMs = int.Parse(ClickDelayBox.Text);
        settings.LowGraphics = LowGraphicsBox.IsChecked == true;
        settings.MaintenanceEnabled = MaintenanceBox.IsChecked == true;
        settings.MaintenanceStart = MaintStartBox.Text.Trim();
        settings.MaintenanceEnd = MaintEndBox.Text.Trim();
    }

    private void OnBrowseJava(object sender, RoutedEventArgs e) => Browse(JavaBox, "Chọn javaw.exe", "Java (javaw.exe;java.exe)|javaw.exe;java.exe|Mọi tệp|*.*");
    private void OnBrowseEmulator(object sender, RoutedEventArgs e) => Browse(EmulatorBox, "Chọn trình chạy J2ME (AngelAChip.jar)", "Jar (*.jar)|*.jar|Mọi tệp|*.*");
    private void OnBrowseGame(object sender, RoutedEventArgs e) => Browse(GameBox, "Chọn jar game", "Jar (*.jar)|*.jar|Mọi tệp|*.*");

    private void OnBrowseTemplate(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Chọn thư mục dữ liệu mẫu (chứa thư mục home)" };
        if (dialog.ShowDialog(this) == true) TemplateBox.Text = dialog.FolderName;
    }

    private void OnFindJava(object sender, RoutedEventArgs e)
    {
        string? found = JavaFinder.Find();
        if (found is null) ShowError("Không tự tìm thấy Java. Bấm \"Chọn...\" để chỉ javaw.exe.");
        else JavaBox.Text = found;
    }

    private void Browse(System.Windows.Controls.TextBox box, string title, string filter)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) box.Text = dialog.FileName;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(WidthBox.Text, out int w) || !int.TryParse(HeightBox.Text, out int h) || w < 120 || h < 120 || w > 1920 || h > 1200)
        {
            ShowError("Kích thước màn hình game phải là số từ 120 đến 1920 (rộng) và 1200 (cao).");
            return;
        }
        if (!int.TryParse(DelayBox.Text, out int delay) || delay < 0 || delay > 600_000)
        {
            ShowError("Delay mở acc phải là số mili giây từ 0 đến 600000.");
            return;
        }
        if (!int.TryParse(ClickDelayBox.Text, out int clickDelay) || clickDelay < 1000 || clickDelay > 120_000)
        {
            ShowError("Thời gian chờ trước khi bấm phải là số mili giây từ 1000 đến 120000.");
            return;
        }
        if (MaintenanceSchedule.Create(true, MaintStartBox.Text, MaintEndBox.Text) is null)
        {
            ShowError("Giờ bảo trì phải có dạng HH:mm (ví dụ 03:30 và 05:00).");
            return;
        }
        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}

/// <summary>Tự tìm javaw.exe: JAVA_HOME, rồi PATH, rồi các thư mục cài Java quen thuộc.</summary>
public static class JavaFinder
{
    public static string? Find()
    {
        string? home = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(home) && File.Exists(Path.Combine(home, "bin", "javaw.exe"))) return Path.Combine(home, "bin", "javaw.exe");

        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(dir.Trim('"'), "javaw.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException)
            {
                // phần tử PATH sai định dạng: bỏ qua
            }
        }

        foreach (string root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
        {
            foreach (string vendor in new[] { "Eclipse Adoptium", "Java", "Microsoft", "Zulu", "Amazon Corretto" })
            {
                string dir = Path.Combine(root, vendor);
                if (!Directory.Exists(dir)) continue;
                var hit = Directory.GetDirectories(dir).OrderByDescending(d => d).Select(d => Path.Combine(d, "bin", "javaw.exe")).FirstOrDefault(File.Exists);
                if (hit != null) return hit;
            }
        }
        return null;
    }
}
