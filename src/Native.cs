using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PresentationOverlay;

[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int left, top, right, bottom;
}

[StructLayout(LayoutKind.Sequential)]
public struct APPBARDATA
{
    public uint cbSize;
    public IntPtr hWnd;
    public uint uCallbackMessage;
    public uint uEdge;
    public RECT rc;
    public IntPtr lParam;
}

public sealed record MonitorInfo(IntPtr Handle, RECT Bounds, RECT Work, bool Primary);

public static class Native
{
    public const uint EVENT_SYSTEM_FOREGROUND = 3;
    public const uint WINEVENT_OUTOFCONTEXT = 0;
    public const uint ABM_NEW = 0, ABM_REMOVE = 1, ABM_QUERYPOS = 2, ABM_SETPOS = 3;
    public const uint ABE_LEFT = 0;
    public const uint SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040;
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    const int GWL_EXSTYLE = -20;
    const long WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;

    public delegate void WinEventProc(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
    delegate bool MonitorEnumProc(IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor, rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc proc, uint pid, uint tid, uint flags);
    [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder sb, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr hMon, ref MONITORINFO mi);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr hMon, int type, out uint x, out uint y);
    [DllImport("shell32.dll")] public static extern UIntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder sb, ref int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint PrivateExtractIcons(string file, int index, int cx, int cy, IntPtr[] icons, uint[]? ids, uint count, uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);

    public static string WindowText(IntPtr hwnd)
    {
        var sb = new StringBuilder(512);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string WindowClass(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string? ProcessPath(uint pid)
    {
        var h = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : null;
        }
        finally { CloseHandle(h); }
    }

    public static List<MonitorInfo> Monitors()
    {
        var list = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(h, ref mi))
                list.Add(new MonitorInfo(h, mi.rcMonitor, mi.rcWork, (mi.dwFlags & 1) != 0));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public static double MonitorScale(IntPtr monitor) =>
        GetDpiForMonitor(monitor, 0 /* MDT_EFFECTIVE_DPI */, out var x, out _) == 0 ? x / 96.0 : 1.0;

    /// <summary>Geen focus stelen, geen taakbalkknop: klikken op de sidebar mag de presentatie niet verstoren.</summary>
    public static void MakeNoActivate(IntPtr hwnd)
    {
        var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));
    }

    /// <summary>Laadt een icoon als afbeelding (png/ico/...) of uit een exe, zo groot mogelijk.</summary>
    public static ImageSource? LoadIcon(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".ico")
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }

            var icons = new IntPtr[1];
            if (PrivateExtractIcons(path, 0, 128, 128, icons, null, 1, 0) > 0 && icons[0] != IntPtr.Zero)
            {
                try
                {
                    var src = Imaging.CreateBitmapSourceFromHIcon(icons[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    src.Freeze();
                    return src;
                }
                finally { DestroyIcon(icons[0]); }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}
