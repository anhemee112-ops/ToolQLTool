namespace ToolQL.Core;

/// <summary>
/// Thời gian chờ tăng dần khi mất mạng: 40 s, 80 s, 160 s ... tối đa 5 phút. <see cref="Reset"/> khi vào game.
/// Mốc đầu là 40 s vì đăng nhập lại sớm hơn sau khi mất kết nối thì server khóa acc (chủ repo báo, 2026-10-30).
/// </summary>
public sealed class Backoff
{
    /// <summary>Thời gian chờ tối thiểu trước khi đăng nhập lại sau khi mất kết nối.</summary>
    public static readonly TimeSpan MinReconnectDelay = TimeSpan.FromSeconds(40);

    private readonly TimeSpan _initial;
    private readonly TimeSpan _max;
    private int _attempt;

    public Backoff(TimeSpan? initial = null, TimeSpan? max = null)
    {
        _initial = initial ?? MinReconnectDelay;
        _max = max ?? TimeSpan.FromMinutes(5);
    }

    /// <summary>Thời gian chờ cho lần thử kế tiếp.</summary>
    public TimeSpan Next()
    {
        double seconds = _initial.TotalSeconds * Math.Pow(2, Math.Min(_attempt, 30));
        _attempt++;
        return TimeSpan.FromSeconds(Math.Min(seconds, _max.TotalSeconds));
    }

    public void Reset() => _attempt = 0;
}
