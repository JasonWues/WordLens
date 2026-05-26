using Microsoft.Extensions.Logging;
using WordLens.Abstractions.Services;
using WordLens.Linux.Native;

namespace WordLens.Linux.Services.Implementations;

public sealed class LinuxCursorPositionProvider : ICursorPositionProvider
{
    private readonly ILogger<LinuxCursorPositionProvider> _logger;

    public LinuxCursorPositionProvider(ILogger<LinuxCursorPositionProvider> logger)
    {
        _logger = logger;
    }

    public bool TryGetCursorPosition(out CursorPosition position)
    {
        position = default;

        if (!OperatingSystem.IsLinux())
            return false;

        try
        {
            if (!LinuxCursorNative.TryGetCursorPosition(out var x, out var y))
                return false;

            position = new CursorPosition(x, y);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "获取 Linux 鼠标位置失败");
            return false;
        }
    }
}
