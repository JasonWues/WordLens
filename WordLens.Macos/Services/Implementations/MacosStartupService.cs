using System.Security;
using Microsoft.Extensions.Logging;
using WordLens.Abstractions.Services;
using WordLens.Services;

namespace WordLens.Macos.Services.Implementations;

public sealed class MacosStartupService : IStartupService
{
    private const string MacLaunchAgentLabel = "com.crimsonninja.wordlens";

    private readonly ILogger<MacosStartupService> _logger;

    public MacosStartupService(ILogger<MacosStartupService> logger)
    {
        _logger = logger;
    }

    public bool IsSupported => OperatingSystem.IsMacOS();

    public bool IsEnabled()
    {
        if (!IsSupported)
            return false;

        try
        {
            return File.Exists(GetMacLaunchAgentPath());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取 macOS 开机自启状态失败");
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (!IsSupported)
            return;

        try
        {
            SetMacEnabled(enabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新 macOS 开机自启状态失败");
            throw;
        }
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

        var executablePath = GetExecutablePath();
        var escapedExecutablePath = SecurityElement.Escape(executablePath) ?? executablePath;
        File.WriteAllText(path, $$"""
                                  <?xml version="1.0" encoding="UTF-8"?>
                                  <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                                  <plist version="1.0">
                                  <dict>
                                      <key>Label</key>
                                      <string>{{MacLaunchAgentLabel}}</string>
                                      <key>ProgramArguments</key>
                                      <array>
                                          <string>{{escapedExecutablePath}}</string>
                                          <string>--autostart</string>
                                      </array>
                                      <key>RunAtLoad</key>
                                      <true/>
                                  </dict>
                                  </plist>
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
}
