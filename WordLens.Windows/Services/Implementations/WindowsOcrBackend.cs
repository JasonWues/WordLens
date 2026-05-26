using Windows.Foundation;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using WordLens.Abstractions.Services;

namespace WordLens.Windows.Services.Implementations;

public sealed class WindowsOcrBackend : ILocalOcrBackend
{
    public bool IsSupported => OcrEngine.AvailableRecognizerLanguages.Count > 0;

    public async Task<string?> RecognizePngAsync(
        byte[] pngBytes,
        string languageCode = "auto",
        CancellationToken cancellationToken = default)
    {
        if (pngBytes.Length == 0)
            return null;

        var engine = CreateOcrEngine(languageCode);
        using var stream = await CreateImageStreamAsync(pngBytes, cancellationToken);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken);
        var transform = CreateScaleTransform(decoder);

        using var bitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage)
            .AsTask(cancellationToken);

        var result = await engine.RecognizeAsync(bitmap).AsTask(cancellationToken);
        var text = result.Text.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public Task<string[]> GetSupportedLanguagesAsync(CancellationToken cancellationToken = default)
    {
        var languages = OcrEngine.AvailableRecognizerLanguages
            .Select(language => language.LanguageTag)
            .Prepend("auto")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult(languages);
    }

    private static OcrEngine CreateOcrEngine(string languageCode)
    {
        if (!string.IsNullOrWhiteSpace(languageCode) &&
            !string.Equals(languageCode, "auto", StringComparison.OrdinalIgnoreCase))
        {
            var language = new Language(languageCode);
            if (OcrEngine.IsLanguageSupported(language))
            {
                var languageEngine = OcrEngine.TryCreateFromLanguage(language);
                if (languageEngine != null)
                    return languageEngine;
            }
        }

        return OcrEngine.TryCreateFromUserProfileLanguages() ??
               throw new PlatformNotSupportedException("当前 Windows 用户配置中没有可用的 OCR 识别语言。");
    }

    private static async Task<InMemoryRandomAccessStream> CreateImageStreamAsync(
        byte[] pngBytes,
        CancellationToken cancellationToken)
    {
        var stream = new InMemoryRandomAccessStream();
        using var writer = new DataWriter(stream);
        writer.WriteBytes(pngBytes);
        await writer.StoreAsync().AsTask(cancellationToken);
        writer.DetachStream();
        stream.Seek(0);
        return stream;
    }

    private static BitmapTransform CreateScaleTransform(BitmapDecoder decoder)
    {
        var maxDimension = OcrEngine.MaxImageDimension;
        if (decoder.PixelWidth <= maxDimension && decoder.PixelHeight <= maxDimension)
            return new BitmapTransform();

        var scale = Math.Min(
            (double)maxDimension / decoder.PixelWidth,
            (double)maxDimension / decoder.PixelHeight);

        return new BitmapTransform
        {
            ScaledWidth = Math.Max(1, (uint)Math.Round(decoder.PixelWidth * scale)),
            ScaledHeight = Math.Max(1, (uint)Math.Round(decoder.PixelHeight * scale))
        };
    }
}
