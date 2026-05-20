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

    public string License => "MIT License";

    public string LicenseText => @"MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the ""Software""), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.";

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
