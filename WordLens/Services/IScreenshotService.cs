using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;

namespace WordLens.Services
{
    /// <summary>
    /// 跨平台截图服务接口
    /// </summary>
    public interface IScreenshotService
    {
        /// <summary>
        /// 捕获屏幕指定区域
        /// </summary>
        /// <param name="area">要捕获的矩形区域（屏幕坐标）</param>
        /// <returns>捕获的图像，如果失败返回null</returns>
        Task<WriteableBitmap?> CaptureAreaAsync(Rect area);

        /// <summary>
        /// 捕获整个屏幕
        /// </summary>
        /// <returns>捕获的图像，如果失败返回null</returns>
        Task<WriteableBitmap?> CaptureFullScreenAsync();

        /// <summary>
        /// 获取所有屏幕的边界（用于多显示器场景）
        /// </summary>
        /// <returns>包含所有屏幕的虚拟边界</returns>
        Rect GetVirtualScreenBounds();
    }
}