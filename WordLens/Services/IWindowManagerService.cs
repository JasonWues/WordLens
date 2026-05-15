using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

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
    Task ShowTranslationWindowAsync(string selectedText);

    /// <summary>
    /// 显示或激活设置窗口
    /// 如果窗口已存在，则激活并显示；否则创建新窗口
    /// </summary>
    Task ShowSettingsWindowAsync();

    /// <summary>
    /// 显示或激活截图窗口
    /// 如果窗口已存在，则激活并显示；否则创建新窗口
    /// </summary>
    void ShowScreenCaptureWindow();

    /// <summary>
    /// 显示或激活 OCR 结果窗口
    /// </summary>
    /// <param name="screenshot">已截取的图片</param>
    /// <param name="recognizedText">可选的初始识别文本</param>
    void ShowOcrResultWindow(WriteableBitmap screenshot, string? recognizedText = null);

    /// <summary>
    /// 显示或激活历史记录窗口
    /// 如果窗口已存在，则激活并显示；否则创建新窗口
    /// </summary>
    void ShowHistoryWindow();

    /// <summary>
    /// 关闭所有窗口（应用程序退出时调用）
    /// </summary>
    void CloseAllWindows();
}
