using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Microsoft.Extensions.Logging;
using WordLens.Native;
using WordLens.Services;

namespace WordLens.Infrastructure.Screenshot;

/// <summary>
///     基于 Rust xcap 的跨平台截图服务实现
/// </summary>
public class XcapScreenshotService : IScreenshotService
{
    private readonly ILogger<XcapScreenshotService> _logger;

    public XcapScreenshotService(ILogger<XcapScreenshotService> logger)
    {
        _logger = logger;
    }

    public async Task<WriteableBitmap?> CaptureAreaAsync(Rect area)
    {
        try
        {
            return await Task.Run(() =>
            {
                var x = (int)Math.Floor(area.X);
                var y = (int)Math.Floor(area.Y);
                var right = (int)Math.Ceiling(area.Right);
                var bottom = (int)Math.Ceiling(area.Bottom);
                var width = right - x;
                var height = bottom - y;

                if (width <= 0 || height <= 0)
                {
                    _logger.LogWarning("无效的截图区域: {Area}", area);
                    return null;
                }

                var screenshot = ScreenshotNative.CaptureRegion(x, y, width, height);
                return ConvertBufferToWriteableBitmap(screenshot);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "xcap截图过程中发生错误");
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
        try
        {
            var bounds = ScreenshotNative.GetVirtualScreenBounds();
            return new Rect(
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取xcap虚拟屏幕边界失败");
            return new Rect(0, 0, 1920, 1080);
        }
    }

    private unsafe WriteableBitmap? ConvertBufferToWriteableBitmap(ScreenshotNative.ScreenshotData screenshot)
    {
        try
        {
            var writeableBitmap = new WriteableBitmap(
                new PixelSize(screenshot.Width, screenshot.Height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            using (var framebuffer = writeableBitmap.Lock())
            {
                fixed (byte* source = screenshot.Buffer)
                {
                    var bytesPerRow = screenshot.Width * 4;
                    var sourceStride = screenshot.Stride;
                    var destinationStride = framebuffer.RowBytes;
                    var destination = (byte*)framebuffer.Address.ToPointer();

                    for (var row = 0; row < screenshot.Height; row++)
                    {
                        Buffer.MemoryCopy(
                            source + row * sourceStride,
                            destination + row * destinationStride,
                            destinationStride,
                            bytesPerRow);
                    }
                }
            }

            return writeableBitmap;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从xcap缓冲区转换位图失败");
            return null;
        }
    }
}
