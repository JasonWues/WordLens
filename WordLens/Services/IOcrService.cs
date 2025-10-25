using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace WordLens.Services;

/// <summary>
///     OCR文字识别服务接口（预留，待实现）
/// </summary>
public interface IOcrService
{
    /// <summary>
    ///     从图片中识别文字
    /// </summary>
    /// <param name="bitmap">要识别的图片</param>
    /// <param name="languageCode">识别语言代码（如"zh-CN", "en-US"等）</param>
    /// <returns>识别出的文字，如果识别失败返回null</returns>
    Task<string?> RecognizeTextAsync(WriteableBitmap bitmap, string languageCode = "zh-CN");

    /// <summary>
    ///     检查OCR服务是否可用
    /// </summary>
    /// <returns>true表示可用</returns>
    Task<bool> IsAvailableAsync();

    /// <summary>
    ///     获取支持的语言列表
    /// </summary>
    /// <returns>语言代码列表</returns>
    Task<string[]> GetSupportedLanguagesAsync();
}