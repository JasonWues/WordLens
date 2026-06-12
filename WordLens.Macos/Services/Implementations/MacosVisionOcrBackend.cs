using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WordLens.Abstractions.Services;
using WordLens.Macos.Native;

namespace WordLens.Macos.Services.Implementations;

/// <summary>
///     Local OCR backend backed by the macOS Vision framework
///     (<c>VNRecognizeTextRequest</c>), invoked through the Rust native helper.
///     Fully offline; mirrors <c>WindowsOcrBackend</c> on the .NET side.
/// </summary>
public sealed class MacosVisionOcrBackend : ILocalOcrBackend
{
    public bool IsSupported => OperatingSystem.IsMacOS();

    public async Task<string?> RecognizePngAsync(
        byte[] pngBytes,
        string languageCode = "auto",
        CancellationToken cancellationToken = default)
    {
        if (pngBytes.Length == 0)
            return null;

        var languages = MapToVisionLanguages(languageCode);

        // Vision runs synchronously; offload to the thread pool to keep callers responsive.
        var text = await Task.Run(
            () => MacOSVisionOcrNative.RecognizePng(pngBytes, languages),
            cancellationToken);

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public Task<string[]> GetSupportedLanguagesAsync(CancellationToken cancellationToken = default)
    {
        var visionLanguages = MacOSVisionOcrNative.GetSupportedLanguages();
        var mapped = visionLanguages
            .Select(MapVisionLanguageToLanguageCode)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase);

        var result = new[] { "auto" }
            .Concat(mapped)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult(result);
    }

    /// <summary>
    ///     Maps a WordLens language code to a comma-separated Vision BCP-47 priority
    ///     list. "auto" (or empty) returns null so Vision auto-detects the script.
    /// </summary>
    private static string? MapToVisionLanguages(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode) ||
            languageCode.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return null;

        return languageCode switch
        {
            "zh" or "zh-CN" or "zh-Hans" => "zh-Hans,en-US",
            "zh-TW" or "zh-HK" or "zh-Hant" => "zh-Hant,en-US",
            "en" or "en-US" => "en-US",
            "ja" or "ja-JP" => "ja-JP,en-US",
            "ko" or "ko-KR" => "ko-KR,en-US",
            "fr" or "fr-FR" => "fr-FR",
            "de" or "de-DE" => "de-DE",
            "es" or "es-ES" => "es-ES",
            "it" or "it-IT" => "it-IT",
            "pt" or "pt-BR" or "pt-PT" => "pt-BR",
            "ru" or "ru-RU" => "ru-RU",
            _ => languageCode
        };
    }

    /// <summary>
    ///     Maps a Vision BCP-47 code back to a WordLens language code. Returns null
    ///     for codes WordLens does not surface (filtered out by the caller).
    /// </summary>
    private static string? MapVisionLanguageToLanguageCode(string visionLanguage)
    {
        return visionLanguage switch
        {
            "en-US" or "en" => "en-US",
            "zh-Hans" => "zh-CN",
            "zh-Hant" => "zh-TW",
            "ja-JP" or "ja" => "ja-JP",
            "ko-KR" or "ko" => "ko-KR",
            "fr-FR" or "fr" => "fr-FR",
            "de-DE" or "de" => "de-DE",
            "es-ES" or "es" => "es-ES",
            "it-IT" or "it" => "it-IT",
            "pt-BR" or "pt-PT" or "pt" => "pt-BR",
            "ru-RU" or "ru" => "ru-RU",
            _ => visionLanguage
        };
    }
}
