using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Microsoft.Extensions.Logging;
using ScreenCapture.NET;

namespace WordLens.Services.Implementations.Screenshot;

/// <summary>
///     Linux平台截图服务实现
/// </summary>
public class LinuxScreenshotService : IScreenshotService
{
    private readonly ILogger<LinuxScreenshotService> _logger;
    private readonly IScreenCapture? _screenCapture;
    private readonly IScreenCaptureService _screenCaptureService;

    public LinuxScreenshotService(ILogger<LinuxScreenshotService> logger, IScreenCaptureService screenCaptureService)
    {
        _logger = logger;
        _screenCaptureService = screenCaptureService;

        try
        {
            var graphicsCard = _screenCaptureService.GetGraphicsCards().FirstOrDefault();
            var display = _screenCaptureService.GetDisplays(graphicsCard).FirstOrDefault();
            _screenCapture = _screenCaptureService.GetScreenCapture(display);
            _logger.LogInformation("截图服务初始化成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化截图服务时发生严重错误");
            _screenCapture = null;
        }
    }

    public async Task<WriteableBitmap?> CaptureAreaAsync(Rect area)
    {
        // 如果初始化失败，直接返回 null
        if (_screenCapture == null)
        {
            _logger.LogError("截图失败，因为服务未能正确初始化");
            return null;
        }

        try
        {
            return await Task.Run(() =>
            {
                var scale = 1;
                var x = (int)(area.X * scale);
                var y = (int)(area.Y * scale);
                var width = (int)(area.Width * scale);
                var height = (int)(area.Height * scale);

                if (width <= 0 || height <= 0)
                {
                    _logger.LogWarning($"无效的截图区域: {area}");
                    return null;
                }

                var captureZone = _screenCapture.RegisterCaptureZone(x, y, width, height);

                _screenCapture.CaptureScreen();

                using (captureZone.Lock())
                {
                    // 3. 转换缓冲区
                    var bitmap = ConvertBufferToWriteableBitmap(captureZone.RawBuffer, width, height);
                    return bitmap;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "截图过程中发生错误");
            return null;
        }
    }

    public async Task<WriteableBitmap?> CaptureFullScreenAsync()
    {
        var bounds = GetVirtualScreenBounds();
        return await CaptureAreaAsync(bounds);
    }

    public Rect GetVirtualScreenBounds()
    {
        // TODO: 获取Linux屏幕边界
        // 可以通过X11的XRRGetScreenResources获取
        _logger.LogWarning("Linux屏幕边界获取尚未实现");
        return new Rect(0, 0, 1920, 1080); // 临时返回默认值
    }

    private WriteableBitmap? ConvertBufferToWriteableBitmap(ReadOnlySpan<byte> rawBuffer, int width, int height)
    {
        try
        {
            var writeableBitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            using (var buffer = writeableBitmap.Lock())
            {
                Marshal.Copy(rawBuffer.ToArray(), 0, buffer.Address, rawBuffer.Length);
            }

            return writeableBitmap;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从缓冲区转换位图失败");
            return null;
        }
    }
}