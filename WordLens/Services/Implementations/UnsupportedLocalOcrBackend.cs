using System;
using System.Threading;
using System.Threading.Tasks;
using WordLens.Abstractions.Services;

namespace WordLens.Services.Implementations;

public sealed class UnsupportedLocalOcrBackend : ILocalOcrBackend
{
    public bool IsSupported => false;

    public Task<string?> RecognizePngAsync(
        byte[] pngBytes,
        string languageCode = "auto",
        CancellationToken cancellationToken = default)
    {
        throw new PlatformNotSupportedException("当前平台尚未接入本地 OCR 后端。");
    }

    public Task<string[]> GetSupportedLanguagesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Array.Empty<string>());
    }
}
