using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ClaudeWatch.Widget;

public static class WindowInterop
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000,
                       WS_EX_TRANSPARENT = 0x20, WS_EX_LAYERED = 0x80000;

    [DllImport("user32.dll")] private static extern long GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] private static extern long SetWindowLongPtr(IntPtr h, int i, long v);

    public static void ApplyWidgetStyles(Window w)
    {
        var h = new WindowInteropHelper(w).Handle;
        SetWindowLongPtr(h, GWL_EXSTYLE,
            GetWindowLongPtr(h, GWL_EXSTYLE) | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    public static void SetClickThrough(Window w, bool on)
    {
        var h = new WindowInteropHelper(w).Handle;
        var ex = GetWindowLongPtr(h, GWL_EXSTYLE);
        ex = on ? ex | WS_EX_TRANSPARENT | WS_EX_LAYERED : ex & ~WS_EX_TRANSPARENT;
        SetWindowLongPtr(h, GWL_EXSTYLE, ex);
    }
}
