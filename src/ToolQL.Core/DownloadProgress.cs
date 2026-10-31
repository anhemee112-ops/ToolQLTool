namespace ToolQL.Core;

public enum DownloadStatus
{
    Waiting,
    Done,
    TimedOut,
}

/// <summary>
/// Quyết định khi nào client đã tải xong dữ liệu game lần đầu, chỉ từ số tệp và tổng dung lượng thư mục dữ liệu của nó
/// (đo ngày 2026-10-31: sau khi bấm "Tải dữ liệu", 147 tệp ~773 KB xuất hiện trong vòng 3 giây rồi không đổi nữa).
/// Xong = đã thấy dữ liệu đổi và rồi yên lặng ít nhất <c>quietFor</c>; hết giờ = quá <c>timeout</c> kể từ lúc bắt đầu mà chưa xong.
/// </summary>
public sealed class DownloadProgress
{
    private readonly DateTimeOffset _startedAt;
    private readonly TimeSpan _quietFor, _timeout;
    private bool _seenAny;
    private int _files;
    private long _bytes;
    private DateTimeOffset _lastChange;

    /// <summary>Đã thấy dữ liệu thay đổi so với lần đo đầu (nghĩa là client đã bắt đầu tải).</summary>
    public bool Changed { get; private set; }

    public DownloadProgress(DateTimeOffset startedAt, TimeSpan quietFor, TimeSpan timeout)
    {
        _startedAt = startedAt;
        _quietFor = quietFor;
        _timeout = timeout;
        _lastChange = startedAt;
    }

    public DownloadStatus Observe(int files, long bytes, DateTimeOffset now)
    {
        if (!_seenAny)
        {
            _seenAny = true;
            (_files, _bytes) = (files, bytes);
        }
        else if (files != _files || bytes != _bytes)
        {
            (_files, _bytes) = (files, bytes);
            _lastChange = now;
            Changed = true;
        }

        if (Changed && now - _lastChange >= _quietFor) return DownloadStatus.Done;
        return now - _startedAt >= _timeout ? DownloadStatus.TimedOut : DownloadStatus.Waiting;
    }
}
