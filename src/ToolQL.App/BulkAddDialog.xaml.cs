using System.IO;
using System.Windows;
using Microsoft.Win32;
using ToolQL.Core;

namespace ToolQL.App;

/// <summary>Thêm nhiều acc cùng lúc từ danh sách dán vào (định dạng ở <see cref="BulkAccountParser"/>).</summary>
public partial class BulkAddDialog : Window
{
    private readonly Func<ParsedAccount, bool> _exists;

    /// <param name="exists">Cho biết một acc đã có trong danh sách chưa (để báo trùng, vì thêm là tất cả hoặc không).</param>
    public BulkAddDialog(Func<ParsedAccount, bool> exists)
    {
        InitializeComponent();
        _exists = exists;
        Loaded += (_, _) => InputBox.Focus();
    }

    public IReadOnlyList<ParsedAccount> Accounts { get; private set; } = Array.Empty<ParsedAccount>();

    private void OnOpenFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Mở danh sách acc", Filter = "Văn bản (*.txt)|*.txt|Mọi tệp|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            InputBox.Text = File.ReadAllText(dialog.FileName);
        }
        catch (IOException ex)
        {
            ShowErrors(new[] { "Không đọc được tệp: " + ex.Message });
        }
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        var result = BulkAccountParser.Parse(InputBox.Text);
        var errors = result.Errors.ToList();
        if (errors.Count == 0 && result.Accounts.Count == 0) errors.Add("Chưa có dòng nào.");
        if (errors.Count == 0)
        {
            foreach (var a in result.Accounts)
                if (_exists(a)) errors.Add($"Acc {a.User} trên {a.Server} đã có trong danh sách.");
        }
        if (errors.Count > 0)
        {
            ShowErrors(errors);
            return;
        }
        Accounts = result.Accounts;
        DialogResult = true;
    }

    private void ShowErrors(IReadOnlyList<string> errors)
    {
        ErrorText.Text = string.Join("\n", errors.Take(6)) + (errors.Count > 6 ? $"\n... và {errors.Count - 6} lỗi nữa." : "");
        ErrorText.Visibility = Visibility.Visible;
    }
}
