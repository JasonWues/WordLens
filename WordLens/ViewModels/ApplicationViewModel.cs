using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WordLens.Messages;
using WordLens.Services;

namespace WordLens.ViewModels;

public partial class ApplicationViewModel : ViewModelBase
{
    private readonly IWindowManagerService _windowManager;
    private readonly IHotkeyManagerService _hotkeyManager;
    private readonly IClipboardMonitorService _clipboardMonitor;

    [ObservableProperty] private bool isClipboardMonitorEnabled;

    public ApplicationViewModel(
        IWindowManagerService windowManager,
        IHotkeyManagerService hotkeyManager,
        IClipboardMonitorService clipboardMonitor)
    {
        _windowManager = windowManager;
        _hotkeyManager = hotkeyManager;
        _clipboardMonitor = clipboardMonitor;
        _clipboardMonitor.TextChanged += OnClipboardTextChanged;

        // 注册翻译窗口消息
        WeakReferenceMessenger.Default.Register<TriggerTranslationMessage, string>(this, "text",
            async (recipient, message) =>
            {
                try
                {
                    await _windowManager.ShowTranslationWindowAsync(message.SelectedText);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            });

        // 注册OCR截图窗口消息
        WeakReferenceMessenger.Default.Register<TriggerTranslationMessage, string>(this, "ocr", (recipient, message) =>
        {
            try
            {
                _windowManager.ShowScreenCaptureWindow();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        });
    }

    public string ClipboardMonitorMenuHeader => IsClipboardMonitorEnabled ? "停止监听剪贴板" : "监听剪贴板";

    [RelayCommand]
    private async Task ShowSettingAsync()
    {
        await _windowManager.ShowSettingsWindowAsync();
    }

    [RelayCommand]
    private async Task ToggleClipboardMonitorAsync()
    {
        if (_clipboardMonitor.IsRunning)
            await _clipboardMonitor.StopAsync();
        else
            await _clipboardMonitor.StartAsync();

        IsClipboardMonitorEnabled = _clipboardMonitor.IsRunning;
    }

    [RelayCommand]
    private void Exit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime application)
        {
            _clipboardMonitor.TextChanged -= OnClipboardTextChanged;
            _clipboardMonitor.Dispose();
            _hotkeyManager.Dispose();
            application.Shutdown();
        }
    }

    partial void OnIsClipboardMonitorEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(ClipboardMonitorMenuHeader));
    }

    private void OnClipboardTextChanged(object? sender, ClipboardTextChangedEventArgs e)
    {
        _ = ShowClipboardTranslationAsync(e.Text);
    }

    private async Task ShowClipboardTranslationAsync(string text)
    {
        try
        {
            await _windowManager.ShowTranslationWindowAsync(text);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
