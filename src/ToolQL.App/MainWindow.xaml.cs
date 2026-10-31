using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using ToolQL.Core;
using ToolQL.Protocol;

namespace ToolQL.App;

/// <summary>
/// Cửa sổ quản lý kiểu Tool 2: bảng acc, mỗi acc bấm Login thì mở MỘT cửa sổ client riêng (jar game chạy bằng trình J2ME AngelChip);
/// Xếp cửa sổ, Ẩn client, Hiện, Auto login (mở lại acc có ✓ sau ít nhất 40 giây). Tắt ToolQL thì mọi cửa sổ client tắt theo.
/// Tham số dòng lệnh (để thử): <c>--thu-muc-du-lieu &lt;thư mục&gt;</c> đặt nơi lưu acc, cài đặt và dữ liệu client.
/// </summary>
public partial class MainWindow : Window
{
    private const string Version = "0.1.0";

    private readonly string _dataDir;
    private readonly string _clientsDir;
    private readonly string _settingsPath;
    private readonly AccountStore _store;
    private readonly AppSettings _settings;
    private readonly ChildProcessJob _job = new();
    private readonly ClientHub _hub;

    // Dữ liệu game tải tự động lần đầu (xem TemplateBuilder). Acc bấm Login lúc chưa có dữ liệu thì được mở lại ngay khi tải xong.
    private readonly TemplateBuilder _template;
    private readonly HashSet<Guid> _waitingForData = new();
    private bool _dataChecked;
    private DateTimeOffset _dataRetryAfter;
    private readonly ObservableCollection<AccountRow> _rows = new();
    private readonly ListCollectionView _view;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    /// <summary>Cứ chừng này giây thì trả RAM ít dùng của các client về Windows (xem <see cref="WindowOps.TrimMemory"/>).</summary>
    private const int TrimEverySeconds = 30;
    private int _trimTicks;
    private readonly HashSet<int> _titled = new();
    private readonly Mutex? _singleInstance;
    private List<string>? _collecting;
    private bool _suppressTick;
    private bool _autoLogin;
    private LoginClickScheduler _clicks = new(TimeSpan.FromSeconds(12));

    // Popup "Đã có bản cập nhật mới" hiện khoảng 30-40 giây sau khi mở acc: canh từ giây 10 tới phút 5 của mỗi lần chạy.
    private readonly PopupGuard _popup = new(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));

    // Vị trí nút "Chơi TK" trong vùng client 240×320 của AngelAChip bản gọn (đo ngày 2026-10-31 trên bản gốc: (120, 147) trên vùng client
    // 240×365 có 22 px thanh menu, nên là (120, 125) ở bản gọn), tính theo tỉ lệ để còn đúng khi khung phóng khác.
    // CHỈ mới kiểm với kích thước màn hình 240×320.
    private const double PlayButtonX = 0.5, PlayButtonY = 125.0 / 320;

    private readonly ToolLog _log;
    private readonly Dictionary<Guid, string> _lastLogged = new();
    private readonly HashSet<(Guid, DateTimeOffset)> _promptSeen = new(), _promptGone = new();
    private bool _wasMaintenance;

    private readonly bool _noLogin;   // --khong-dang-nhap: mở client nhưng không ghi tài khoản/mật khẩu (thử mở/xếp cửa sổ mà không đăng nhập)

    public MainWindow()
    {
        InitializeComponent();

        string[] args = Environment.GetCommandLineArgs();
        string? custom = ArgValue(args, "--thu-muc-du-lieu");
        _noLogin = args.Contains("--khong-dang-nhap");
        _dataDir = custom ?? Path.GetDirectoryName(AccountStore.DefaultPath)!;
        _clientsDir = custom is null ? AppSettings.ClientsDir : Path.Combine(custom, "clients");
        _settingsPath = Path.Combine(_dataDir, "settings.json");
        _log = new ToolLog(Path.Combine(_dataDir, "toolql.log"), TimeProvider.System);

        // Một ToolQL cho mỗi thư mục dữ liệu (hai bản cùng ghi một danh sách acc sẽ đè nhau).
        string mutexName = "ToolQL.Single." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_dataDir.ToLowerInvariant())))[..16];
        _singleInstance = new Mutex(true, mutexName, out bool first);
        if (!first)
        {
            MessageBox.Show("ToolQL đang chạy với cùng thư mục dữ liệu. Không mở hai bản cùng lúc.", "ToolQL", MessageBoxButton.OK, MessageBoxImage.Information);
            Application.Current.Shutdown(1);
            _store = null!; _settings = null!; _hub = null!; _view = null!; _template = null!;
            return;
        }

        _store = new AccountStore(Path.Combine(_dataDir, "accounts.json"), new DpapiPasswordProtector());
        try
        {
            _store.Load();
            _settings = AppSettings.Load(_settingsPath);
        }
        catch (InvalidDataException ex)
        {
            // Không tiếp tục: lưu tiếp sẽ ghi đè file hỏng và mất acc.
            MessageBox.Show(ex.Message + "\n\nToolQL sẽ đóng để không ghi đè file này. Hãy sửa hoặc đổi tên file rồi mở lại.",
                "ToolQL", MessageBoxButton.OK, MessageBoxImage.Error);
            Application.Current.Shutdown(1);
            _settings = new AppSettings(); _hub = null!; _view = null!; _template = null!;
            return;
        }
        if (_settings.JavaPath.Length == 0 && JavaFinder.Find() is { } java) _settings.JavaPath = java;

        _hub = new ClientHub(new SystemClientLauncher(_job), Prepare, id => ClientDirectory.ClearCredentials(AccountDir(id)),
            TimeSpan.FromMilliseconds(Math.Max(0, _settings.OpenDelayMs)),
            probe: new TcpTableProbe(ServerList.All.Select(s => s.Port).Distinct()));
        _hub.Failed += OnClientFailed;
        _hub.Changed += OnClientChanged;
        _template = new TemplateBuilder(_settings, new SystemClientLauncher(_job), _log.Write);
        _template.Finished += OnTemplateFinished;
        _log.Write($"ToolQL {Version} khởi động" + (_noLogin ? " (chế độ không đăng nhập)" : "") + $"; dữ liệu: {_dataDir}");

        foreach (var e in _store.Entries) AddRow(e);
        _view = (ListCollectionView)CollectionViewSource.GetDefaultView(_rows);
        AccountList.ItemsSource = _view;
        ApplySort(_settings.SortMode);
        if (_rows.Count > 0) AccountList.SelectedIndex = 0;

        _timer.Tick += (_, _) => OnTimer();
        _timer.Start();
        Closing += OnWindowClosing;
        UpdateUi();
    }

    private static string? ArgValue(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private string AccountDir(Guid id) => Path.Combine(_clientsDir, id.ToString("N"));

    private IEnumerable<AccountRow> Selected => AccountList.SelectedItems.Cast<AccountRow>().OrderBy(r => r.Index).ToList();

    private AccountRow AddRow(AccountEntry e)
    {
        var row = new AccountRow(e.Id, e.User, e.Server, e.Note, e.Tick);
        row.TickChanged += OnRowTickChanged;
        _rows.Add(row);
        return row;
    }

    // ---------- chuẩn bị và mở client ----------

    /// <summary>Chuẩn bị thư mục dữ liệu của acc (sao chép mẫu, ghi tài khoản/mật khẩu) rồi dựng lệnh mở. Ném lỗi tiếng Việt nếu thiếu cấu hình.</summary>
    private ClientLaunchSpec Prepare(Guid id)
    {
        var entry = _store.Entries.FirstOrDefault(e => e.Id == id) ?? throw new KeyNotFoundException("Không có acc này nữa.");
        if (ClientCommand.Check(_settings) is { } problem) throw new InvalidOperationException(problem);
        int serverIndex = ServerList.All.ToList().FindIndex(s => s.Name == entry.Server);
        if (serverIndex < 0) throw new InvalidOperationException($"Server \"{entry.Server}\" không có trong danh sách.");

        if (!TemplateBuilder.Exists(_settings))
        {
            // Chưa có dữ liệu game: tự tải (client không đăng nhập), xong sẽ tự mở lại acc này.
            _waitingForData.Add(id);
            StartDataDownload();
            throw new InvalidOperationException("đang tự tải dữ liệu game lần đầu, xong sẽ tự mở acc");
        }

        var credentials = _store.GetCredentials(id);   // InvalidDataException nếu không giải được mật khẩu
        string dir = AccountDir(id);
        ClientDirectory.Prepare(_settings.EffectiveTemplateDir, dir, credentials.User, credentials.Password, serverIndex, DateTimeOffset.UtcNow,
            writeCredentials: !_noLogin, lowGraphics: _settings.LowGraphics);
        return ClientCommand.Build(_settings, dir, "a" + id.ToString("N")[..8]);
    }

    /// <summary>Bắt đầu tải dữ liệu game nếu chưa chạy và chưa vừa thất bại (nghỉ 5 phút sau mỗi lần thất bại để không dội server).</summary>
    private void StartDataDownload()
    {
        if (_template.Running || DateTimeOffset.UtcNow < _dataRetryAfter) return;
        _template.Start();
    }

    private void OnTemplateFinished(bool ok)
    {
        var waiting = _waitingForData.ToList();
        _waitingForData.Clear();
        if (!ok)
        {
            _dataRetryAfter = DateTimeOffset.UtcNow.AddMinutes(5);
            StatusText.Text = "Tải dữ liệu game thất bại (xem toolql.log); sẽ thử lại khi bấm Login sau 5 phút.";
            return;
        }
        foreach (var id in waiting) _hub.Request(id);
    }

    private void OnClientFailed(Guid id, string message)
    {
        string user = _rows.FirstOrDefault(r => r.Id == id)?.User ?? "?";
        _collecting?.Add($"{user}: {message}");
    }

    /// <summary>Tên tài khoản che bớt cho nhật ký (hai ký tự đầu), để gửi nhật ký đi không lộ tài khoản.</summary>
    private string Who(Guid id)
    {
        string user = _rows.FirstOrDefault(r => r.Id == id)?.User ?? "?";
        return user.Length <= 2 ? "***" : user[..2] + new string('*', Math.Min(user.Length - 2, 6));
    }

    private void OnClientChanged(Guid id)
    {
        string what = _hub.StateOf(id) switch
        {
            ClientState.Queued => "xếp hàng chờ mở",
            ClientState.Running => $"đã mở client (pid {_hub.ProcessIdOf(id)})",
            ClientState.Exited => _hub.EndOf(id) switch
            {
                ClientEnd.User => "đã đóng (người dùng)",
                ClientEnd.Lost => $"MẤT KẾT NỐI hoặc client thoát (mã {_hub.ExitCodeOf(id)}); chờ ít nhất {_hub.WaitLeftOf(id).TotalSeconds:0}s mới mở lại" +
                                  (_hub.AutoRetryPausedOf(id) ? "; ĐÃ DỪNG tự mở lại (mất kết nối liên tiếp)" : ""),
                ClientEnd.Maintenance => "đóng vì tới khung giờ bảo trì",
                ClientEnd.Failed => "mở lỗi: " + _hub.ErrorOf(id),
                _ => "đã thoát",
            },
            _ => "chưa mở",
        };
        if (_lastLogged.TryGetValue(id, out var previous) && previous == what) return;
        _lastLogged[id] = what;
        _log.Write($"{Who(id)}: {what}");
    }

    /// <summary>Chạy một thao tác mở/đóng của người dùng và gom lỗi mở không được thành MỘT hộp thoại. Auto login không hiện hộp thoại (xem cột Status).</summary>
    private void RunManual(Action action)
    {
        _collecting = new List<string>();
        try
        {
            action();
        }
        finally
        {
            var errors = _collecting;
            _collecting = null;
            if (errors.Count > 0)
                MessageBox.Show(string.Join("\n", errors.Take(8)), "Không mở được acc", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RefreshRows();
    }

    // ---------- nút bấm: danh sách acc ----------

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        var dialog = new AccountDialog { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var entry = _store.Add(dialog.User, dialog.Password!, dialog.Server, dialog.Note);
            var row = AddRow(entry);
            Renumber();
            AccountList.SelectedItem = row;
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "ToolQL", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        UpdateUi();
    }

    private void OnBulkAdd(object sender, RoutedEventArgs e)
    {
        var dialog = new BulkAddDialog(a => _store.Entries.Any(x => x.User == a.User && x.Server == a.Server)) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        foreach (var a in dialog.Accounts) AddRow(_store.Add(a.User, a.Password, a.Server, a.Note));
        Renumber();
        MessageBox.Show($"Đã thêm {dialog.Accounts.Count} acc.", "ToolQL", MessageBoxButton.OK, MessageBoxImage.Information);
        UpdateUi();
    }

    private void OnEdit(object sender, RoutedEventArgs e)
    {
        if (Selected.FirstOrDefault() is not { } row) return;
        var dialog = new AccountDialog(row.User, row.Server, row.Note) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var entry = _store.Update(row.Id, dialog.Server, dialog.Note, dialog.Password);
            row.SetDetails(entry.Server, entry.Note);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "ToolQL", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnListDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Bấm đúp vào ô ✓ hoặc nút Hiện không phải là "sửa acc".
        for (DependencyObject? d = e.OriginalSource as DependencyObject; d != null && d is not ListViewItem; d = VisualTreeHelper.GetParent(d))
            if (d is CheckBox or Button) return;
        if (e.OriginalSource is FrameworkElement { DataContext: AccountRow }) OnEdit(sender, e);
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        var rows = Selected.ToList();
        if (rows.Count == 0) return;
        string what = rows.Count == 1 ? $"acc {rows[0].User} ({rows[0].Server})" : $"{rows.Count} acc đang chọn";
        if (MessageBox.Show($"Xóa {what}?\nCửa sổ client của chúng (nếu đang mở) sẽ bị đóng.", "ToolQL", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        foreach (var row in rows)
        {
            _hub.Stop(row.Id);
            _store.Remove(row.Id);
            _rows.Remove(row);
            try
            {
                if (Directory.Exists(AccountDir(row.Id))) Directory.Delete(AccountDir(row.Id), recursive: true);
            }
            catch (IOException) { /* client chưa kịp thoát: để lại, không quan trọng */ }
            catch (UnauthorizedAccessException) { }
        }
        Renumber();
        UpdateUi();
    }

    private void OnTick(object sender, RoutedEventArgs e) => SetTicks(Selected, true);
    private void OnUntick(object sender, RoutedEventArgs e) => SetTicks(Selected, false);

    private void SetTicks(IEnumerable<AccountRow> rows, bool tick)
    {
        var list = rows.ToList();
        _suppressTick = true;
        foreach (var row in list) row.Tick = tick;
        _suppressTick = false;
        _store.SetTick(list.Select(r => r.Id), tick);
    }

    private void OnRowTickChanged(AccountRow row)
    {
        if (!_suppressTick) _store.SetTick(new[] { row.Id }, row.Tick);
    }

    // ---------- nút bấm: mở / đóng ----------

    private void OnLogin(object sender, RoutedEventArgs e) => Login(Selected);
    private void OnLoginAll(object sender, RoutedEventArgs e) => Login(_rows.OrderBy(r => r.Index));

    private void Login(IEnumerable<AccountRow> rows)
    {
        var tooSoon = new List<string>();
        RunManual(() =>
        {
            foreach (var row in rows.ToList())
            {
                var outcome = _hub.Request(row.Id);
                if (outcome.Result == StartResult.TooSoon) tooSoon.Add($"{row.User}: chờ thêm {Math.Ceiling(outcome.Wait.TotalSeconds):0} giây");
            }
        });
        if (tooSoon.Count > 0)
            MessageBox.Show("Các acc này vừa ngắt kết nối nên chưa mở lại được (đăng nhập lại sớm hơn 40 giây thì server khóa acc):\n" + string.Join("\n", tooSoon.Take(10)),
                "ToolQL", MessageBoxButton.OK, MessageBoxImage.Information);
        UpdateUi();
    }

    /// <summary>Đóng: tắt client VÀ bỏ ✓ (để Auto login không mở lại).</summary>
    private void OnClose(object sender, RoutedEventArgs e)
    {
        var rows = Selected.ToList();
        foreach (var row in rows) _hub.Stop(row.Id);
        SetTicks(rows, false);
        RefreshRows();
        UpdateUi();
    }

    private void OnStopAll(object sender, RoutedEventArgs e)
    {
        _hub.StopAll();
        _autoLogin = false;
        AutoLoginToggle.IsChecked = false;
        AutoLoginToggle.Content = "Auto login: TẮT";
        RefreshRows();
        UpdateUi();
    }

    private void OnAutoLoginToggle(object sender, RoutedEventArgs e)
    {
        _autoLogin = AutoLoginToggle.IsChecked == true;
        AutoLoginToggle.Content = _autoLogin ? "Auto login: BẬT" : "Auto login: TẮT";
        UpdateUi();
    }

    // ---------- nút bấm: cửa sổ client ----------

    private List<(AccountRow Row, IntPtr Hwnd)> RunningWindows() =>
        _hub.Running()
            .Select(r => (Row: _rows.FirstOrDefault(x => x.Id == r.Id), Hwnd: WindowOps.FindMainWindow(r.Pid)))
            .Where(x => x.Row != null && x.Hwnd != IntPtr.Zero)
            .Select(x => (x.Row!, x.Hwnd))
            .OrderBy(x => x.Item1.Index)
            .ToList();

    private void OnArrange(object sender, RoutedEventArgs e)
    {
        var windows = RunningWindows();
        if (windows.Count == 0) return;
        var size = WindowOps.SizeOf(windows[0].Hwnd) ?? (256, 404);
        var dpi = VisualTreeHelper.GetDpi(this);
        var area = SystemParameters.WorkArea;
        int left = (int)(area.Left * dpi.DpiScaleX), top = (int)(area.Top * dpi.DpiScaleY);
        var cells = WindowGrid.Layout(windows.Count, (int)(area.Width * dpi.DpiScaleX), (int)(area.Height * dpi.DpiScaleY), size.Width, size.Height, gap: 4);
        for (int i = 0; i < windows.Count; i++)
            WindowOps.Place(windows[i].Hwnd, new WindowRect(left + cells[i].X, top + cells[i].Y, cells[i].Width, cells[i].Height));
    }

    private void OnHideClients(object sender, RoutedEventArgs e)
    {
        foreach (var (_, hwnd) in RunningWindows()) WindowOps.Minimize(hwnd);
    }

    private void OnShowAll(object sender, RoutedEventArgs e)
    {
        foreach (var (_, hwnd) in RunningWindows()) WindowOps.Bring(hwnd);
    }

    private void OnShowOne(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not AccountRow row) return;
        var pid = _hub.ProcessIdOf(row.Id);
        var hwnd = pid is { } p ? WindowOps.FindMainWindow(p) : IntPtr.Zero;
        if (hwnd == IntPtr.Zero) StatusText.Text = $"{row.User}: chưa có cửa sổ client để hiện (acc chưa mở hoặc cửa sổ chưa hiện).";
        else WindowOps.Bring(hwnd);
    }

    private void OnOpenDataDir(object sender, RoutedEventArgs e)
    {
        if (Selected.FirstOrDefault() is not { } row) return;
        string dir = AccountDir(row.Id);
        if (!Directory.Exists(dir)) { StatusText.Text = $"{row.User}: chưa có thư mục dữ liệu (acc chưa mở lần nào)."; return; }
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = false });
    }

    // ---------- menu ----------

    private void OnExit(object sender, RoutedEventArgs e) => Close();

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog(_settings) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        dialog.ApplyTo(_settings);
        SaveSettings();
        UpdateUi();
    }

    private void SaveSettings()
    {
        try
        {
            _settings.Save(_settingsPath);
        }
        catch (IOException ex)
        {
            MessageBox.Show("Không lưu được cài đặt: " + ex.Message, "ToolQL", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Tải lại dữ liệu game (lần đầu ToolQL tự tải; mục này để tải lại khi game cập nhật). Chỉ ảnh hưởng acc mở lần đầu sau đó.</summary>
    private void OnMakeTemplate(object sender, RoutedEventArgs e)
    {
        if (ClientCommand.Check(_settings) is { } problem)
        {
            MessageBox.Show(problem, "ToolQL", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (_template.Running)
        {
            MessageBox.Show("ToolQL đang tải dữ liệu game, chờ một chút.", "ToolQL", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show(
                "ToolQL sẽ tự mở một cửa sổ client KHÔNG đăng nhập, bấm \"Tải dữ liệu\" rồi đóng (khoảng nửa phút, đừng đụng chuột).\n\n" +
                $"Dữ liệu lưu vào:\n{_settings.EffectiveTemplateDir}\n\nClient sẽ kết nối tới server game để tải dữ liệu. Tiếp tục?",
                "Tải lại dữ liệu game", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _dataRetryAfter = default;
        _template.Start();
    }

    private void OnSortChanged(object sender, RoutedEventArgs e)
    {
        string mode = (string)((MenuItem)sender).Tag;
        ApplySort(mode);
        _settings.SortMode = mode;
        SaveSettings();
    }

    private void ApplySort(string mode)
    {
        if (mode is not ("added" or "server" or "alpha")) mode = "added";
        SortAdded.IsChecked = mode == "added";
        SortServer.IsChecked = mode == "server";
        SortAlpha.IsChecked = mode == "alpha";
        _view.CustomSort = mode switch
        {
            "server" => new RowComparer(byServer: true),
            "alpha" => new RowComparer(byServer: false),
            _ => null,
        };
        Renumber();
    }

    private sealed class RowComparer : System.Collections.IComparer
    {
        private readonly bool _byServer;
        public RowComparer(bool byServer) => _byServer = byServer;

        public int Compare(object? x, object? y)
        {
            var a = (AccountRow)x!;
            var b = (AccountRow)y!;
            if (_byServer && a.ServerNumber != b.ServerNumber) return a.ServerNumber.CompareTo(b.ServerNumber);
            return NaturalComparer.Instance.Compare(a.User, b.User);
        }
    }

    private void OnHideNames(object sender, RoutedEventArgs e)
    {
        AccountRow.HideNames = HideNamesItem.IsChecked;
        foreach (var row in _rows) row.RaiseUserDisplay();
        UpdateCard();
    }

    private void OnAbout(object sender, RoutedEventArgs e) =>
        MessageBox.Show($"ToolQL {Version}\nQuản lý nhiều tài khoản Ngọc Rồng Online: mỗi acc một cửa sổ client.\n\nDữ liệu: {_dataDir}",
            "Phiên bản", MessageBoxButton.OK, MessageBoxImage.Information);

    // ---------- giờ và giao diện ----------

    private void OnTimer()
    {
        var ticked = _store.Entries.Where(en => en.Tick).Select(en => en.Id).ToList();
        _hub.Tick(ticked, _autoLogin, InMaintenance());
        if (_hub.MaintenanceActive != _wasMaintenance)
        {
            _wasMaintenance = _hub.MaintenanceActive;
            _log.Write(_wasMaintenance ? $"VÀO khung giờ bảo trì {_settings.MaintenanceStart}-{_settings.MaintenanceEnd}: đóng mọi client" : "HẾT khung giờ bảo trì: mở lại các acc đã đóng");
        }
        RefreshRows();

        // Đặt tiêu đề cửa sổ client thành "tài khoản · server" để dễ nhận ra (một lần cho mỗi tiến trình).
        var alive = new HashSet<int>();
        foreach (var (id, pid) in _hub.Running())
        {
            alive.Add(pid);
            if (_titled.Contains(pid)) continue;
            var hwnd = WindowOps.FindMainWindow(pid);
            if (hwnd == IntPtr.Zero || _rows.FirstOrDefault(r => r.Id == id) is not { } row) continue;
            WindowOps.SetTitle(hwnd, $"{row.UserDisplay} · SV{row.ServerNumber}");
            _titled.Add(pid);
        }
        _titled.IntersectWith(alive);
        if (!_dataChecked)
        {
            _dataChecked = true;
            if (!_noLogin && !TemplateBuilder.Exists(_settings)) StartDataDownload();   // lần đầu: tự tải dữ liệu game ngay khi mở ToolQL
        }
        _template.Tick();
        if (++_trimTicks >= TrimEverySeconds)
        {
            _trimTicks = 0;
            foreach (int pid in alive) WindowOps.TrimMemory(pid);
        }
        if (_settings.AutoPressPlay && !_noLogin) PressPlayWhenDue();
        if (!_noLogin) DismissUpdatePromptWhenDue();
        UpdateStatus();
        UpdateCard();
    }

    /// <summary>
    /// Chụp cửa sổ các client mới mở (tối đa 3 mỗi giây) để tìm popup "Đã có bản cập nhật mới" và bấm "Không" khi thấy (xem <see cref="PopupGuard"/>).
    /// Chụp bằng <c>PrintWindow</c> nên không đưa cửa sổ lên trên; chỉ lúc bấm mới đưa lên trong tích tắc, và mỗi giây tối đa một cú bấm.
    /// </summary>
    private void DismissUpdatePromptWhenDue()
    {
        var now = DateTimeOffset.UtcNow;
        var running = _hub.Running().Select(r => (r.Id, StartedAt: _hub.StartedAtOf(r.Id) ?? now)).ToList();
        foreach (var id in _popup.Watching(running, now).Take(3))
        {
            var hwnd = _hub.ProcessIdOf(id) is { } pid ? WindowOps.FindMainWindow(pid) : IntPtr.Zero;
            if (hwnd == IntPtr.Zero || WindowOps.CaptureClient(hwnd) is not { } frame) continue;
            var startedAt = running.First(r => r.Id == id).StartedAt;
            bool present = UpdatePromptDetector.IsPresent(frame);
            var key = (id, startedAt);
            if (present && _promptSeen.Add(key)) _log.Write($"{Who(id)}: thấy popup \"Đã có bản cập nhật mới\"");
            if (!present && _promptSeen.Contains(key) && _promptGone.Add(key)) _log.Write($"{Who(id)}: popup cập nhật đã đóng");
            if (!_popup.Observe(id, startedAt, now, present)) continue;
            WindowOps.ClickClient(hwnd, UpdatePromptDetector.NoButtonX, UpdatePromptDetector.NoButtonY);
            _log.Write($"{Who(id)}: đã bấm \"Không\" ở popup cập nhật");
            return;   // một cú bấm mỗi giây
        }
    }

    /// <summary>Bấm "Chơi TK" cho MỘT acc đã đến hạn (xem <see cref="LoginClickScheduler"/>). Chưa có cửa sổ thì thử lại lần sau.</summary>
    private void PressPlayWhenDue()
    {
        _clicks.Delay = TimeSpan.FromMilliseconds(Math.Max(1000, _settings.LoginClickDelayMs));
        var running = _hub.Running()
            .Select(r => (r.Id, StartedAt: _hub.StartedAtOf(r.Id) ?? DateTimeOffset.UtcNow))
            .ToList();
        if (_clicks.NextDue(running, DateTimeOffset.UtcNow) is not { } id) return;
        var hwnd = _hub.ProcessIdOf(id) is { } pid ? WindowOps.FindMainWindow(pid) : IntPtr.Zero;
        if (hwnd == IntPtr.Zero) return;
        if (WindowOps.ClickClient(hwnd, PlayButtonX, PlayButtonY))
        {
            _clicks.MarkDone(id, running.First(r => r.Id == id).StartedAt);
            _log.Write($"{Who(id)}: đã bấm \"Chơi TK\" (đăng nhập vào game)");
        }
    }

    /// <summary>Đang trong khung giờ bảo trì cài đặt (giờ Việt Nam)? Giờ sai định dạng thì coi là không bảo trì (hộp Cài đặt đã chặn nhập sai).</summary>
    private bool InMaintenance() =>
        MaintenanceSchedule.Create(_settings.MaintenanceEnabled, _settings.MaintenanceStart, _settings.MaintenanceEnd)?.IsMaintenance(DateTimeOffset.UtcNow) ?? false;

    private void RefreshRows()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var row in _rows) row.Refresh(_hub, now);
    }

    private void Renumber()
    {
        int i = 1;
        foreach (AccountRow row in _view) row.Index = i++;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateUi();

    private void UpdateUi()
    {
        EmptyHint.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        bool any = AccountList.SelectedItems.Count > 0;
        RemoveButton.IsEnabled = LoginButton.IsEnabled = CloseButton.IsEnabled = TickButton.IsEnabled = UntickButton.IsEnabled = any;
        LoginAllButton.IsEnabled = _rows.Count > 0;
        UpdateCard();
        UpdateStatus();
    }

    private void UpdateCard()
    {
        var selected = Selected.ToList();
        if (selected.Count == 0)
        {
            CardTitle.Text = "Chọn một acc";
            CardBody.Text = "";
            CardError.Text = "";
            return;
        }
        if (selected.Count > 1)
        {
            CardTitle.Text = $"{selected.Count} acc đang chọn";
            CardBody.Text = $"Đang chạy: {selected.Count(r => r.State == ClientState.Running)}\nCó ✓: {selected.Count(r => r.Tick)}";
            CardError.Text = "";
            return;
        }
        var row = selected[0];
        CardTitle.Text = row.UserDisplay;
        var sb = new StringBuilder();
        sb.AppendLine($"Server: {row.Server} (SV{row.ServerNumber})");
        sb.AppendLine($"Trạng thái: {row.StateText}");
        sb.AppendLine($"Mã tiến trình: {(row.Pid is { } pid ? pid.ToString() : "—")}");
        if (row.State == ClientState.Running)
            sb.AppendLine($"Kết nối game: {(row.Connected switch { true => "có", false => "không", null => "chưa rõ" })}");
        sb.AppendLine($"Auto login: {(row.Tick ? "có ✓" : "không")}");
        if (row.Note.Length > 0) sb.AppendLine($"Ghi chú: {row.Note}");
        sb.Append($"Dữ liệu: {AccountDir(row.Id)}");
        CardBody.Text = sb.ToString();
        CardError.Text = row.Error ?? "";
    }

    private void UpdateStatus()
    {
        int running = _rows.Count(r => r.State == ClientState.Running);
        int queued = _rows.Count(r => r.State == ClientState.Queued);
        string text = $"ToolQL {Version} | Acc: {_rows.Count} | Đang chạy: {running}" + (queued > 0 ? $" | Chờ mở: {queued}" : "") +
                      $" | Auto login: {(_autoLogin ? "BẬT" : "TẮT")}";
        if (_hub.MaintenanceActive)
            text += $" | ĐANG BẢO TRÌ ({_settings.MaintenanceStart}-{_settings.MaintenanceEnd}): đã đóng client, sẽ mở lại khi hết khung giờ";
        if (_template.Running) text += " | Đang tự tải dữ liệu game lần đầu (đừng đụng chuột khoảng nửa phút)";
        if (ClientCommand.Check(_settings) is { } problem) text += " | " + problem;
        StatusText.Text = text;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        _timer.Stop();
        _template.Cancel();
        _log.Write("ToolQL đóng: tắt mọi client");
        _hub.Dispose();       // giết mọi client và xóa thông tin đăng nhập khỏi kho RMS của chúng
        _job.Dispose();
        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
    }
}
