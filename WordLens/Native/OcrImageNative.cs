using System;
using System.Runtime.InteropServices;

namespace WordLens.Native;

internal static partial class OcrImageNative
{
    private const string LibName = "native";

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeByteBuffer
    {
        public readonly IntPtr Data;
        public readonly UIntPtr Len;
        public readonly UIntPtr Capacity;
    }

    [LibraryImport(LibName, EntryPoint = "preprocess_ocr_bgra_to_png")]
    private static partial int PreprocessOcrBgraToPng(
        IntPtr pixels,
        uint width,
        uint height,
        uint stride,
        out NativeByteBuffer buffer);

    [LibraryImport(LibName, EntryPoint = "free_byte_buffer")]
    private static partial void FreeByteBuffer(IntPtr data, UIntPtr len, UIntPtr capacity);

    [LibraryImport(LibName, EntryPoint = "get_last_native_error")]
    private static partial IntPtr GetLastNativeErrorPtr();

    [LibraryImport(LibName, EntryPoint = "free_c_string")]
    private static partial void FreeCString(IntPtr ptr);

    public static byte[] PreprocessBgraToPng(IntPtr pixels, int width, int height, int stride)
    {
        if (pixels == IntPtr.Zero)
            throw new ArgumentException("Pixel buffer address cannot be zero.", nameof(pixels));

        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Image width and height must be greater than zero.");

        if (stride < width * 4)
            throw new ArgumentOutOfRangeException(nameof(stride), "Image stride must be at least width * 4.");

        var status = PreprocessOcrBgraToPng(
            pixels,
            checked((uint)width),
            checked((uint)height),
            checked((uint)stride),
            out var nativeBuffer);

        if (status != 0 || nativeBuffer.Data == IntPtr.Zero)
        {
            throw new InvalidOperationException($"OCR image preprocessing failed ({status}): {GetLastNativeError()}");
        }

        try
        {
            var length = checked((int)nativeBuffer.Len);
            var buffer = new byte[length];
            Marshal.Copy(nativeBuffer.Data, buffer, 0, length);
            return buffer;
        }
        finally
        {
            FreeByteBuffer(nativeBuffer.Data, nativeBuffer.Len, nativeBuffer.Capacity);
        }
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
