using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using Windows.Win32;
using WordLens.Abstractions.Services;
using WordLens.Services;
using ZLogger;

namespace WordLens.Windows.Services.Implementations;

public sealed class WindowsCursorPositionProvider : ICursorPositionProvider
{
    private readonly ILogger<WindowsCursorPositionProvider> _logger;

    public WindowsCursorPositionProvider(ILogger<WindowsCursorPositionProvider> logger)
    {
        _logger = logger;
    }

    public bool TryGetCursorPosition(out CursorPosition position)
    {
        position = default;

        if (!OperatingSystem.IsWindowsVersionAtLeast(5, 0))
            return false;

        try
        {
            if (!PInvoke.GetCursorPos(out var point))
            {
                _logger.ZLogDebug($"获取鼠标位置失败: Win32Error={Marshal.GetLastPInvokeError()}");
                return false;
            }

            position = new CursorPosition(point.X, point.Y);
            return true;
        }
        catch (Exception ex)
        {
            _logger.ZLogDebug($"获取鼠标位置失败: {ex.Message}");
            return false;
        }
    }
}
