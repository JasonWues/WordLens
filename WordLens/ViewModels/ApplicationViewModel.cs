using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WordLens.Messages;
using WordLens.Services;

namespace WordLens.ViewModels;

public partial class ApplicationViewModel : ViewModelBase
{
    private readonly IWindowManagerService _windowManager;
    private readonly IHotkeyManagerService _hotkeyManager;

    public ApplicationViewModel(IWindowManagerService windowManager,IHotkeyManagerService hotkeyManager)
    {
        _windowManager = windowManager;
        _hotkeyManager = hotkeyManager;

        // 注册翻译窗口消息
        WeakReferenceMessenger.Default.Register<TriggerTranslationMessage, string>(this, "text",
            async (recipient, message) =>
            {
                await Dispatcher.UIThread.InvokeAsync(async () =>
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
            });

        // 注册OCR截图窗口消息
        WeakReferenceMessenger.Default.Register<TriggerTranslationMessage, string>(this, "ocr", (recipient, message) =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
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
        });
    }

    [RelayCommand]
    private async Task ShowSettingAsync()
    {
        await _windowManager.ShowSettingsWindowAsync();
    }

    [RelayCommand]
    private void ShowHistory()
    {
        try
        {
            _windowManager.ShowHistoryWindow();
        }
        catch (Exception e)
        {
            Console.WriteLine($"打开历史记录窗口失败: {e}");
        }
    }

    [RelayCommand]
    private void Exit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime application)
        {
            _hotkeyManager.Dispose();
            application.Shutdown();
        }
    }
}