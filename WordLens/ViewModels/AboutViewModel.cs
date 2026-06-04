using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WordLens.Services.Implementations;

namespace WordLens.ViewModels;

public partial class AboutViewModel : ViewModelBase
{
    private readonly UpdateService? _updateService;

    [ObservableProperty] private bool hasUpdateStatus;

    [ObservableProperty] private bool isCheckingForUpdates;

    [ObservableProperty] private bool isUpdateAvailable;

    [ObservableProperty] private string latestReleaseUrl = string.Empty;

    [ObservableProperty] private string updateStatus = string.Empty;

    public AboutViewModel()
    {
    }

    public AboutViewModel(UpdateService updateService)
    {
        _updateService = updateService;
    }

    public string AppName => "WordLens";

    public string Version => GetVersion();

    public string Description => "一个简洁高效的划词翻译工具";

    public string Copyright => $"© {DateTime.Now.Year} WordLens";

    public string License => "GNU GPL v3.0";

    public string LicenseText => @"WordLens is licensed under the GNU General Public License v3.0 only (GPL-3.0-only).

You can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, version 3 of the License.

See the LICENSE file in this repository for the full license text.";

    public string ThirdPartyLibraries => @"本软件使用了以下开源库：

• Avalonia UI - 跨平台 UI 框架 (MIT License)
• CommunityToolkit.Mvvm - MVVM 工具包 (MIT License)
• SharpHook - 全局热键支持 (MIT License)
• ZLogger - 高性能日志库 (MIT License)
• Semi.Avalonia - UI 主题库 (MIT License)";

    public string GitHubUrl => UpdateService.RepositoryUrl;

    private string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.0.0";
    }

    [RelayCommand]
    private void OpenGitHub()
    {
        OpenUrl(GitHubUrl);
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        if (_updateService == null)
        {
            HasUpdateStatus = true;
            IsUpdateAvailable = false;
            UpdateStatus = "当前环境无法检查更新。";
            return;
        }

        IsCheckingForUpdates = true;
        HasUpdateStatus = true;
        IsUpdateAvailable = false;
        LatestReleaseUrl = string.Empty;
        UpdateStatus = "正在检查更新...";

        try
        {
            var result = await _updateService.CheckForUpdatesAsync(cancellationToken);
            LatestReleaseUrl = result.ReleaseUrl;
            IsUpdateAvailable = result.IsUpdateAvailable;

            if (result.IsUpdateAvailable)
            {
                UpdateStatus = $"发现新版本 {result.LatestVersion}，当前版本 {result.CurrentVersion}。";
                return;
            }

            UpdateStatus = string.IsNullOrWhiteSpace(result.LatestVersion)
                ? "尚未找到已发布版本。"
                : $"当前已是最新版本（{result.CurrentVersion}）。";
        }
        catch (Exception ex)
        {
            IsUpdateAvailable = false;
            LatestReleaseUrl = string.Empty;
            UpdateStatus = $"检查更新失败：{ex.Message}";
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    [RelayCommand]
    private void OpenLatestRelease()
    {
        if (!string.IsNullOrWhiteSpace(LatestReleaseUrl))
            OpenUrl(LatestReleaseUrl);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // 静默失败
        }
    }
}
