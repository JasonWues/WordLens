using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using WordLens.Abstractions.Services;
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
    private readonly ICursorPositionProvider _cursorPositionProvider;

#if DEBUG
    /// <summary>
    ///     Debug 构建用于保存 OCR 截图的临时目录
    /// </summary>
    private readonly string _tempScreenshotDir;
#endif

    private Point? _captureStartPoint;
    private Point? _captureEndPoint;
    private Point _captureOrigin;
    private double _captureScale = 1.0;

    [ObservableProperty] private Point endPoint;

    [ObservableProperty] private bool isSelecting;

    [ObservableProperty] private Rect selectionRect;

    [ObservableProperty] private string sizeHint = "";

    [ObservableProperty] private Point startPoint;

    public ScreenCaptureViewModel()
    {
        // 设计时构造函数
        _screenshotService = null!;
        _windowManager = null!;
        _cursorPositionProvider = null!;
        _logger = null!;
#if DEBUG
        _tempScreenshotDir = string.Empty;
#endif
    }

    public ScreenCaptureViewModel(
        IScreenshotService screenshotService,
        IWindowManagerService windowManager,
        ICursorPositionProvider cursorPositionProvider,
        ILogger<ScreenCaptureViewModel> logger)
    {
        _screenshotService = screenshotService;
        _windowManager = windowManager;
        _cursorPositionProvider = cursorPositionProvider;
        _logger = logger;

#if DEBUG
        // Debug 构建保留 OCR 截图，便于排查识别效果；Release 不落盘。
        _tempScreenshotDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WordLens",
            "Screenshots"
        );
        Directory.CreateDirectory(_tempScreenshotDir);
#endif

        _logger.ZLogInformation($"屏幕捕获ViewModel初始化完成");
    }

    /// <summary>
    ///     开始选择区域
    /// </summary>
    public void BeginSelection(Point point)
    {
        StartPoint = point;
        EndPoint = point;
        _captureStartPoint = ToCapturePoint(point);
        _captureEndPoint = _captureStartPoint;
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
        _captureEndPoint = ToCapturePoint(point);
        UpdateSelectionRect();
    }

    /// <summary>
    ///     完成选择
    /// </summary>
    public bool CompleteSelection(Point point)
    {
        if (!IsSelecting) return false;

        EndPoint = point;
        _captureEndPoint = ToCapturePoint(point);
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

    public Task<bool> PrepareCaptureAsync(Rect overlayBounds, double overlayScale)
    {
        ResetSelection();
        _captureOrigin = new Point(overlayBounds.X, overlayBounds.Y);
        _captureScale = double.IsFinite(overlayScale) && overlayScale > 0 ? overlayScale : 1.0;

        _logger.ZLogInformation($"截图遮罩准备完成: Bounds={overlayBounds}, Scale={_captureScale}");
        return Task.FromResult(true);
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
    private async Task CaptureAndProcessAsync()
    {
        try
        {
            var captureRect = BuildCaptureRect();
            if (captureRect.Width <= 0 || captureRect.Height <= 0)
            {
                _logger.ZLogError($"截图区域无效: {captureRect}");
                return;
            }

            _logger.ZLogInformation($"开始截图: Visual={SelectionRect}, Capture={captureRect}");

            var bitmap = await _screenshotService.CaptureAreaAsync(captureRect);

            if (bitmap == null)
            {
                _logger.ZLogError($"截图失败: {captureRect}");
                return;
            }

            _logger.ZLogInformation($"截图成功: {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");

#if DEBUG
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"screenshot_{timestamp}.png";
            var filepath = Path.Combine(_tempScreenshotDir, filename);

            SaveBitmap(bitmap, filepath);
            _logger.ZLogInformation($"截图已保存: {filepath}");
#endif

            _windowManager.ShowOcrResultWindow(bitmap);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"截图处理过程中发生错误");
        }
    }

    private Rect BuildCaptureRect()
    {
        var start = _captureStartPoint ?? ToCapturePoint(StartPoint);
        var end = _captureEndPoint ?? ToCapturePoint(EndPoint);
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var right = Math.Max(start.X, end.X);
        var bottom = Math.Max(start.Y, end.Y);

        return new Rect(left, top, right - left, bottom - top);
    }

    private Point ToCapturePoint(Point point)
    {
        if (OperatingSystem.IsWindows() &&
            _cursorPositionProvider.TryGetCursorPosition(out var cursorPosition))
        {
            return new Point(cursorPosition.X, cursorPosition.Y);
        }

        return new Point(
            _captureOrigin.X + point.X * _captureScale,
            _captureOrigin.Y + point.Y * _captureScale);
    }

    private void ResetSelection()
    {
        IsSelecting = false;
        StartPoint = new Point();
        EndPoint = new Point();
        _captureStartPoint = null;
        _captureEndPoint = null;
        SelectionRect = new Rect();
        SizeHint = string.Empty;
    }

#if DEBUG
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
#endif

}
