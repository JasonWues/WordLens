using System;
using System.Threading;
using System.Threading.Tasks;
using WordLens.Services;

namespace WordLens.Services.Implementations;

public sealed class UnsupportedLocalTtsBackend : ILocalTtsBackend
{
    public Task SpeakAsync(
        string text,
        string? voice,
        double speed,
        CancellationToken cancellationToken = default)
    {
        throw new PlatformNotSupportedException("当前平台尚未接入本地 TTS 后端。");
    }

    public void Stop()
    {
    }
}
