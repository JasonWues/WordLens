using System;
using System.ComponentModel;
using Avalonia.Controls;

namespace WordLens.Views;

public partial class TranslationHistoryView : Window
{
    public TranslationHistoryView()
    {
        InitializeComponent();
        
        // 拦截窗口关闭事件
        Closing += OnWindowClosing;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        // 取消关闭操作
        e.Cancel = true;
        
        // 隐藏窗口而不是关闭
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        // 清理事件订阅
        Closing -= OnWindowClosing;
        base.OnClosed(e);
    }
}
