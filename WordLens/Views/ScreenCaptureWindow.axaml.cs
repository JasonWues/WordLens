using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using WordLens.ViewModels;

namespace WordLens.Views;

/// <summary>
///     屏幕捕获窗口
///     提供全屏遮罩和区域选择功能
/// </summary>
public partial class ScreenCaptureWindow : Window
{
    private bool _allowClose;
    private IPointer? _capturedPointer;
    private Canvas? _captureCanvas;
    private Line? _horizontalGuideLine;
    private bool _ignorePointerCaptureLost;
    private Border? _sizeHintBorder;
    private Line? _verticalGuideLine;

    public ScreenCaptureWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // 获取控件引用
        _captureCanvas = this.FindControl<Canvas>("CaptureCanvas");
        _verticalGuideLine = this.FindControl<Line>("VerticalGuideLine");
        _horizontalGuideLine = this.FindControl<Line>("HorizontalGuideLine");
        _sizeHintBorder = this.FindControl<Border>("SizeHintBorder");

        // 拦截窗口关闭事件
        Closing += OnWindowClosing;

        // 窗口加载完成后设置焦点
        Opened += (_, _) => _captureCanvas?.Focus();
    }

    public (Rect Bounds, double Scale) GetCaptureOverlayGeometry()
    {
        var screens = Screens.All;
        if (screens.Count == 0)
            return (new Rect(0, 0, 1920, 1080), 1.0);

        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;

        foreach (var screen in screens)
        {
            var bounds = screen.Bounds;
            minX = Math.Min(minX, bounds.X);
            minY = Math.Min(minY, bounds.Y);
            maxX = Math.Max(maxX, bounds.X + bounds.Width);
            maxY = Math.Max(maxY, bounds.Y + bounds.Height);
        }

        var anchorPoint = new PixelPoint(minX + 1, minY + 1);
        var anchorScreen = Screens.ScreenFromPoint(anchorPoint) ?? Screens.Primary ?? screens[0];
        var scale = double.IsFinite(anchorScreen.Scaling) && anchorScreen.Scaling > 0
            ? anchorScreen.Scaling
            : 1.0;

        return (new Rect(minX, minY, (maxX - minX) / scale, (maxY - minY) / scale), scale);
    }

    public void PrepareForCapture(Rect bounds)
    {
        WindowState = WindowState.Normal;
        Position = new PixelPoint((int)Math.Floor(bounds.X), (int)Math.Floor(bounds.Y));
        Width = bounds.Width;
        Height = bounds.Height;

        ReleasePointerCapture();
        HideGuideLines();
        if (DataContext is ScreenCaptureViewModel vm)
            vm.CancelSelection();

        _captureCanvas?.Focus();
    }

    public void ForceClose()
    {
        _allowClose = true;
        ReleasePointerCapture();
        HideGuideLines();
        if (DataContext is ScreenCaptureViewModel vm)
            vm.CancelCaptureSession();
        Close();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
            return;

        // 取消关闭操作
        e.Cancel = true;

        // 隐藏窗口而不是关闭
        CancelAndHide();
    }

    /// <summary>
    ///     鼠标按下事件 - 开始选择区域
    /// </summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_captureCanvas == null || DataContext is not ScreenCaptureViewModel vm)
            return;

        if (!e.GetCurrentPoint(_captureCanvas).Properties.IsLeftButtonPressed)
            return;

        var point = e.GetPosition(_captureCanvas);
        _capturedPointer = e.Pointer;
        _capturedPointer.Capture(_captureCanvas);
        UpdateGuideLines(point);
        vm.BeginSelection(point);
        e.Handled = true;
    }

    /// <summary>
    ///     鼠标移动事件 - 更新选择区域
    /// </summary>
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_captureCanvas == null || DataContext is not ScreenCaptureViewModel vm)
            return;

        var point = e.GetPosition(_captureCanvas);
        UpdateGuideLines(point);
        vm.UpdateSelection(point);

        // 更新尺寸提示位置（跟随鼠标）
        if (vm.IsSelecting && _sizeHintBorder != null)
        {
            Canvas.SetLeft(_sizeHintBorder, point.X + 15);
            Canvas.SetTop(_sizeHintBorder, point.Y + 15);
        }

        e.Handled = true;
    }

    /// <summary>
    ///     鼠标释放事件 - 完成选择并截图
    /// </summary>
    private async void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_captureCanvas == null || DataContext is not ScreenCaptureViewModel vm)
            return;

        var point = e.GetPosition(_captureCanvas);
        ReleasePointerCapture();
        var success = vm.CompleteSelection(point);

        // 如果截图成功，隐藏窗口
        if (success)
        {
            // 先隐藏遮罩，避免 OCR 截图包含半透明遮罩和选区边框。
            HideGuideLines();
            Hide();
            await Task.Delay(75);
            await vm.CaptureSelectionAsync();
        }
        else
        {
            HideGuideLines();
        }

        e.Handled = true;
    }

    /// <summary>
    ///     键盘按键事件 - 处理ESC取消
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CancelAndHide();
            e.Handled = true;
        }
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _capturedPointer = null;
        if (_ignorePointerCaptureLost)
            return;

        if (DataContext is ScreenCaptureViewModel vm && vm.IsSelecting)
            vm.CancelSelection();

        HideGuideLines();
    }

    private void UpdateGuideLines(Point point)
    {
        if (_captureCanvas == null || _verticalGuideLine == null || _horizontalGuideLine == null)
            return;

        var width = _captureCanvas.Bounds.Width;
        var height = _captureCanvas.Bounds.Height;
        if (width <= 0 || height <= 0)
            return;

        var x = Math.Clamp(point.X, 0, width);
        var y = Math.Clamp(point.Y, 0, height);

        _verticalGuideLine.StartPoint = new Point(x, 0);
        _verticalGuideLine.EndPoint = new Point(x, height);
        _verticalGuideLine.IsVisible = true;

        _horizontalGuideLine.StartPoint = new Point(0, y);
        _horizontalGuideLine.EndPoint = new Point(width, y);
        _horizontalGuideLine.IsVisible = true;
    }

    private void HideGuideLines()
    {
        if (_verticalGuideLine != null)
            _verticalGuideLine.IsVisible = false;
        if (_horizontalGuideLine != null)
            _horizontalGuideLine.IsVisible = false;
    }

    private void ReleasePointerCapture()
    {
        if (_capturedPointer == null)
            return;

        _ignorePointerCaptureLost = true;
        _capturedPointer.Capture(null);
        _capturedPointer = null;
        _ignorePointerCaptureLost = false;
    }

    private void CancelAndHide()
    {
        ReleasePointerCapture();
        HideGuideLines();
        if (DataContext is ScreenCaptureViewModel vm)
            vm.CancelCaptureSession();
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        // 清理事件订阅
        Closing -= OnWindowClosing;
        base.OnClosed(e);
    }
}
