using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using WordLens.Models;
using WordLens.ViewModels;

namespace WordLens.Views;

public partial class PopupWindowView : Window
{
    public PopupWindowView()
    {
        InitializeComponent();
        
        // 拦截窗口关闭事件，改为隐藏窗口
        Closing += OnWindowClosing;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        // 取消关闭操作
        e.Cancel = true;
        
        // 隐藏窗口而不是关闭
        Hide();
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

    private void Minimize_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Hide();
    }

    private async void CopySource_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PopupWindowViewModel vm) await CopyToClipboardAsync(vm.SourceText);
    }

    private async void CopyTranslation_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string text } button) await CopyToClipboardAsync(text);
    }

    private void RemoveTranslation_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TranslationResult result } &&
            DataContext is PopupWindowViewModel vm)
        {
            vm.TranslationResults.Remove(result);
        }
    }

    private async Task CopyToClipboardAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var topLevel = GetTopLevel(this);
        if (topLevel?.Clipboard != null) await topLevel.Clipboard.SetTextAsync(text);
    }

    protected override void OnClosed(EventArgs e)
    {
        // 清理事件订阅
        Closing -= OnWindowClosing;
        base.OnClosed(e);
    }
}
