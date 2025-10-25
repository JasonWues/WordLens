using System;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Messaging;
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
        
        WeakReferenceMessenger.Default.Register<CloseWindowMessage>(this, (r, m) =>
        {
            Close();
        });
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new CapturingKeyMessage(e));
    }

    protected override void OnClosed(EventArgs e)
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        base.OnClosed(e);
    }
}