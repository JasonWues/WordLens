namespace WordLens.Abstractions.Services;

public interface ILocalOcrBackend
{
    bool IsSupported { get; }

    Task<string?> RecognizePngAsync(
        byte[] pngBytes,
        string languageCode = "auto",
        CancellationToken cancellationToken = default);

    Task<string[]> GetSupportedLanguagesAsync(CancellationToken cancellationToken = default);
}
