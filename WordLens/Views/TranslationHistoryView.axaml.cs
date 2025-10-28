using System;
using System.ComponentModel;
using Avalonia.Controls;
using WordLens.ViewModels;

namespace WordLens.Views;

public partial class TranslationHistoryView : Window
{
    public TranslationHistoryView()
    {
        InitializeComponent();
        
        // 拦截窗口关闭事件
        Closing += OnWindowClosing;
        
        // 窗口加载时初始化ViewModel
        Loaded += async (s, e) =>
        {
            if (DataContext is TranslationHistoryViewModel vm)
            {
                await vm.InitializeAsync();
            }
        };
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