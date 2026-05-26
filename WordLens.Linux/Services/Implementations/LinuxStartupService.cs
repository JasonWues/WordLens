using Microsoft.Extensions.Logging;
using WordLens.Abstractions.Services;

namespace WordLens.Linux.Services.Implementations;

public sealed class LinuxStartupService : IStartupService
{
    private readonly ILogger<LinuxStartupService> _logger;

    public LinuxStartupService(ILogger<LinuxStartupService> logger)
    {
        _logger = logger;
    }

    public bool IsSupported => OperatingSystem.IsLinux();

    public bool IsEnabled()
    {
        if (!IsSupported)
            return false;

        try
        {
            return File.Exists(GetLinuxDesktopEntryPath());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取 Linux 开机自启状态失败");
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (!IsSupported)
            return;

        try
        {
            SetLinuxEnabled(enabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新 Linux 开机自启状态失败");
            throw;
        }
    }

    private static void SetLinuxEnabled(bool enabled)
    {
        var path = GetLinuxDesktopEntryPath();

        if (!enabled)
        {
            File.Delete(path);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        File.WriteAllText(path, $$"""
                                  [Desktop Entry]
                                  Type=Application
                                  Name=WordLens
                                  Exec={{EscapeDesktopExecPath(GetExecutablePath())}} --autostart
                                  Terminal=false
                                  X-GNOME-Autostart-enabled=true
                                  """);
    }

    private static string GetExecutablePath()
    {
        return Environment.ProcessPath ?? throw new InvalidOperationException("无法获取当前程序路径。");
    }

    private static string GetLinuxDesktopEntryPath()
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");

        if (string.IsNullOrWhiteSpace(configHome))
            configHome = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config");

        return Path.Combine(configHome, "autostart", "wordlens.desktop");
    }

    private static string EscapeDesktopExecPath(string path)
    {
        return $"\"{path.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }
}
