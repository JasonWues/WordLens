using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Messaging;
using Ursa.Controls;
using WordLens.Messages;
using WordLens.ViewModels;

namespace WordLens.Views;

public partial class MainWindowView : Window
{
    public MainWindowView()
    {
        InitializeComponent();

        // 订阅窗口加载事件
        Opened += async (s, e) =>
        {
            if (DataContext is MainWindowViewModel vm) await vm.InitializeAsync();
        };

        KeyDown += OnWindowKeyDown;
        
        // 拦截窗口关闭事件，改为隐藏窗口
        Closing += OnWindowClosing;
        
        WeakReferenceMessenger.Default.Register<CloseWindowMessage>(this, (r, m) =>
        {
            // 通过消息关闭时也改为隐藏
            Hide();
        });
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        // 取消关闭操作
        e.Cancel = true;
        
        // 隐藏窗口而不是关闭
        Hide();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new CapturingKeyMessage(e));
    }

    protected override void OnClosed(EventArgs e)
    {
        // 清理事件订阅
        Closing -= OnWindowClosing;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        base.OnClosed(e);
    }
}