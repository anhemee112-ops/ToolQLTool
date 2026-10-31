using System.Windows;

namespace ToolQL.App;

public partial class App : Application
{
    public App()
    {
        // Lỗi không bắt được: báo bằng hộp thoại thay vì tắt im lặng. Thông báo lỗi không bao giờ chứa mật khẩu.
        DispatcherUnhandledException += (_, e) =>
        {
            MessageBox.Show("Lỗi không mong đợi:\n\n" + e.Exception.Message, "ToolQL", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
    }
}
