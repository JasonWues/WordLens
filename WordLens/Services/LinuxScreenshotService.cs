using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;

namespace WordLens.Services
{
    /// <summary>
    /// Linux平台截图服务实现（预留）
    /// TODO: 实现X11/Wayland截图功能
    /// </summary>
    public class LinuxScreenshotService : IScreenshotService
    {
        private readonly ILogger<LinuxScreenshotService> _logger;

        public LinuxScreenshotService(ILogger<LinuxScreenshotService> logger)
        {
            _logger = logger;
        }

        public Task<WriteableBitmap?> CaptureAreaAsync(Rect area)
        {
            _logger.LogWarning("Linux截图功能尚未实现");
            // TODO: 实现X11或Wayland截图
            // 可以使用：
            // 1. X11: libX11, XGetImage
            // 2. Wayland: wlroots截图协议
            // 3. 或调用系统命令: scrot, gnome-screenshot
            return Task.FromResult<WriteableBitmap?>(null);
        }

        public Task<WriteableBitmap?> CaptureFullScreenAsync()
        {
            _logger.LogWarning("Linux截图功能尚未实现");
            return Task.FromResult<WriteableBitmap?>(null);
        }

        public Rect GetVirtualScreenBounds()
        {
            // TODO: 获取Linux屏幕边界
            // 可以通过X11的XRRGetScreenResources获取
            _logger.LogWarning("Linux屏幕边界获取尚未实现");
            return new Rect(0, 0, 1920, 1080); // 临时返回默认值
        }
    }
}