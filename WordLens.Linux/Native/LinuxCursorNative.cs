using System.Runtime.InteropServices;

namespace WordLens.Linux.Native;

internal static partial class LinuxCursorNative
{
    private const string LibX11 = "libX11.so.6";

    public static bool TryGetCursorPosition(out int x, out int y)
    {
        x = 0;
        y = 0;

        if (!OperatingSystem.IsLinux() || !IsX11Session())
            return false;

        try
        {
            var display = XOpenDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero)
                return false;

            try
            {
                var screen = XDefaultScreen(display);
                var rootWindow = XRootWindow(display, screen);
                if (rootWindow == IntPtr.Zero)
                    return false;

                var result = XQueryPointer(
                    display,
                    rootWindow,
                    out _,
                    out _,
                    out var rootX,
                    out var rootY,
                    out _,
                    out _,
                    out _);

                if (result == 0)
                    return false;

                x = rootX;
                y = rootY;
                return true;
            }
            finally
            {
                XCloseDisplay(display);
            }
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    private static bool IsX11Session()
    {
        var display = Environment.GetEnvironmentVariable("DISPLAY");
        if (string.IsNullOrWhiteSpace(display))
            return false;

        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        if (string.Equals(sessionType, "x11", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase))
            return false;

        return string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
    }

    [LibraryImport(LibX11, EntryPoint = "XOpenDisplay")]
    private static partial IntPtr XOpenDisplay(IntPtr displayName);

    [LibraryImport(LibX11, EntryPoint = "XDefaultScreen")]
    private static partial int XDefaultScreen(IntPtr display);

    [LibraryImport(LibX11, EntryPoint = "XRootWindow")]
    private static partial IntPtr XRootWindow(IntPtr display, int screenNumber);

    [LibraryImport(LibX11, EntryPoint = "XQueryPointer")]
    private static partial int XQueryPointer(
        IntPtr display,
        IntPtr window,
        out IntPtr rootReturn,
        out IntPtr childReturn,
        out int rootXReturn,
        out int rootYReturn,
        out int winXReturn,
        out int winYReturn,
        out uint maskReturn);

    [LibraryImport(LibX11, EntryPoint = "XCloseDisplay")]
    private static partial int XCloseDisplay(IntPtr display);
}
