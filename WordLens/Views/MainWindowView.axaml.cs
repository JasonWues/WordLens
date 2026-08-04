using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Messaging;
using WordLens.Messages;
using WordLens.Util;
using WordLens.ViewModels;

namespace WordLens.Views;

public partial class MainWindowView : Window
{
    public MainWindowView()
    {
        InitializeComponent();

        KeyDown += OnWindowKeyDown;
        Activated += OnWindowActivated;

        // 拦截窗口关闭事件，改为隐藏窗口
        Closing += OnWindowClosing;

    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
            viewModel.SettingsViewModel.SetContentActive(true);
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        // 取消关闭操作
        e.Cancel = true;

        // 隐藏窗口而不是关闭
        Hide();

        if (DataContext is MainWindowViewModel viewModel)
            viewModel.SettingsViewModel.SetContentActive(false);
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        var message = new CapturingKeyMessage(
            KeyCodeUtil.ConvertToKeyCode(e.Key),
            KeyCodeUtil.ConvertToEventMask(e.KeyModifiers));
        WeakReferenceMessenger.Default.Send(message);
        if (message.Handled)
            e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        // 清理事件订阅
        Activated -= OnWindowActivated;
        Closing -= OnWindowClosing;
        KeyDown -= OnWindowKeyDown;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        base.OnClosed(e);
    }
}
