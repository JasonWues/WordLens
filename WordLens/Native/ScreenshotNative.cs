using System;
using System.Runtime.InteropServices;

namespace WordLens.Native;

internal static partial class ScreenshotNative
{
    private const string LibName = "native";

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeScreenshotBuffer
    {
        public readonly IntPtr Data;
        public readonly UIntPtr Len;
        public readonly UIntPtr Capacity;
        public readonly uint Width;
        public readonly uint Height;
        public readonly uint Stride;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeVirtualScreenBounds
    {
        public readonly int X;
        public readonly int Y;
        public readonly uint Width;
        public readonly uint Height;
    }

    public readonly record struct ScreenshotData(byte[] Buffer, int Width, int Height, int Stride);

    public readonly record struct VirtualScreenBounds(int X, int Y, int Width, int Height);

    [LibraryImport(LibName, EntryPoint = "capture_screen_region")]
    private static partial int CaptureScreenRegion(
        int x,
        int y,
        uint width,
        uint height,
        out NativeScreenshotBuffer buffer);

    [LibraryImport(LibName, EntryPoint = "free_screenshot_buffer")]
    private static partial void FreeScreenshotBuffer(IntPtr data, UIntPtr len, UIntPtr capacity);

    [LibraryImport(LibName, EntryPoint = "get_virtual_screen_bounds")]
    private static partial int GetVirtualScreenBoundsNative(out NativeVirtualScreenBounds bounds);

    [LibraryImport(LibName, EntryPoint = "get_last_native_error")]
    private static partial IntPtr GetLastNativeErrorPtr();

    [LibraryImport(LibName, EntryPoint = "free_c_string")]
    private static partial void FreeCString(IntPtr ptr);

    public static ScreenshotData CaptureRegion(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Capture width and height must be greater than zero.");
        }

        var status = CaptureScreenRegion(x, y, checked((uint)width), checked((uint)height), out var nativeBuffer);
        if (status != 0 || nativeBuffer.Data == IntPtr.Zero)
        {
            throw new InvalidOperationException($"xcap screenshot failed ({status}): {GetLastNativeError()}");
        }

        try
        {

            var length = checked((int)nativeBuffer.Len);
            var buffer = new byte[length];
            Marshal.Copy(nativeBuffer.Data, buffer, 0, length);

            return new ScreenshotData(
                buffer,
                checked((int)nativeBuffer.Width),
                checked((int)nativeBuffer.Height),
                checked((int)nativeBuffer.Stride));
        }
        finally
        {
            FreeScreenshotBuffer(nativeBuffer.Data, nativeBuffer.Len, nativeBuffer.Capacity);
        }
    }

    public static VirtualScreenBounds GetVirtualScreenBounds()
    {
        var status = GetVirtualScreenBoundsNative(out var bounds);
        if (status != 0)
        {
            throw new InvalidOperationException($"xcap monitor enumeration failed ({status}): {GetLastNativeError()}");
        }

        return new VirtualScreenBounds(
            bounds.X,
            bounds.Y,
            checked((int)bounds.Width),
            checked((int)bounds.Height));
    }

    private static string GetLastNativeError()
    {
        var ptr = IntPtr.Zero;
        try
        {
            ptr = GetLastNativeErrorPtr();
            return ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
        }
        finally
        {
            if (ptr != IntPtr.Zero)
            {
                FreeCString(ptr);
            }
        }
    }
}
