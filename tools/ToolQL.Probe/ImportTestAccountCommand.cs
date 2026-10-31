using ToolQL.Core;
using ToolQL.Protocol;

namespace ToolQL.Probe;

/// <summary>
/// `probe import-test-account &lt;thư mục dữ liệu&gt;`: nạp acc thử (<c>%USERPROFILE%\.config\toolql\test-account.env</c>) vào kho acc của ToolQL
/// ở thư mục đó, mật khẩu mã hóa DPAPI như khi thêm bằng giao diện. KHÔNG nối mạng, KHÔNG in mật khẩu (tên đăng nhập cũng che bớt).
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public static class ImportTestAccountCommand
{
    public static int Run(string dataDir)
    {
        TestAccount account;
        try
        {
            account = TestAccount.Load();
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException)
        {
            Console.WriteLine(ex.Message);
            return 1;
        }

        string server = (account.Server is { } s ? ServerList.Find(s) : null)?.Name ?? ServerList.All[0].Name;
        var store = new AccountStore(Path.Combine(dataDir, "accounts.json"), new DpapiPasswordProtector());
        store.Load();
        try
        {
            store.Add(account.User, account.Password, server, "acc thử");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine(ex.Message);
            return 1;
        }
        string masked = account.User.Length <= 3 ? "***" : account.User[..3] + new string('*', Math.Min(account.User.Length - 3, 6));
        Console.WriteLine($"Đã nạp acc thử {masked} trên {server} vào {Path.Combine(dataDir, "accounts.json")} (mật khẩu mã hóa DPAPI, không in ra).");
        return 0;
    }
}
