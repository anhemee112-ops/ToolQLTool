using System.Windows;
using Microsoft.Win32;

namespace ToolQL.App;

public partial class SettingsDialog : Window
{
    public SettingsDialog(string jarPath)
    {
        InitializeComponent();
        JarBox.Text = jarPath;
    }

    public string JarPath => JarBox.Text.Trim();

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Chọn jar của client", Filter = "Jar (*.jar)|*.jar|Mọi tệp|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) JarBox.Text = dialog.FileName;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (JarPath.Length > 0 && !System.IO.File.Exists(JarPath))
        {
            ErrorText.Text = "Không thấy tệp: " + JarPath;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        DialogResult = true;
    }
}
