using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Microsoft.Extensions.Logging;

namespace WordLens.Services
{
    /// <summary>
    /// Windows平台截图服务实现
    /// 使用GDI32 API进行屏幕捕获
    /// </summary>
    public class WindowsScreenshotService : IScreenshotService
    {
        private readonly ILogger<WindowsScreenshotService> _logger;

        public WindowsScreenshotService(ILogger<WindowsScreenshotService> logger)
        {
            _logger = logger;
        }

        public async Task<WriteableBitmap?> CaptureAreaAsync(Rect area)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 获取DPI缩放比例
                    var scale = GetDpiScale();
                    
                    // 应用DPI缩放
                    int x = (int)(area.X * scale);
                    int y = (int)(area.Y * scale);
                    int width = (int)(area.Width * scale);
                    int height = (int)(area.Height * scale);

                    if (width <= 0 || height <= 0)
                    {
                        _logger.LogWarning($"无效的截图区域: {area}");
                        return null;
                    }

                    _logger.LogInformation($"开始截图: 区域=({x}, {y}, {width}, {height}), DPI缩放={scale}");

                    // 获取屏幕DC
                    IntPtr screenDc = GetDC(IntPtr.Zero);
                    IntPtr memDc = CreateCompatibleDC(screenDc);
                    IntPtr hBitmap = CreateCompatibleBitmap(screenDc, width, height);
                    IntPtr oldBitmap = SelectObject(memDc, hBitmap);

                    // 执行位块传输（截图）
                    // 使用SRCCOPY | CAPTUREBLT以支持分层窗口和更好的色彩捕获
                    bool success = BitBlt(memDc, 0, 0, width, height, screenDc, x, y, SRCCOPY | CAPTUREBLT);
                    
                    if (!success)
                    {
                        _logger.LogWarning("标准BitBlt失败，尝试使用PrintWindow");
                    }

                    if (!success)
                    {
                        _logger.LogError("BitBlt截图失败");
                        return null;
                    }

                    // 创建Avalonia WriteableBitmap
                    var bitmap = ConvertToWriteableBitmap(hBitmap, width, height);

                    // 清理资源
                    SelectObject(memDc, oldBitmap);
                    DeleteObject(hBitmap);
                    DeleteDC(memDc);
                    ReleaseDC(IntPtr.Zero, screenDc);

                    _logger.LogInformation("截图成功完成");
                    return bitmap;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "截图过程中发生错误");
                    return null;
                }
            });
        }

        public async Task<WriteableBitmap?> CaptureFullScreenAsync()
        {
            var bounds = GetVirtualScreenBounds();
            return await CaptureAreaAsync(bounds);
        }

        public Rect GetVirtualScreenBounds()
        {
            int x = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int y = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            // 考虑DPI缩放
            var scale = GetDpiScale();
            return new Rect(x / scale, y / scale, width / scale, height / scale);
        }

        /// <summary>
        /// 将Windows HBITMAP转换为Avalonia WriteableBitmap
        /// </summary>
        private WriteableBitmap? ConvertToWriteableBitmap(IntPtr hBitmap, int width, int height)
        {
            try
            {
                // 获取位图信息
                BITMAP bmp = new BITMAP();
                GetObject(hBitmap, Marshal.SizeOf(bmp), ref bmp);

                // 创建WriteableBitmap
                var writeableBitmap = new WriteableBitmap(
                    new PixelSize(width, height),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Premul);

                using (var buffer = writeableBitmap.Lock())
                {
                    // 获取位图数据
                    BITMAPINFO bmi = new BITMAPINFO();
                    bmi.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                    bmi.biWidth = width;
                    bmi.biHeight = -height; // 负值表示从上到下
                    bmi.biPlanes = 1;
                    bmi.biBitCount = 32;
                    bmi.biCompression = BI_RGB;

                    IntPtr screenDc = GetDC(IntPtr.Zero);
                    
                    // 直接将位图数据复制到WriteableBitmap
                    GetDIBits(screenDc, hBitmap, 0, (uint)height, buffer.Address, ref bmi, DIB_RGB_COLORS);
                    
                    ReleaseDC(IntPtr.Zero, screenDc);
                    
                    // 检查是否需要HDR到SDR的色彩空间转换
                    if (IsHDRDisplay())
                    {
                        _logger.LogInformation("检测到HDR显示器，应用色彩校正");
                        ApplyHDRtoSDRConversion(buffer.Address, width, height);
                    }
                }

                return writeableBitmap;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "转换位图失败");
                return null;
            }
        }

        /// <summary>
        /// 应用HDR到SDR的色彩空间转换
        /// 修复HDR环境下截图发灰的问题
        /// </summary>
        private unsafe void ApplyHDRtoSDRConversion(IntPtr buffer, int width, int height)
        {
            try
            {
                byte* ptr = (byte*)buffer.ToPointer();
                int pixelCount = width * height;

                for (int i = 0; i < pixelCount; i++)
                {
                    int offset = i * 4;
                    
                    // 读取BGRA值
                    byte b = ptr[offset];
                    byte g = ptr[offset + 1];
                    byte r = ptr[offset + 2];
                    byte a = ptr[offset + 3];

                    // 转换到线性空间 (简化的sRGB到线性转换)
                    double rLinear = GammaToLinear(r / 255.0);
                    double gLinear = GammaToLinear(g / 255.0);
                    double bLinear = GammaToLinear(b / 255.0);

                    // 应用简单的tone mapping (Reinhard)
                    // 这有助于恢复HDR内容在SDR显示时丢失的亮度
                    rLinear = ToneMapReinhard(rLinear);
                    gLinear = ToneMapReinhard(gLinear);
                    bLinear = ToneMapReinhard(bLinear);

                    // 转换回sRGB伽马空间
                    r = (byte)(LinearToGamma(rLinear) * 255.0);
                    g = (byte)(LinearToGamma(gLinear) * 255.0);
                    b = (byte)(LinearToGamma(bLinear) * 255.0);

                    // 写回修正后的值
                    ptr[offset] = b;
                    ptr[offset + 1] = g;
                    ptr[offset + 2] = r;
                    ptr[offset + 3] = a;
                }

                _logger.LogInformation("HDR色彩校正完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HDR色彩转换失败");
            }
        }

        /// <summary>
        /// sRGB伽马到线性空间转换
        /// </summary>
        private double GammaToLinear(double value)
        {
            if (value <= 0.04045)
                return value / 12.92;
            return Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        /// <summary>
        /// 线性空间到sRGB伽马转换
        /// </summary>
        private double LinearToGamma(double value)
        {
            if (value <= 0.0031308)
                return value * 12.92;
            return 1.055 * Math.Pow(value, 1.0 / 2.4) - 0.055;
        }

        /// <summary>
        /// Reinhard tone mapping算法
        /// 将HDR亮度映射到SDR范围
        /// </summary>
        private double ToneMapReinhard(double value)
        {
            // 简化的Reinhard tone mapping
            // L_out = L_in / (1 + L_in)
            // 这有助于保持细节同时压缩高亮度
            const double exposure = 1.5; // 可调整曝光度
            double exposed = value * exposure;
            return exposed / (1.0 + exposed);
        }

        /// <summary>
        /// 获取系统DPI缩放比例
        /// </summary>
        private double GetDpiScale()
        {
            try
            {
                IntPtr hdc = GetDC(IntPtr.Zero);
                int dpi = GetDeviceCaps(hdc, LOGPIXELSX);
                ReleaseDC(IntPtr.Zero, hdc);
                return dpi / 96.0; // 96 DPI是标准DPI
            }
            catch
            {
                return 1.0; // 默认不缩放
            }
        }

        /// <summary>
        /// 检测是否为HDR显示器
        /// </summary>
        private bool IsHDRDisplay()
        {
            try
            {
                // 检查是否启用了HDR
                IntPtr hdc = GetDC(IntPtr.Zero);
                int colorDepth = GetDeviceCaps(hdc, BITSPIXEL);
                ReleaseDC(IntPtr.Zero, hdc);
                
                // 如果色深大于24位，可能是HDR
                if (colorDepth > 24)
                {
                    _logger.LogInformation($"检测到高色深显示器: {colorDepth}位");
                    return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        #region Windows API 声明

        private const int SRCCOPY = 0x00CC0020;
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;
        private const int LOGPIXELSX = 88;
        private const int BITSPIXEL = 12;
        private const int BI_RGB = 0;
        private const int DIB_RGB_COLORS = 0;
        private const int CAPTUREBLT = 0x40000000;

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
            IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("gdi32.dll")]
        private static extern int GetObject(IntPtr hObject, int nCount, ref BITMAP lpObject);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
            IntPtr lpvBits, ref BITMAPINFO lpbi, uint uUsage);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public uint[] cols;
        }

        #endregion
    }
}