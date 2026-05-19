using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using WordLens.Services;
using ZLogger;

namespace WordLens.ViewModels;

/// <summary>
///     屏幕捕获窗口的ViewModel
///     管理区域选择、截图和OCR流程
/// </summary>
public partial class ScreenCaptureViewModel : ViewModelBase
{
    private readonly ILogger<ScreenCaptureViewModel> _logger;
    private readonly IScreenshotService _screenshotService;
    private readonly IWindowManagerService _windowManager;

    /// <summary>
    ///     用于保存截图的临时目录
    /// </summary>
    private readonly string _tempScreenshotDir;

    private WriteableBitmap? _screenBackground;

    [ObservableProperty] private Point endPoint;

    [ObservableProperty] private bool isSelecting;

    [ObservableProperty] private Rect selectionRect;

    [ObservableProperty] private string sizeHint = "";

    [ObservableProperty] private Point startPoint;

    public Rect ScreenBackgroundBounds { get; private set; }

    public ScreenCaptureViewModel()
    {
        // 设计时构造函数
        _screenshotService = null!;
        _windowManager = null!;
        _logger = null!;
        _tempScreenshotDir = string.Empty;
    }

    public ScreenCaptureViewModel(
        IScreenshotService screenshotService,
        IWindowManagerService windowManager,
        ILogger<ScreenCaptureViewModel> logger)
    {
        _screenshotService = screenshotService;
        _windowManager = windowManager;
        _logger = logger;

        // 创建临时截图目录
        _tempScreenshotDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WordLens",
            "Screenshots"
        );
        Directory.CreateDirectory(_tempScreenshotDir);

        _logger.ZLogInformation($"屏幕捕获ViewModel初始化完成");
    }

    /// <summary>
    ///     开始选择区域
    /// </summary>
    public void BeginSelection(Point point)
    {
        StartPoint = point;
        EndPoint = point;
        IsSelecting = true;
        UpdateSelectionRect();
        _logger.ZLogDebug($"开始选择区域: {point}");
    }

    /// <summary>
    ///     更新选择区域
    /// </summary>
    public void UpdateSelection(Point point)
    {
        if (!IsSelecting) return;

        EndPoint = point;
        UpdateSelectionRect();
    }

    /// <summary>
    ///     完成选择
    /// </summary>
    public bool CompleteSelection(Point point)
    {
        if (!IsSelecting) return false;

        EndPoint = point;
        IsSelecting = false;
        UpdateSelectionRect();

        // 检查选区大小
        if (SelectionRect.Width < 10 || SelectionRect.Height < 10)
        {
            _logger.ZLogWarning($"选区过小，取消截图: {SelectionRect}");
            return false;
        }

        _logger.ZLogInformation($"完成区域选择: {SelectionRect}");
        return true;
    }

    /// <summary>
    ///     执行当前选区截图并打开 OCR 结果窗口
    /// </summary>
    public async Task CaptureSelectionAsync()
    {
        await CaptureAndProcessAsync();
    }

    public async Task<bool> PrepareCaptureAsync()
    {
        ResetSelection();
        ClearCapturedBackground();

        ScreenBackgroundBounds = _screenshotService.GetVirtualScreenBounds();
        _logger.ZLogInformation($"开始预捕获屏幕背景: {ScreenBackgroundBounds}");

        var bitmap = await _screenshotService.CaptureAreaAsync(ScreenBackgroundBounds);
        if (bitmap == null)
        {
            _logger.ZLogError($"预捕获屏幕背景失败");
            return false;
        }

        _screenBackground = bitmap;
        _logger.ZLogInformation($"预捕获屏幕背景成功: {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");
        return true;
    }

    /// <summary>
    ///     取消选择
    /// </summary>
    public void CancelSelection()
    {
        ResetSelection();
        _logger.ZLogInformation($"取消区域选择");
    }

    public void CancelCaptureSession()
    {
        ResetSelection();
        ClearCapturedBackground();
        _logger.ZLogInformation($"取消截图会话");
    }

    /// <summary>
    ///     更新选择矩形
    /// </summary>
    private void UpdateSelectionRect()
    {
        var x = Math.Min(StartPoint.X, EndPoint.X);
        var y = Math.Min(StartPoint.Y, EndPoint.Y);
        var width = Math.Abs(EndPoint.X - StartPoint.X);
        var height = Math.Abs(EndPoint.Y - StartPoint.Y);

        SelectionRect = new Rect(x, y, width, height);

        // 更新尺寸提示
        SizeHint = $"{(int)width} × {(int)height}";
    }

    /// <summary>
    ///     执行截图并处理
    /// </summary>
    private Task CaptureAndProcessAsync()
    {
        try
        {
            _logger.ZLogInformation($"开始截图: {SelectionRect}");

            var bitmap = CropSelectionFromCapturedBackground();

            if (bitmap == null)
            {
                _logger.ZLogError($"从预捕获背景裁剪截图失败");
                return Task.CompletedTask;
            }

            _logger.ZLogInformation($"截图成功: {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");

            // 保存截图到临时文件
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"screenshot_{timestamp}.png";
            var filepath = Path.Combine(_tempScreenshotDir, filename);

            SaveBitmap(bitmap, filepath);
            _logger.ZLogInformation($"截图已保存: {filepath}");

            _windowManager.ShowOcrResultWindow(bitmap);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"截图或保存过程中发生错误");
        }
        finally
        {
            ClearCapturedBackground();
        }

        return Task.CompletedTask;
    }

    private WriteableBitmap? CropSelectionFromCapturedBackground()
    {
        if (_screenBackground == null)
        {
            _logger.ZLogError($"缺少预捕获屏幕背景，无法裁剪选区");
            return null;
        }

        if (ScreenBackgroundBounds.Width <= 0 || ScreenBackgroundBounds.Height <= 0)
        {
            _logger.ZLogError($"预捕获屏幕边界无效: {ScreenBackgroundBounds}");
            return null;
        }

        var left = Math.Clamp(SelectionRect.X, 0, ScreenBackgroundBounds.Width);
        var top = Math.Clamp(SelectionRect.Y, 0, ScreenBackgroundBounds.Height);
        var right = Math.Clamp(SelectionRect.Right, 0, ScreenBackgroundBounds.Width);
        var bottom = Math.Clamp(SelectionRect.Bottom, 0, ScreenBackgroundBounds.Height);

        if (right <= left || bottom <= top)
        {
            _logger.ZLogError($"选区不在预捕获屏幕范围内: {SelectionRect}");
            return null;
        }

        return CropBitmap(_screenBackground, new Rect(left, top, right - left, bottom - top), ScreenBackgroundBounds);
    }

    private static unsafe WriteableBitmap? CropBitmap(
        WriteableBitmap source,
        Rect cropRect,
        Rect sourceBounds)
    {
        var sourceWidth = source.PixelSize.Width;
        var sourceHeight = source.PixelSize.Height;
        if (sourceWidth <= 0 || sourceHeight <= 0)
            return null;

        var scaleX = sourceWidth / sourceBounds.Width;
        var scaleY = sourceHeight / sourceBounds.Height;

        var sourceLeft = Math.Clamp((int)Math.Floor(cropRect.X * scaleX), 0, sourceWidth - 1);
        var sourceTop = Math.Clamp((int)Math.Floor(cropRect.Y * scaleY), 0, sourceHeight - 1);
        var sourceRight = Math.Clamp((int)Math.Ceiling(cropRect.Right * scaleX), sourceLeft + 1, sourceWidth);
        var sourceBottom = Math.Clamp((int)Math.Ceiling(cropRect.Bottom * scaleY), sourceTop + 1, sourceHeight);
        var targetWidth = sourceRight - sourceLeft;
        var targetHeight = sourceBottom - sourceTop;

        var target = new WriteableBitmap(
            new PixelSize(targetWidth, targetHeight),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using var sourceBuffer = source.Lock();
        using var targetBuffer = target.Lock();

        const int bytesPerPixel = 4;
        var bytesPerRow = targetWidth * bytesPerPixel;
        var sourceBase = (byte*)sourceBuffer.Address.ToPointer();
        var targetBase = (byte*)targetBuffer.Address.ToPointer();

        for (var row = 0; row < targetHeight; row++)
        {
            var sourceRow = sourceBase + (sourceTop + row) * sourceBuffer.RowBytes + sourceLeft * bytesPerPixel;
            var targetRow = targetBase + row * targetBuffer.RowBytes;
            Buffer.MemoryCopy(sourceRow, targetRow, targetBuffer.RowBytes, bytesPerRow);
        }

        return target;
    }

    private void ResetSelection()
    {
        IsSelecting = false;
        StartPoint = new Point();
        EndPoint = new Point();
        SelectionRect = new Rect();
        SizeHint = string.Empty;
    }

    private void ClearCapturedBackground()
    {
        _screenBackground?.Dispose();
        _screenBackground = null;
        ScreenBackgroundBounds = new Rect();
    }

    /// <summary>
    ///     保存位图到文件
    /// </summary>
    private void SaveBitmap(WriteableBitmap bitmap, string filepath)
    {
        try
        {
            using var fileStream = File.Create(filepath);
            bitmap.Save(fileStream);
            _logger.ZLogInformation($"位图已保存到: {filepath}");
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"保存位图失败: {filepath}");
            throw;
        }
    }

    /// <summary>
    ///     获取虚拟屏幕边界（用于多显示器）
    /// </summary>
    public Rect GetVirtualScreenBounds()
    {
        return _screenshotService.GetVirtualScreenBounds();
    }
}
