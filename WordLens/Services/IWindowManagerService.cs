using System;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace WordLens.Services;

/// <summary>
/// 窗口管理器服务接口
/// 负责管理应用程序中所有窗口的生命周期，确保每种窗口类型只有一个实例
/// </summary>
public interface IWindowManagerService
{
    /// <summary>
    /// 显示或激活翻译窗口
    /// 如果窗口已存在，则激活并显示；否则创建新窗口
    /// </summary>
    /// <param name="selectedText">要翻译的文本</param>
    /// <returns>翻译窗口实例</returns>
    Task<Window> ShowTranslationWindowAsync(string selectedText);

    /// <summary>
    /// 显示或激活设置窗口
    /// 如果窗口已存在，则激活并显示；否则创建新窗口
    /// </summary>
    /// <returns>设置窗口实例</returns>
    Task<Window> ShowSettingsWindowAsync();

    /// <summary>
    /// 显示或激活截图窗口
    /// 如果窗口已存在，则激活并显示；否则创建新窗口
    /// </summary>
    /// <returns>截图窗口实例</returns>
    Window ShowScreenCaptureWindow();

    /// <summary>
    /// 显示或激活历史记录窗口
    /// 如果窗口已存在，则激活并显示；否则创建新窗口
    /// </summary>
    /// <returns>历史记录窗口实例</returns>
    Window ShowHistoryWindow();

    /// <summary>
    /// 关闭所有窗口（应用程序退出时调用）
    /// </summary>
    void CloseAllWindows();
}