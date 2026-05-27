using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WordLens.Abstractions.Services;

namespace WordLens.Linux.Services.Implementations;

public sealed class LinuxTesseractOcrBackend : ILocalOcrBackend
{
    private static readonly string[] AutoLanguagePreference =
    {
        "eng",
        "chi_sim",
        "chi_tra",
        "jpn",
        "kor"
    };

    private readonly ILogger<LinuxTesseractOcrBackend> _logger;

    public LinuxTesseractOcrBackend(ILogger<LinuxTesseractOcrBackend> logger)
    {
        _logger = logger;
    }

    public bool IsSupported => OperatingSystem.IsLinux() && FindTesseractExecutable() != null;

    public async Task<string?> RecognizePngAsync(
        byte[] pngBytes,
        string languageCode = "auto",
        CancellationToken cancellationToken = default)
    {
        if (pngBytes.Length == 0)
            return null;

        var tesseract = FindTesseractExecutable();
        if (tesseract == null)
            throw new PlatformNotSupportedException("未找到 tesseract，请先安装 Tesseract OCR。");

        var tempFile = Path.Combine(Path.GetTempPath(), $"wordlens-ocr-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(tempFile, pngBytes, cancellationToken);

        try
        {
            var installedLanguages = await GetInstalledTesseractLanguagesAsync(tesseract, cancellationToken);
            var tesseractLanguage = ResolveTesseractLanguage(languageCode, installedLanguages);
            var arguments = new List<string> { tempFile, "stdout" };

            if (!string.IsNullOrWhiteSpace(tesseractLanguage))
            {
                arguments.Add("-l");
                arguments.Add(tesseractLanguage);
            }

            var result = await RunProcessAsync(tesseract, arguments, cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Tesseract OCR 执行失败: {result.StandardError.Trim()}");

            var text = result.StandardOutput.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        finally
        {
            TryDeleteTempFile(tempFile);
        }
    }

    public async Task<string[]> GetSupportedLanguagesAsync(CancellationToken cancellationToken = default)
    {
        var tesseract = FindTesseractExecutable();
        if (tesseract == null)
            return Array.Empty<string>();

        var installedLanguages = await GetInstalledTesseractLanguagesAsync(tesseract, cancellationToken);
        var mappedLanguages = installedLanguages
            .Select(MapTesseractLanguageToLanguageCode)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(language => language, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new[] { "auto" }
            .Concat(mappedLanguages)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<HashSet<string>> GetInstalledTesseractLanguagesAsync(
        string tesseract,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await RunProcessAsync(tesseract, new[] { "--list-langs" }, cancellationToken);
            var output = string.Join(Environment.NewLine, result.StandardOutput, result.StandardError);

            return output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => !line.StartsWith("List of available languages", StringComparison.OrdinalIgnoreCase))
                .Where(line => !line.Contains(' '))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "读取 Tesseract OCR 语言列表失败，将使用默认语言配置");
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? ResolveTesseractLanguage(string languageCode, HashSet<string> installedLanguages)
    {
        if (string.IsNullOrWhiteSpace(languageCode) ||
            languageCode.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            if (installedLanguages.Count == 0)
                return null;

            var autoLanguages = AutoLanguagePreference
                .Where(installedLanguages.Contains)
                .ToArray();

            return autoLanguages.Length == 0 ? null : string.Join('+', autoLanguages);
        }

        var tesseractLanguage = MapLanguageCodeToTesseractLanguage(languageCode);
        if (installedLanguages.Count == 0 || installedLanguages.Contains(tesseractLanguage))
            return tesseractLanguage;

        throw new PlatformNotSupportedException($"Tesseract OCR 未安装语言包: {tesseractLanguage}");
    }

    private static string MapLanguageCodeToTesseractLanguage(string languageCode)
    {
        return languageCode switch
        {
            "en" or "en-US" => "eng",
            "zh" or "zh-CN" or "zh-Hans" => "chi_sim",
            "zh-TW" or "zh-HK" or "zh-Hant" => "chi_tra",
            "ja" or "ja-JP" => "jpn",
            "ko" or "ko-KR" => "kor",
            "fr" or "fr-FR" => "fra",
            "de" or "de-DE" => "deu",
            "es" or "es-ES" => "spa",
            _ => languageCode.Replace('-', '_')
        };
    }

    private static string? MapTesseractLanguageToLanguageCode(string tesseractLanguage)
    {
        return tesseractLanguage switch
        {
            "eng" => "en-US",
            "chi_sim" => "zh-CN",
            "chi_tra" => "zh-TW",
            "jpn" => "ja-JP",
            "kor" => "ko-KR",
            "fra" => "fr-FR",
            "deu" => "de-DE",
            "spa" => "es-ES",
            "osd" => null,
            _ => tesseractLanguage.Replace('_', '-')
        };
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        if (!process.Start())
            throw new InvalidOperationException($"无法启动进程: {fileName}");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            throw;
        }

        return new ProcessResult(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);
    }

    private static string? FindTesseractExecutable()
    {
        if (!OperatingSystem.IsLinux())
            return null;

        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
            return null;

        foreach (var path in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(path, "tesseract");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort cleanup after cancellation.
        }
    }

    private static void TryDeleteTempFile(string tempFile)
    {
        try
        {
            File.Delete(tempFile);
        }
        catch
        {
            // Temporary OCR inputs are disposable; cleanup failure should not hide OCR results.
        }
    }

    private readonly record struct ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
