using Microsoft.Extensions.Logging;
using WordLens.Macos.Native;
using WordLens.Services;

namespace WordLens.Macos.Services.Implementations;

public sealed class MacosCursorPositionProvider : ICursorPositionProvider
{
    private readonly ILogger<MacosCursorPositionProvider> _logger;

    public MacosCursorPositionProvider(ILogger<MacosCursorPositionProvider> logger)
    {
        _logger = logger;
    }

    public bool TryGetCursorPosition(out CursorPosition position)
    {
        position = default;

        if (!OperatingSystem.IsMacOS())
            return false;

        try
        {
            if (!MacOSCursorNative.TryGetCursorPosition(out var x, out var y))
                return false;

            position = new CursorPosition(x, y);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "获取 macOS 鼠标位置失败");
            return false;
        }
    }
}
