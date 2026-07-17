using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using WordLens.ViewModels;

namespace WordLens.Views;

public partial class PopupWindowView : Window
{
    private bool _resultsPinnedToBottom = true;

    public event EventHandler? Hiding;

    public PopupWindowView()
    {
        InitializeComponent();

        // 拦截窗口关闭事件，改为隐藏窗口
        Closing += OnWindowClosing;

        // 隧道阶段处理 Ctrl+Enter，先于输入框，避免插入换行
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        // 取消关闭操作
        e.Cancel = true;

        // 隐藏窗口而不是关闭
        HideWindow();
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        if (DataContext is PopupWindowViewModel vm && vm.TranslateCommand.CanExecute(null))
            vm.TranslateCommand.Execute(null);

        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // 冒泡阶段处理 Esc，让打开的下拉框先消费按键关闭自身
        if (!e.Handled && e.Key == Key.Escape)
        {
            e.Handled = true;
            HideWindow();
        }
    }

    /// <summary>
    ///     聚焦原文输入框并全选。窗口是缓存复用的，每次显示后由 WindowManagerService 调用。
    /// </summary>
    public void FocusSourceEditor()
    {
        Dispatcher.UIThread.Post(() =>
        {
            SourceEditor.Focus();
            SourceEditor.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void ResultsScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer viewer)
            return;

        if (e.ExtentDelta.Y != 0)
        {
            // 内容增长（流式输出）时，只有用户停留在底部才跟随滚动
            if (_resultsPinnedToBottom)
                viewer.ScrollToEnd();
            return;
        }

        _resultsPinnedToBottom = viewer.Offset.Y + viewer.Viewport.Height >= viewer.Extent.Height - 4;
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void ResizeGrip_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(WindowEdge.SouthEast, e);
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        HideWindow();
    }

    private void HideWindow()
    {
        Hiding?.Invoke(this, EventArgs.Empty);
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        // 清理事件订阅
        Closing -= OnWindowClosing;
        base.OnClosed(e);
    }
}