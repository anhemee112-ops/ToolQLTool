using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ToolQL.Core;
using ToolQL.Render;

namespace ToolQL.App;

/// <summary>
/// Cửa sổ chính: danh sách acc, Bắt đầu/Dừng từng acc, khung xem MỘT acc, và lưới xem tối đa 4 acc. Chỉ vẽ khi đang xem (một
/// acc hoặc lưới) và cửa sổ không thu nhỏ; acc không được xem thì không tạo snapshot, không vẽ (chỉ giữ kết nối).
/// Xem riêng vẽ 10 khung/giây, lưới 5 khung/giây (mục tiêu tạm của plan v0.1, chốt sau khi đo ở M4); mọi nơi đều chỉ vẽ lại
/// khi trạng thái game thật sự đổi.
/// Tham số dòng lệnh (để thử, không cần nối mạng): <c>--thu-muc-du-lieu &lt;thư mục&gt;</c> đặt nơi lưu acc và cài đặt;
/// <c>--demo &lt;packets.jsonl&gt;</c> thêm một acc demo phát lại từ tệp bắt gói (dùng được nhiều lần để có nhiều acc demo).
/// </summary>
public partial class MainWindow : Window
{
    private static readonly TimeSpan SingleInterval = TimeSpan.FromMilliseconds(100);   // 10 khung/giây
    private static readonly TimeSpan GridInterval = TimeSpan.FromMilliseconds(200);     // 5 khung/giây

    /// <summary>Khung nhìn theo nhân vật (điểm ảnh game, trước khi phóng to cho vừa khung xem). Kích thước do ToolQL chọn, không phải của client.</summary>
    private static readonly Viewport ViewViewport = new(640, 360);

    /// <summary>Khung nhìn của mỗi ô lưới: nhỏ hơn vì mỗi ô chỉ rộng chừng 1/2 khung xem.</summary>
    private static readonly Viewport CellViewport = new(384, 216);

    private readonly AccountStore _store;
    private readonly string _settingsPath;
    private readonly AppSettings _settings;
    private readonly SessionHub _hub;
    private readonly ObservableCollection<AccountRow> _rows = new();
    private readonly DispatcherTimer _viewTimer = new(DispatcherPriority.Render);
    private readonly List<GridCell> _cells = new();
    private TileCache? _tiles;
    private MapViewer _viewer;
    private AccountRow? _viewing;
    private bool _gridMode;
    private WriteableBitmap? _bitmap;
    private int _drawTotal;

    public MainWindow()
    {
        InitializeComponent();

        string[] args = Environment.GetCommandLineArgs();
        string? dataDir = ArgValues(args, "--thu-muc-du-lieu").FirstOrDefault();
        string accountsPath = dataDir is null ? AccountStore.DefaultPath : Path.Combine(dataDir, "accounts.json");
        _settingsPath = dataDir is null ? AppSettings.DefaultPath : Path.Combine(dataDir, "settings.json");

        _store = new AccountStore(accountsPath, new DpapiPasswordProtector());
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
            _settings = new AppSettings();
            _viewer = new MapViewer(null);
            _hub = new SessionHub(_store);
            return;
        }

        _hub = new SessionHub(_store);
        _hub.Changed += OnHubChanged;
        _tiles = OpenTiles();
        _viewer = new MapViewer(_tiles, ViewViewport, ownsTiles: false);

        foreach (var e in _store.Entries) _rows.Add(new AccountRow(e.Id, e.User, e.Server, e.Note));
        int demoNumber = 0;
        foreach (var demo in ArgValues(args, "--demo")) AddDemo(demo, ++demoNumber);
        AccountList.ItemsSource = _rows;
        if (_rows.Count > 0) AccountList.SelectedIndex = 0;

        _viewTimer.Tick += OnViewTick;
        StateChanged += (_, _) => UpdateViewTimer();
        Closed += OnWindowClosed;
        UpdateUi();
    }

    private static IEnumerable<string> ArgValues(string[] args, string name)
    {
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == name) yield return args[i + 1];
    }

    private AccountRow? Selected => AccountList.SelectedItem as AccountRow;

    // ---------- tài nguyên ----------

    private TileCache? OpenTiles()
    {
        string path = _settings.ClientJarPath;
        if (path.Length == 0) return null;
        try
        {
            return new TileCache(ClientAssets.OpenJar(path));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            MessageBox.Show($"Không mở được jar của client:\n{path}\n\n{ex.Message}\n\nKhung xem sẽ vẽ không có ô lát.",
                "ToolQL", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
    }

    private void AddDemo(string capturePath, int number)
    {
        if (!File.Exists(capturePath))
        {
            MessageBox.Show("Không thấy tệp bắt gói: " + capturePath, "ToolQL", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var id = Guid.NewGuid();
        var row = new AccountRow(id, $"demo {number}", "tệp bắt gói", Path.GetFileName(Path.GetDirectoryName(capturePath)) ?? "", isDemo: true);
        _hub.RegisterDemo(id, CaptureReader.Replay(capturePath));
        row.SetState(_hub.StateOf(id), _hub.MessageOf(id));
        _rows.Add(row);
    }

    // ---------- trạng thái từ các phiên (luồng khác) ----------

    private void OnHubChanged(Guid id, SessionState state, string? message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var row = _rows.FirstOrDefault(r => r.Id == id);
            if (row is null) return;
            row.SetState(state, message);
            UpdateUi();
        });
    }

    // ---------- nút bấm ----------

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateUi();

    private void OnListDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_gridMode && Selected is { State: SessionState.InGame }) OnToggleView(sender, e);
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        var dialog = new AccountDialog { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var entry = _store.Add(dialog.User, dialog.Password!, dialog.Server, dialog.Note);
            var row = new AccountRow(entry.Id, entry.User, entry.Server, entry.Note);
            _rows.Add(row);
            AccountList.SelectedItem = row;
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "ToolQL", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        UpdateUi();
    }

    private void OnEdit(object sender, RoutedEventArgs e)
    {
        if (Selected is not { IsDemo: false } row) return;
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

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (Selected is not { IsDemo: false } row) return;
        if (MessageBox.Show($"Xóa acc {row.User} ({row.Server})?", "ToolQL", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        if (_viewing == row) StopViewing();
        _store.Remove(row.Id);
        _rows.Remove(row);
        UpdateUi();
    }

    private void OnStart(object sender, RoutedEventArgs e)
    {
        if (Selected is not { IsDemo: false } row) return;
        try
        {
            var outcome = _hub.Start(row.Id);
            if (outcome.Result == StartResult.TooSoon)
                MessageBox.Show($"Acc {row.User} vừa ngắt kết nối. Phải chờ thêm {Math.Ceiling(outcome.Wait.TotalSeconds):0} giây nữa mới đăng nhập lại được " +
                                "(đăng nhập lại sớm hơn 40 giây thì server khóa acc).", "ToolQL", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or KeyNotFoundException)
        {
            MessageBox.Show(ex.Message, "ToolQL", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        UpdateUi();
    }

    private void OnStop(object sender, RoutedEventArgs e)
    {
        if (Selected is not { IsDemo: false } row) return;
        StopButton.IsEnabled = false;
        // Stop chờ luồng phiên kết thúc (vài giây tối đa): không chặn giao diện.
        Task.Run(() => _hub.Stop(row.Id)).ContinueWith(_ => Dispatcher.BeginInvoke(UpdateUi));
    }

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog(_settings.ClientJarPath) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        _settings.ClientJarPath = dialog.JarPath;
        try
        {
            _settings.Save(_settingsPath);
        }
        catch (IOException ex)
        {
            MessageBox.Show("Không lưu được cài đặt: " + ex.Message, "ToolQL", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // Mọi bộ vẽ đang dùng bộ ô lát cũ: hủy hết trước khi đóng nó. Các ô lưới được dựng lại ở lần vẽ kế tiếp.
        ClearGrid();
        _viewer.Dispose();
        _tiles?.Dispose();
        _tiles = OpenTiles();
        _viewer = new MapViewer(_tiles, ViewViewport, ownsTiles: false);
        UpdateUi();
    }

    // ---------- xem một acc ----------

    private void OnToggleView(object sender, RoutedEventArgs e)
    {
        if (_gridMode) return;
        if (_viewing != null)
        {
            StopViewing();
        }
        else if (Selected is { State: SessionState.InGame } row)
        {
            _viewing = row;
            _viewer.Invalidate();
            ViewHint.Visibility = Visibility.Collapsed;
            UpdateViewTimer();
            OnViewTick(this, EventArgs.Empty);   // vẽ ngay khung đầu
        }
        UpdateUi();
    }

    private void StopViewing()
    {
        _viewing = null;
        UpdateViewTimer();
        ViewImage.Source = null;
        _bitmap = null;
        ViewHint.Visibility = Visibility.Visible;
    }

    // ---------- lưới nhiều acc ----------

    private void OnToggleGrid(object sender, RoutedEventArgs e)
    {
        if (_gridMode)
        {
            ExitGrid();
        }
        else
        {
            StopViewing();
            _gridMode = true;
            SingleLayer.Visibility = Visibility.Collapsed;
            GridLayer.Visibility = Visibility.Visible;
            UpdateViewTimer();
            TickGrid();   // vẽ ngay
        }
        UpdateUi();
    }

    private void ExitGrid()
    {
        _gridMode = false;
        ClearGrid();
        GridLayer.Visibility = Visibility.Collapsed;
        SingleLayer.Visibility = Visibility.Visible;
        UpdateViewTimer();
    }

    private void OnCellDoubleClick(AccountRow row)
    {
        ExitGrid();
        AccountList.SelectedItem = row;
        OnToggleView(this, new RoutedEventArgs());
    }

    private void ClearGrid()
    {
        foreach (var cell in _cells) cell.Dispose();
        _cells.Clear();
        GridCells.Children.Clear();
    }

    /// <summary>Dựng lại các ô nếu tập acc "Trong game" (tối đa 4 đầu danh sách) đã đổi.</summary>
    private void RefreshGridCells()
    {
        var chosen = GridPlan.Choose(_rows.Select(r => (r.Id, r.State)));
        if (chosen.SequenceEqual(_cells.Select(c => c.Row.Id))) return;

        ClearGrid();
        foreach (var id in chosen)
        {
            var cell = new GridCell(_rows.First(r => r.Id == id), _tiles, CellViewport, OnCellDoubleClick);
            _cells.Add(cell);
            GridCells.Children.Add(cell.Root);
        }
        var (columns, rows) = GridPlan.Layout(_cells.Count);
        GridCells.Columns = Math.Max(columns, 1);
        GridCells.Rows = Math.Max(rows, 1);
        GridHint.Visibility = _cells.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TickGrid()
    {
        RefreshGridCells();
        bool drew = false;
        foreach (var cell in _cells)
        {
            cell.UpdateLabel();
            var game = _hub.GameOf(cell.Row.Id);
            if (game is null) continue;
            using var bitmap = cell.Viewer.Render(game.Snapshot());
            if (bitmap is null) continue;   // trạng thái chưa đổi: không vẽ
            cell.Show(bitmap);
            _drawTotal++;
            drew = true;
        }
        if (drew) UpdateStatus();
    }

    // ---------- bộ đếm giờ vẽ ----------

    /// <summary>Bộ đếm giờ chỉ chạy khi đang xem (một acc hoặc lưới) và cửa sổ không thu nhỏ; khoảng cách tùy chế độ.</summary>
    private void UpdateViewTimer()
    {
        bool watching = _gridMode || _viewing != null;
        _viewTimer.Interval = _gridMode ? GridInterval : SingleInterval;
        if (watching && WindowState != WindowState.Minimized) _viewTimer.Start();
        else _viewTimer.Stop();
    }

    private void OnViewTick(object? sender, EventArgs e)
    {
        if (_gridMode)
        {
            TickGrid();
            return;
        }
        if (_viewing is null) return;
        var game = _hub.GameOf(_viewing.Id);
        if (game is null) return;
        using var bitmap = _viewer.Render(game.Snapshot());
        if (bitmap is null) return;   // trạng thái chưa đổi: không vẽ
        _drawTotal++;
        _bitmap = SkiaBridge.CopyTo(bitmap, _bitmap);
        ViewImage.Source = _bitmap;
        UpdateStatus();
    }

    // ---------- giao diện ----------

    private void UpdateUi()
    {
        var row = Selected;
        bool real = row is { IsDemo: false };
        bool running = row != null && SessionStateText.IsRunning(row.State);

        EmptyHint.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EditButton.IsEnabled = real && !running;
        RemoveButton.IsEnabled = real && !running;
        StartButton.IsEnabled = real && !running;
        StopButton.IsEnabled = real && running;
        ViewButton.Content = _viewing != null ? "Ngừng xem" : "Xem";
        ViewButton.IsEnabled = !_gridMode && (_viewing != null || row is { State: SessionState.InGame });
        GridButton.Content = _gridMode ? "Thoát lưới" : "Lưới";

        // Đang xem mà acc rời game (mất kết nối, bị dừng): ngừng xem để không giữ khung cũ.
        if (_viewing is { State: not SessionState.InGame } && !SessionStateText.IsRunning(_viewing.State)) StopViewing();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        int running = _rows.Count(r => SessionStateText.IsRunning(r.State));
        string jar = _settings.ClientJarPath.Length == 0 ? "chưa chọn jar (không có ô lát)" : Path.GetFileName(_settings.ClientJarPath);
        string watching = _gridMode ? $"Lưới {_cells.Count} acc" : _viewing is null ? "Không xem" : $"Đang xem {_viewing.User}";
        string text = $"Acc: {_rows.Count} | Đang chạy: {running} | {watching} | Số lần vẽ: {_drawTotal} | Jar: {jar}";
        if (Selected?.Message is { Length: > 0 } message) text += " | " + message;
        StatusText.Text = text;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _viewTimer.Stop();
        _hub.Dispose();
        ClearGrid();
        _viewer.Dispose();
        _tiles?.Dispose();
    }
}
