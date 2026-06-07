using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace FH.LanguageComboTool.Wpf;

internal sealed class WindowResizer
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const int WmNcLeftButtonDown = 0x00A1;
    private const int MonitorDefaultToNearest = 0x00000002;

    private readonly Window _window;
    private HwndSource? _source;

    public WindowResizer(Window window)
    {
        _window = window;
        _window.SourceInitialized += OnSourceInitialized;
        _window.Closed += OnClosed;
    }

    public void AddLeft(UIElement element) => AddResizeHandle(element, 10);
    public void AddRight(UIElement element) => AddResizeHandle(element, 11);
    public void AddTop(UIElement element) => AddResizeHandle(element, 12);
    public void AddTopLeft(UIElement element) => AddResizeHandle(element, 13);
    public void AddTopRight(UIElement element) => AddResizeHandle(element, 14);
    public void AddBottom(UIElement element) => AddResizeHandle(element, 15);
    public void AddBottomLeft(UIElement element) => AddResizeHandle(element, 16);
    public void AddBottomRight(UIElement element) => AddResizeHandle(element, 17);

    private void AddResizeHandle(UIElement element, int hitTest)
    {
        element.MouseLeftButtonDown += (_, e) =>
        {
            if (_window.WindowState != WindowState.Normal)
                return;

            var handle = new WindowInteropHelper(_window).Handle;
            ReleaseCapture();
            SendMessage(handle, WmNcLeftButtonDown, (IntPtr)hitTest, IntPtr.Zero);
            e.Handled = true;
        };
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _source = HwndSource.FromHwnd(new WindowInteropHelper(_window).Handle);
        _source?.AddHook(WindowProc);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _source?.RemoveHook(WindowProc);
        _source = null;
    }

    private IntPtr WindowProc(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == WmGetMinMaxInfo)
        {
            ApplyMonitorWorkArea(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static void ApplyMonitorWorkArea(IntPtr hwnd, IntPtr lParam)
    {
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
            return;

        var monitorInfo = MonitorInfo.Create();
        if (!GetMonitorInfoW(monitor, ref monitorInfo))
            return;

        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        minMaxInfo.MaxPosition.X = Math.Abs(monitorInfo.WorkArea.Left - monitorInfo.MonitorArea.Left);
        minMaxInfo.MaxPosition.Y = Math.Abs(monitorInfo.WorkArea.Top - monitorInfo.MonitorArea.Top);
        minMaxInfo.MaxSize.X = Math.Abs(monitorInfo.WorkArea.Right - monitorInfo.WorkArea.Left);
        minMaxInfo.MaxSize.Y = Math.Abs(monitorInfo.WorkArea.Bottom - monitorInfo.WorkArea.Top);
        minMaxInfo.MaxTrackSize = minMaxInfo.MaxSize;
        Marshal.StructureToPtr(minMaxInfo, lParam, false);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoW(IntPtr monitor, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;

        public static MonitorInfo Create() => new()
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };
    }
}
