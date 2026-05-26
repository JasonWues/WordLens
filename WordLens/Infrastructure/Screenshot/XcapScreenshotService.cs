using System;
using System.Runtime.InteropServices;
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
public partial class XcapScreenshotService : IScreenshotService
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
                var scale = GetCaptureScale();
                var x = (int)Math.Round(area.X * scale);
                var y = (int)Math.Round(area.Y * scale);
                var width = (int)Math.Round(area.Width * scale);
                var height = (int)Math.Round(area.Height * scale);

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
            var scale = GetCaptureScale();
            return new Rect(
                bounds.X / scale,
                bounds.Y / scale,
                bounds.Width / scale,
                bounds.Height / scale);
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

    private static double GetCaptureScale()
    {
        if (!OperatingSystem.IsWindows())
        {
            return 1.0;
        }

        try
        {
            var hdc = GetDC(IntPtr.Zero);
            var dpi = GetDeviceCaps(hdc, LOGPIXELSX);
            ReleaseDC(IntPtr.Zero, hdc);
            return dpi / 96.0;
        }
        catch
        {
            return 1.0;
        }
    }

    private const int LOGPIXELSX = 88;

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetDC(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [LibraryImport("gdi32.dll")]
    private static partial int GetDeviceCaps(IntPtr hdc, int nIndex);
}
