using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using WordLens.ViewModels;

namespace WordLens.Views
{
    /// <summary>
    /// 屏幕捕获窗口
    /// 提供全屏遮罩和区域选择功能
    /// </summary>
    public partial class ScreenCaptureWindow : Window
    {
        private Canvas? _captureCanvas;
        private Border? _sizeHintBorder;

        public ScreenCaptureWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            
            // 获取控件引用
            _captureCanvas = this.FindControl<Canvas>("CaptureCanvas");
            _sizeHintBorder = this.FindControl<Border>("SizeHintBorder");

            // 窗口加载完成后设置焦点
            this.Opened += (s, e) =>
            {
                _captureCanvas?.Focus();
                
                // 设置窗口覆盖所有屏幕
                if (DataContext is ScreenCaptureViewModel vm)
                {
                    var bounds = vm.GetVirtualScreenBounds();
                    Position = new PixelPoint((int)bounds.X, (int)bounds.Y);
                    Width = bounds.Width;
                    Height = bounds.Height;
                }
            };
        }

        /// <summary>
        /// 鼠标按下事件 - 开始选择区域
        /// </summary>
        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is not ScreenCaptureViewModel vm) return;

            var point = e.GetPosition(_captureCanvas);
            vm.BeginSelection(point);
        }

        /// <summary>
        /// 鼠标移动事件 - 更新选择区域
        /// </summary>
        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (DataContext is not ScreenCaptureViewModel vm) return;

            var point = e.GetPosition(_captureCanvas);
            vm.UpdateSelection(point);

            // 更新尺寸提示位置（跟随鼠标）
            if (vm.IsSelecting && _sizeHintBorder != null)
            {
                Canvas.SetLeft(_sizeHintBorder, point.X + 15);
                Canvas.SetTop(_sizeHintBorder, point.Y + 15);
            }
        }

        /// <summary>
        /// 鼠标释放事件 - 完成选择并截图
        /// </summary>
        private async void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (DataContext is not ScreenCaptureViewModel vm) return;

            var point = e.GetPosition(_captureCanvas);
            var success = await vm.CompleteSelectionAsync(point);

            // 如果截图成功，关闭窗口
            if (success)
            {
                // 给用户一点时间看到选择矩形
                await System.Threading.Tasks.Task.Delay(100);
                Close();
            }
        }

        /// <summary>
        /// 键盘按键事件 - 处理ESC取消
        /// </summary>
        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (DataContext is ScreenCaptureViewModel vm)
                {
                    vm.CancelSelection();
                }
                Close();
            }
        }
    }
}