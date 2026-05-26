using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WordLens.Abstractions.Services;
using ZLogger;

namespace WordLens.Windows.Services.Implementations;

public sealed class WindowsStartupService : IStartupService
{
    private const string AppName = "WordLens";
    private const string WindowsRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly ILogger<WindowsStartupService> _logger;

    public WindowsStartupService(ILogger<WindowsStartupService> logger)
    {
        _logger = logger;
    }

    public bool IsSupported => OperatingSystem.IsWindows();

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(WindowsRunKeyPath, false);
            var value = key?.GetValue(AppName) as string;
            var exePath = Environment.ProcessPath;

            return !string.IsNullOrWhiteSpace(value) &&
                   !string.IsNullOrWhiteSpace(exePath) &&
                   value.Contains(exePath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.ZLogWarning(ex, $"读取 Windows 开机自启状态失败: {ex.Message}");
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(WindowsRunKeyPath, true);

            if (!enabled)
            {
                key.DeleteValue(AppName, false);
                return;
            }

            key.SetValue(AppName, $"\"{GetExecutablePath()}\" --autostart", RegistryValueKind.String);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"更新 Windows 开机自启状态失败: {ex.Message}");
            throw;
        }
    }

    private static string GetExecutablePath()
    {
        return Environment.ProcessPath ?? throw new InvalidOperationException("无法获取当前程序路径。");
    }
}
