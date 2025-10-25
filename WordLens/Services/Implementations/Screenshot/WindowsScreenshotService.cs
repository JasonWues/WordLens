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
///     Windows平台截图服务实现
/// </summary>
public class WindowsScreenshotService : IScreenshotService
{
    private readonly ILogger<WindowsScreenshotService> _logger;
    private readonly IScreenCapture? _screenCapture;
    private readonly IScreenCaptureService _screenCaptureService;

    public WindowsScreenshotService(ILogger<WindowsScreenshotService> logger,
        IScreenCaptureService screenCaptureService)
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
                var scale = GetDpiScale();
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
        var x = GetSystemMetrics(SM_XVIRTUALSCREEN);
        var y = GetSystemMetrics(SM_YVIRTUALSCREEN);
        var width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        var height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        var scale = GetDpiScale();
        return new Rect(x / scale, y / scale, width / scale, height / scale);
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


    #region 系统信息与 DPI (无需修改)

    private double GetDpiScale()
    {
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

    #endregion

    #region Windows API 声明

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;
    private const int LOGPIXELSX = 88;

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    #endregion
}