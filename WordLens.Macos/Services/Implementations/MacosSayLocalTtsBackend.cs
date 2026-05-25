using System.Diagnostics;
using WordLens.Services;

namespace WordLens.Macos.Services.Implementations;

public sealed class MacosSayLocalTtsBackend : ILocalTtsBackend, IDisposable
{
    private readonly Lock _sync = new();
    private Process? _process;

    public async Task SpeakAsync(
        string text,
        string? voice,
        double speed,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        Stop();

        using var registration = cancellationToken.Register(Stop);
        using var process = StartSayProcess(text, voice, speed);

        lock (_sync)
        {
            _process = process;
        }

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_process, process))
                    _process = null;
            }
        }
    }

    public void Stop()
    {
        Process? process;
        lock (_sync)
        {
            process = _process;
            _process = null;
        }

        if (process is not { HasExited: false })
            return;

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private static Process StartSayProcess(string text, string? voice, double speed)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/say",
            UseShellExecute = false
        };

        if (!string.IsNullOrWhiteSpace(voice))
        {
            startInfo.ArgumentList.Add("-v");
            startInfo.ArgumentList.Add(voice.Trim());
        }

        startInfo.ArgumentList.Add("-r");
        startInfo.ArgumentList.Add(ToWordsPerMinute(speed).ToString());
        startInfo.ArgumentList.Add(text);

        return Process.Start(startInfo) ??
               throw new InvalidOperationException("无法启动 macOS say 命令。");
    }

    private static int ToWordsPerMinute(double speed)
    {
        var clamped = Math.Clamp(speed, 0.25, 4.0);
        return Math.Clamp((int)Math.Round(175 * clamped), 80, 500);
    }
}
