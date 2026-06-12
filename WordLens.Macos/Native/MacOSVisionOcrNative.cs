using System;
using System.Runtime.InteropServices;

namespace WordLens.Macos.Native;

/// <summary>
///     P/Invoke bridge to the Rust native helper's macOS Vision OCR functions.
///     Mirrors the string ownership contract used by <c>SelectionNative</c> /
///     <c>ScreenshotNative</c>: returned pointers are freed with <c>free_c_string</c>,
///     and a null result means failure (details via <c>get_last_native_error</c>).
/// </summary>
internal static partial class MacOSVisionOcrNative
{
    private const string LibName = "native";

    [LibraryImport(LibName, EntryPoint = "recognize_text_macos", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr RecognizeTextMacos(byte[] data, nuint len, string? languages);

    [LibraryImport(LibName, EntryPoint = "vision_ocr_supported_languages")]
    private static partial IntPtr VisionOcrSupportedLanguagesPtr();

    [LibraryImport(LibName, EntryPoint = "get_last_native_error")]
    private static partial IntPtr GetLastNativeErrorPtr();

    [LibraryImport(LibName, EntryPoint = "free_c_string")]
    private static partial void FreeCString(IntPtr ptr);

    /// <summary>
    ///     Recognizes text in a PNG image. <paramref name="languages"/> is a
    ///     comma-separated list of BCP-47 codes in priority order, or null/empty
    ///     to let Vision auto-detect. Returns the recognized text (possibly empty),
    ///     or throws when the native call fails.
    /// </summary>
    public static string? RecognizePng(byte[] pngBytes, string? languages)
    {
        var ptr = IntPtr.Zero;
        try
        {
            ptr = RecognizeTextMacos(pngBytes, (nuint)pngBytes.Length, languages);
            if (ptr == IntPtr.Zero)
                throw new InvalidOperationException($"Vision OCR 失败: {GetLastNativeError()}");

            return Marshal.PtrToStringUTF8(ptr);
        }
        finally
        {
            if (ptr != IntPtr.Zero)
                FreeCString(ptr);
        }
    }

    /// <summary>
    ///     Returns the Vision-supported recognition languages as BCP-47 codes,
    ///     or an empty array if the native call reports none.
    /// </summary>
    public static string[] GetSupportedLanguages()
    {
        var ptr = IntPtr.Zero;
        try
        {
            ptr = VisionOcrSupportedLanguagesPtr();
            if (ptr == IntPtr.Zero)
                return Array.Empty<string>();

            var raw = Marshal.PtrToStringUTF8(ptr);
            return string.IsNullOrEmpty(raw)
                ? Array.Empty<string>()
                : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        finally
        {
            if (ptr != IntPtr.Zero)
                FreeCString(ptr);
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
                FreeCString(ptr);
        }
    }
}
