using System;
using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.Input;

namespace WordLens.ViewModels;

public partial class AboutViewModel : ViewModelBase
{
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

    public string GitHubUrl => "https://github.com/yourusername/WordLens";

    private string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.0.0";
    }

    [RelayCommand]
    private void OpenGitHub()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = GitHubUrl,
                UseShellExecute = true
            });
        }
        catch
        {
            // 静默失败
        }
    }
}