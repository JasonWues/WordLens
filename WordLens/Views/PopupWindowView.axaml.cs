using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
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

    private async void CopySource_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PopupWindowViewModel vm) await CopyToClipboardAsync(vm.SourceText);
    }

    private async void CopyTranslation_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string text } button) await CopyToClipboardAsync(text);
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