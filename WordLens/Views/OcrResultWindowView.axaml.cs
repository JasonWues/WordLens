using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WordLens.ViewModels;

namespace WordLens.Views;

public partial class OcrResultWindowView : Window
{
    public OcrResultWindowView()
    {
        InitializeComponent();
        Closing += OnWindowClosing;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        HideWindow();
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        HideWindow();
    }

    private void HideWindow()
    {
        if (DataContext is OcrResultViewModel viewModel)
            viewModel.ReleaseScreenshot();

        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        Closing -= OnWindowClosing;
        base.OnClosed(e);
    }
}
