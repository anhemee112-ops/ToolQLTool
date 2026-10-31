using System.Windows;
using ToolQL.Protocol;

namespace ToolQL.App;

/// <summary>Thêm hoặc sửa một acc. Khi sửa, tên đăng nhập cố định và để trống mật khẩu nghĩa là giữ mật khẩu cũ.</summary>
public partial class AccountDialog : Window
{
    private readonly bool _editing;

    public AccountDialog(string? user = null, string? server = null, string? note = null)
    {
        InitializeComponent();
        _editing = user != null;
        Title = _editing ? "Sửa acc" : "Thêm acc";
        foreach (var s in ServerList.All) ServerBox.Items.Add(s.Name);
        ServerBox.SelectedItem = server ?? ServerList.All[0].Name;
        if (ServerBox.SelectedItem is null) ServerBox.SelectedIndex = 0;
        UserBox.Text = user ?? "";
        UserBox.IsEnabled = !_editing;
        NoteBox.Text = note ?? "";
        if (_editing) PasswordLabel.Text = "Mật khẩu (để trống để giữ mật khẩu cũ)";
        Loaded += (_, _) => (_editing ? (IInputElement)PasswordBox : UserBox).Focus();
    }

    public string User => UserBox.Text.Trim();

    /// <summary>Mật khẩu mới; null khi đang sửa và không nhập gì.</summary>
    public string? Password => PasswordBox.Password.Length == 0 && _editing ? null : PasswordBox.Password;

    public string Server => (string)ServerBox.SelectedItem;
    public string Note => NoteBox.Text.Trim();

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (User.Length == 0)
        {
            ShowError("Nhập tên đăng nhập.");
            return;
        }
        if (!_editing && PasswordBox.Password.Length == 0)
        {
            ShowError("Nhập mật khẩu.");
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
