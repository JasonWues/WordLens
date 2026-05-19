using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WordLens.Services;
using ZLogger;

namespace WordLens.Services.Implementations;

public sealed class StartupService : IStartupService
{
    private const string AppName = "WordLens";
    private const string MacLaunchAgentLabel = "com.crimsonninja.wordlens";
    private const string WindowsRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly ILogger<StartupService> _logger;

    public StartupService(ILogger<StartupService> logger)
    {
        _logger = logger;
    }

    public bool IsSupported =>
        OperatingSystem.IsWindows() ||
        OperatingSystem.IsMacOS() ||
        OperatingSystem.IsLinux();

    public bool IsEnabled()
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return IsWindowsEnabled();

            if (OperatingSystem.IsMacOS())
                return File.Exists(GetMacLaunchAgentPath());

            if (OperatingSystem.IsLinux())
                return File.Exists(GetLinuxDesktopEntryPath());
        }
        catch (Exception ex)
        {
            _logger.ZLogWarning(ex, $"读取开机自启状态失败: {ex.Message}");
        }

        return false;
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                SetWindowsEnabled(enabled);
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                SetMacEnabled(enabled);
                return;
            }

            if (OperatingSystem.IsLinux())
            {
                SetLinuxEnabled(enabled);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"更新开机自启状态失败: {ex.Message}");
            throw;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool IsWindowsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(WindowsRunKeyPath, false);
        var value = key?.GetValue(AppName) as string;
        var exePath = Environment.ProcessPath;

        return !string.IsNullOrWhiteSpace(value) &&
               !string.IsNullOrWhiteSpace(exePath) &&
               value.Contains(exePath, StringComparison.OrdinalIgnoreCase);
    }

    [SupportedOSPlatform("windows")]
    private static void SetWindowsEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(WindowsRunKeyPath, true);

        if (!enabled)
        {
            key.DeleteValue(AppName, false);
            return;
        }

        key.SetValue(AppName, $"\"{GetExecutablePath()}\" --autostart", RegistryValueKind.String);
    }

    private static void SetMacEnabled(bool enabled)
    {
        var path = GetMacLaunchAgentPath();

        if (!enabled)
        {
            File.Delete(path);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var exePath = SecurityElement.Escape(GetExecutablePath()) ?? GetExecutablePath();
        File.WriteAllText(path, $$"""
                                  <?xml version="1.0" encoding="UTF-8"?>
                                  <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                                  <plist version="1.0">
                                  <dict>
                                      <key>Label</key>
                                      <string>{{MacLaunchAgentLabel}}</string>
                                      <key>ProgramArguments</key>
                                      <array>
                                          <string>{{exePath}}</string>
                                          <string>--autostart</string>
                                      </array>
                                      <key>RunAtLoad</key>
                                      <true/>
                                  </dict>
                                  </plist>
                                  """);
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

    private static string GetMacLaunchAgentPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library",
            "LaunchAgents",
            $"{MacLaunchAgentLabel}.plist");
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
