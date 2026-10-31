using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ToolQL.Core;

namespace ToolQL.App;

/// <summary>Hàm Windows để tìm, xếp, ẩn, hiện cửa sổ client theo mã tiến trình.</summary>
public static class WindowOps
{
    private const int SW_MINIMIZE = 6, SW_RESTORE = 9;
    private const string SwingFrameClass = "SunAwtFrame";   // lớp cửa sổ của Swing: cửa sổ chính của trình J2ME

    private delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc proc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetWindowText(IntPtr hwnd, string text);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr hwnd, int x, int y, int w, int h, bool repaint);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);

    private struct Rect { public int Left, Top, Right, Bottom; }

    /// <summary>Cửa sổ chính (hiện, lớp Swing) của tiến trình; <see cref="IntPtr.Zero"/> nếu chưa có.</summary>
    public static IntPtr FindMainWindow(int pid)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out uint owner);
            if (owner != (uint)pid || !IsWindowVisible(hwnd)) return true;
            var name = new StringBuilder(64);
            GetClassName(hwnd, name, name.Capacity);
            if (name.ToString() != SwingFrameClass) return true;
            found = hwnd;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    public static void SetTitle(IntPtr hwnd, string title) => SetWindowText(hwnd, title);

    public static (int Width, int Height)? SizeOf(IntPtr hwnd) =>
        GetWindowRect(hwnd, out var r) ? (r.Right - r.Left, r.Bottom - r.Top) : null;

    /// <summary>Đặt cửa sổ vào vị trí; nếu đang thu nhỏ thì khôi phục trước.</summary>
    public static void Place(IntPtr hwnd, WindowRect rect)
    {
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        MoveWindow(hwnd, rect.X, rect.Y, rect.Width, rect.Height, true);
    }

    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref Point point);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    private struct Point { public int X, Y; }

    private static readonly IntPtr HwndTopmost = new(-1), HwndNoTopmost = new(-2);
    private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_SHOWWINDOW = 0x40, MOUSEEVENTF_LEFTDOWN = 0x2, MOUSEEVENTF_LEFTUP = 0x4;

    /// <summary>
    /// Bấm chuột trái THẬT vào một điểm của vùng client, tính theo tỉ lệ (0..1) của vùng client. Java AWT không nhận thông điệp chuột gửi
    /// bằng PostMessage (đã thử), nên phải đưa cửa sổ lên trên cùng (không cần giành tiêu điểm), bấm, rồi trả con trỏ và cửa sổ đang
    /// được chọn về chỗ cũ. Chặn luồng giao diện khoảng 0,4 giây. False nếu điểm bấm nằm ngoài màn hình.
    /// </summary>
    public static bool ClickClient(IntPtr hwnd, double ratioX, double ratioY)
    {
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        if (!GetClientRect(hwnd, out var client) || client.Right <= 0 || client.Bottom <= 0) return false;
        var target = new Point();
        ClientToScreen(hwnd, ref target);
        target.X += (int)(client.Right * ratioX);
        target.Y += (int)(client.Bottom * ratioY);
        int left = GetSystemMetrics(76), top = GetSystemMetrics(77), width = GetSystemMetrics(78), height = GetSystemMetrics(79);   // màn hình ảo (mọi màn hình)
        if (target.X < left || target.Y < top || target.X >= left + width || target.Y >= top + height) return false;

        GetCursorPos(out var savedCursor);
        IntPtr previous = GetForegroundWindow();
        SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        SetForegroundWindow(hwnd);
        Thread.Sleep(150);
        SetCursorPos(target.X, target.Y);
        Thread.Sleep(80);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(60);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(60);
        SetCursorPos(savedCursor.X, savedCursor.Y);
        SetWindowPos(hwnd, HwndNoTopmost, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        if (previous != IntPtr.Zero && previous != hwnd) SetForegroundWindow(previous);
        return true;
    }

    [DllImport("psapi.dll")] private static extern bool EmptyWorkingSet(IntPtr process);

    /// <summary>
    /// Trả các trang RAM ít dùng của tiến trình về Windows (RAM vật lý của client JVM giảm từ ~95 MB xuống ~15 MB, trang cần lại sẽ nạp lại).
    /// Không đụng tới bộ nhớ riêng (commit) của tiến trình. Lỗi (tiến trình đã thoát, không đủ quyền) thì bỏ qua.
    /// </summary>
    public static void TrimMemory(int pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            EmptyWorkingSet(process.Handle);
        }
        catch (Exception)
        {
        }
    }

    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr hdc, IntPtr bitmap, uint start, uint lines, byte[]? bits, ref BitmapInfo info, uint usage);

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public int Size, Width, Height;
        public short Planes, BitCount;
        public int Compression, SizeImage, XPelsPerMeter, YPelsPerMeter, ColorUsed, ColorImportant;
    }

    private const uint PW_RENDERFULLCONTENT = 2;

    /// <summary>
    /// Chụp vùng client của cửa sổ bằng <c>PrintWindow</c> (đã thử: chụp đủ nội dung dù cửa sổ khác đè lên, không giành tiêu điểm, không
    /// cần đưa lên trên). Null nếu cửa sổ đang thu nhỏ hoặc chụp không được.
    /// </summary>
    public static BgraFrame? CaptureClient(IntPtr hwnd)
    {
        if (IsIconic(hwnd) || !GetWindowRect(hwnd, out var window) || !GetClientRect(hwnd, out var client)) return null;
        int width = window.Right - window.Left, height = window.Bottom - window.Top;
        if (width <= 0 || height <= 0 || client.Right <= 0 || client.Bottom <= 0) return null;
        var origin = new Point();
        ClientToScreen(hwnd, ref origin);

        IntPtr screen = GetDC(IntPtr.Zero);
        IntPtr memory = CreateCompatibleDC(screen);
        IntPtr bitmap = CreateCompatibleBitmap(screen, width, height);
        IntPtr previous = SelectObject(memory, bitmap);
        try
        {
            if (!PrintWindow(hwnd, memory, PW_RENDERFULLCONTENT)) return null;
            var info = new BitmapInfo { Size = Marshal.SizeOf<BitmapInfo>(), Width = width, Height = -height, Planes = 1, BitCount = 32 };   // Height âm: từ trên xuống
            var bits = new byte[width * height * 4];
            SelectObject(memory, previous);   // GetDIBits cần bitmap không còn được chọn vào DC
            previous = IntPtr.Zero;
            if (GetDIBits(memory, bitmap, 0, (uint)height, bits, ref info, 0) == 0) return null;
            return new BgraFrame(client.Right, client.Bottom, width * 4, bits, origin.X - window.Left, origin.Y - window.Top);
        }
        finally
        {
            if (previous != IntPtr.Zero) SelectObject(memory, previous);
            DeleteObject(bitmap);
            DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    public static void Minimize(IntPtr hwnd) => ShowWindow(hwnd, SW_MINIMIZE);

    /// <summary>Khôi phục (nếu đang thu nhỏ) và đưa ra trước.</summary>
    public static void Bring(IntPtr hwnd)
    {
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
    }
}

/// <summary>
/// Job object Windows với cờ "giết mọi tiến trình trong job khi job đóng": ToolQL tắt (kể cả sập) thì mọi cửa sổ client tắt theo,
/// như Tool 2 ("Tắt ToolCikey.exe là mọi cửa sổ game cũng tắt theo"). Gắn không được (hệ điều hành cũ) thì bỏ qua, chỉ mất cơ chế dự phòng này.
/// </summary>
public sealed class ChildProcessJob : IDisposable
{
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimit
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass, SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters { public ulong Read, Write, Other, ReadBytes, WriteBytes, OtherBytes; }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimit
    {
        public BasicLimit Basic;
        public IoCounters Io;
        public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll")] private static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref ExtendedLimit info, uint size);
    [DllImport("kernel32.dll")] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

    private IntPtr _job;

    public ChildProcessJob()
    {
        _job = CreateJobObject(IntPtr.Zero, null);
        if (_job == IntPtr.Zero) return;
        var info = new ExtendedLimit { Basic = { LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE } };
        if (!SetInformationJobObject(_job, JobObjectExtendedLimitInformation, ref info, (uint)Marshal.SizeOf<ExtendedLimit>()))
        {
            CloseHandle(_job);
            _job = IntPtr.Zero;
        }
    }

    public bool Add(Process process) => _job != IntPtr.Zero && AssignProcessToJobObject(_job, process.Handle);

    public void Dispose()
    {
        if (_job != IntPtr.Zero) CloseHandle(_job);   // đóng job = giết mọi tiến trình con còn trong job
        _job = IntPtr.Zero;
    }
}

public sealed class SystemClientProcess : IClientProcess
{
    private readonly Process _process;

    public SystemClientProcess(Process process) => _process = process;

    public int Id => _process.Id;
    public bool HasExited => _process.HasExited;
    public int ExitCode => _process.HasExited ? _process.ExitCode : 0;

    public void Kill() => _process.Kill(entireProcessTree: true);

    public void Dispose() => _process.Dispose();
}

/// <summary>Mở tiến trình client thật (javaw.exe chạy AngelChip) và gắn vào <see cref="ChildProcessJob"/>.</summary>
public sealed class SystemClientLauncher : IClientLauncher
{
    private readonly ChildProcessJob _job;

    public SystemClientLauncher(ChildProcessJob job) => _job = job;

    public IClientProcess Start(ClientLaunchSpec spec)
    {
        var info = new ProcessStartInfo(spec.FileName, spec.Arguments)
        {
            WorkingDirectory = spec.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        var process = Process.Start(info) ?? throw new InvalidOperationException("Không mở được tiến trình client.");
        _job.Add(process);
        return new SystemClientProcess(process);
    }
}

/// <summary>
/// Đọc bảng kết nối TCP của Windows (IPv4, <c>GetExtendedTcpTable</c>) để biết tiến trình client có kết nối đang mở (ESTABLISHED) tới cổng
/// server game hay không. Chỉ đọc, không cần quyền quản trị. Bảng được dùng lại trong 0,5 giây nên nhiều client chỉ tốn một lần đọc.
/// </summary>
public sealed class TcpTableProbe : IConnectionProbe
{
    private const int AfInet = 2, TcpTableOwnerPidAll = 5, Established = 5, ErrorInsufficientBuffer = 122;

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool sort, int af, int tableClass, uint reserved);

    private readonly HashSet<int> _gamePorts;
    private readonly TimeProvider _time;
    private HashSet<int>? _connectedPids;
    private DateTimeOffset _readAt = DateTimeOffset.MinValue;

    public TcpTableProbe(IEnumerable<int> gamePorts, TimeProvider? time = null)
    {
        _gamePorts = gamePorts.ToHashSet();
        _time = time ?? TimeProvider.System;
    }

    public bool? IsConnected(int processId)
    {
        var now = _time.GetUtcNow();
        if (_connectedPids is null || now - _readAt > TimeSpan.FromMilliseconds(500))
        {
            _connectedPids = Read();
            _readAt = now;
        }
        return _connectedPids?.Contains(processId);
    }

    private HashSet<int>? Read()
    {
        int size = 0;
        IntPtr buffer = IntPtr.Zero;
        try
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                uint result = GetExtendedTcpTable(buffer, ref size, false, AfInet, TcpTableOwnerPidAll, 0);
                if (result == 0) break;
                if (result != ErrorInsufficientBuffer || attempt == 3) return null;
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                buffer = Marshal.AllocHGlobal(size);
            }
            if (buffer == IntPtr.Zero) return new HashSet<int>();   // bảng rỗng

            var pids = new HashSet<int>();
            int count = Marshal.ReadInt32(buffer);
            const int rowSize = 24;   // MIB_TCPROW_OWNER_PID: state, localAddr, localPort, remoteAddr, remotePort, owningPid (6 × uint32)
            for (int i = 0; i < count; i++)
            {
                IntPtr row = buffer + 4 + i * rowSize;
                int state = Marshal.ReadInt32(row);
                int remotePortRaw = Marshal.ReadInt32(row, 16);
                int remotePort = ((remotePortRaw & 0xFF) << 8) | ((remotePortRaw >> 8) & 0xFF);   // cổng nằm ở dạng big-endian
                if (state == Established && _gamePorts.Contains(remotePort)) pids.Add(Marshal.ReadInt32(row, 20));
            }
            return pids;
        }
        finally
        {
            if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
        }
    }
}
