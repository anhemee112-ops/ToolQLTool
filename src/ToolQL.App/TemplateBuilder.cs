using System.IO;
using ToolQL.Core;

namespace ToolQL.App;

/// <summary>
/// Tự tải dữ liệu game lần đầu (việc trước đây người dùng phải làm tay: mở client, bấm "Tải dữ liệu", chờ hiện menu, đóng).
/// Mở MỘT client không tài khoản trong thư mục dữ liệu mẫu, bấm nút "Tải dữ liệu" bằng chuột thật, chờ tệp dữ liệu xuất hiện và yên lặng
/// (xem <see cref="DownloadProgress"/>), rồi đóng client. Kết quả là <c>home\</c> mẫu để sao chép cho từng acc.
/// Client kết nối tới server game để tải nhưng KHÔNG đăng nhập. Chỉ gọi từ luồng giao diện (bộ đếm giờ 1 giây gọi <see cref="Tick"/>).
/// </summary>
public sealed class TemplateBuilder
{
    // Nút "Tải dữ liệu" ở giữa, cao khoảng y = 215 trên vùng client 240×320 (đo 2026-10-31 trên AngelAChip bản gọn).
    private const double ButtonX = 0.5, ButtonY = 215.0 / 320;
    private static readonly TimeSpan WaitWindowMax = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SettleAfterWindow = TimeSpan.FromSeconds(6);   // chờ client nạp xong màn hình "Tải dữ liệu"
    private static readonly TimeSpan ClickAgainAfter = TimeSpan.FromSeconds(15);    // chưa thấy tải gì thì bấm lại (tối đa MaxClicks lần)
    private const int MaxClicks = 3;
    private static readonly TimeSpan QuietFor = TimeSpan.FromSeconds(4), DownloadMax = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan CloseWait = TimeSpan.FromSeconds(3);

    private enum Step { Idle, WaitWindow, Settle, Downloading, Closing }

    private readonly AppSettings _settings;
    private readonly IClientLauncher _launcher;
    private readonly TimeProvider _time;
    private readonly Action<string> _log;

    private Step _step = Step.Idle;
    private IClientProcess? _process;
    private DateTimeOffset _startedAt, _windowAt, _lastClick, _closingAt;
    private IntPtr _hwnd;
    private int _clicks;
    private DownloadProgress? _progress;

    /// <summary>Báo khi xong: true = đã có bản mẫu, false = thất bại (đã dọn bản dở).</summary>
    public event Action<bool>? Finished;

    public bool Running => _step != Step.Idle;

    public TemplateBuilder(AppSettings settings, IClientLauncher launcher, Action<string> log, TimeProvider? time = null)
    {
        _settings = settings;
        _launcher = launcher;
        _log = log;
        _time = time ?? TimeProvider.System;
    }

    private const string MarkerName = "da-tai-xong.txt";

    /// <summary>
    /// Đã có bản mẫu đầy đủ chưa: có tệp đánh dấu "tải xong", hoặc (bản mẫu làm tay trước đây) thư mục <c>home</c> có từ 50 tệp trở lên
    /// (bản tải đầy đủ có ~147 tệp). Bản dở do tải hỏng nửa chừng thì không tính.
    /// </summary>
    public static bool Exists(AppSettings settings)
    {
        string dir = settings.EffectiveTemplateDir, home = Path.Combine(dir, "home");
        if (!Directory.Exists(home)) return false;
        if (File.Exists(Path.Combine(dir, MarkerName))) return true;
        try { return Directory.EnumerateFiles(home, "*", SearchOption.AllDirectories).Take(50).Count() >= 50; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    /// <summary>Bắt đầu tải. Xóa bản mẫu cũ (nếu có). Trả false nếu đang chạy, thiếu cấu hình hoặc không mở được client.</summary>
    public bool Start()
    {
        if (Running) return false;
        if (ClientCommand.Check(_settings) is { } problem)
        {
            _log("Không tự tải dữ liệu game được: " + problem);
            return false;
        }
        string dir = _settings.EffectiveTemplateDir, home = Path.Combine(dir, "home");
        try
        {
            Directory.CreateDirectory(dir);
            File.Delete(Path.Combine(dir, MarkerName));
            if (Directory.Exists(home)) Directory.Delete(home, recursive: true);
            Directory.CreateDirectory(home);
            _process = _launcher.Start(ClientCommand.Build(_settings, dir, "template"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _log("Không mở được client để tải dữ liệu game: " + ex.Message);
            TryDelete(home);
            return false;
        }
        _startedAt = _time.GetUtcNow();
        _hwnd = IntPtr.Zero;
        _clicks = 0;
        _progress = null;
        _step = Step.WaitWindow;
        _log("Bắt đầu tự tải dữ liệu game lần đầu (client không đăng nhập)");
        return true;
    }

    public void Tick()
    {
        if (_step == Step.Idle || _process is null) return;
        var now = _time.GetUtcNow();

        if (_step != Step.Closing && _process.HasExited)
        {
            Fail("client thoát giữa chừng");
            return;
        }

        switch (_step)
        {
            case Step.WaitWindow:
                _hwnd = WindowOps.FindMainWindow(_process.Id);
                if (_hwnd != IntPtr.Zero)
                {
                    _windowAt = now;
                    _step = Step.Settle;
                }
                else if (now - _startedAt >= WaitWindowMax) Fail("client không hiện cửa sổ");
                break;

            case Step.Settle:
                if (now - _windowAt < SettleAfterWindow) break;
                _progress = new DownloadProgress(now, QuietFor, DownloadMax);
                _progress.Observe(CountFiles(out long bytes), bytes, now);   // mốc trước khi bấm
                Click(now);
                _step = Step.Downloading;
                break;

            case Step.Downloading:
                switch (_progress!.Observe(CountFiles(out long size), size, now))
                {
                    case DownloadStatus.Done:
                        _log("Tải dữ liệu game xong");
                        try { _process.Kill(); } catch (InvalidOperationException) { }
                        _closingAt = now;
                        _step = Step.Closing;
                        break;
                    case DownloadStatus.TimedOut:
                        Fail("quá thời gian chờ tải dữ liệu");
                        break;
                    default:
                        if (!_progress.Changed && _clicks < MaxClicks && now - _lastClick >= ClickAgainAfter) Click(now);
                        break;
                }
                break;

            case Step.Closing:
                if (_process.HasExited || now - _closingAt >= CloseWait) Finish(true);
                break;
        }
    }

    /// <summary>Hủy (khi tắt ToolQL giữa chừng): giết client và dọn bản dở.</summary>
    public void Cancel()
    {
        if (Running) Fail("bị hủy");
    }

    private void Click(DateTimeOffset now)
    {
        _lastClick = now;
        _clicks++;
        bool ok = _hwnd != IntPtr.Zero && WindowOps.ClickClient(_hwnd, ButtonX, ButtonY);
        _log($"Bấm \"Tải dữ liệu\" (lần {_clicks}" + (ok ? ")" : ", không bấm được cửa sổ)"));
    }

    private int CountFiles(out long bytes)
    {
        bytes = 0;
        int count = 0;
        string home = Path.Combine(_settings.EffectiveTemplateDir, "home");
        try
        {
            foreach (var file in new DirectoryInfo(home).EnumerateFiles("*", SearchOption.AllDirectories))
            {
                count++;
                bytes += file.Length;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return count;
    }

    private void Fail(string reason)
    {
        _log("Tải dữ liệu game THẤT BẠI: " + reason);
        try { _process?.Kill(); } catch (InvalidOperationException) { }
        Finish(false);
    }

    private void Finish(bool ok)
    {
        _process?.Dispose();
        _process = null;
        _step = Step.Idle;
        if (ok)
        {
            try { File.WriteAllText(Path.Combine(_settings.EffectiveTemplateDir, MarkerName), _time.GetUtcNow().ToString("O")); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        else TryDelete(Path.Combine(_settings.EffectiveTemplateDir, "home"));   // không để lại bản dở, lần sau tải lại
        Finished?.Invoke(ok);
    }

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
