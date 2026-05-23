using System;
using System.Runtime.InteropServices;

namespace WordLens.Native;

internal static partial class MacOSCursorNative
{
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    public static bool TryGetCursorPosition(out int x, out int y)
    {
        x = 0;
        y = 0;

        if (!OperatingSystem.IsMacOS())
            return false;

        var currentEvent = CGEventCreate(IntPtr.Zero);
        if (currentEvent == IntPtr.Zero)
            return false;

        try
        {
            var location = CGEventGetLocation(currentEvent);
            if (!double.IsFinite(location.X) || !double.IsFinite(location.Y))
                return false;

            x = (int)Math.Round(location.X);
            y = (int)Math.Round(location.Y);
            return true;
        }
        finally
        {
            CFRelease(currentEvent);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CGPoint
    {
        public readonly double X;
        public readonly double Y;
    }

    [LibraryImport(CoreGraphics, EntryPoint = "CGEventCreate")]
    private static partial IntPtr CGEventCreate(IntPtr source);

    [LibraryImport(CoreGraphics, EntryPoint = "CGEventGetLocation")]
    private static partial CGPoint CGEventGetLocation(IntPtr eventRef);

    [LibraryImport(CoreFoundation, EntryPoint = "CFRelease")]
    private static partial void CFRelease(IntPtr cf);
}
