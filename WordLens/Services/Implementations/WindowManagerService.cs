using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WordLens.Models;
using WordLens.ViewModels;
using WordLens.Views;
using ZLogger;

namespace WordLens.Services.Implementations;

/// <summary>
/// 窗口管理器服务实现
/// 使用单例模式管理所有窗口，确保每种窗口类型只有一个实例
/// </summary>
public class WindowManagerService : IWindowManagerService
{
    private const int TranslationWindowDefaultWidth = 340;
    private const int TranslationWindowDefaultHeight = 450;
    private const int TranslationWindowPointerOffset = 12;

    private readonly IServiceProvider _serviceProvider;
    private readonly ISettingsService _settingsService;
    private readonly ICursorPositionProvider _cursorPositionProvider;
    private readonly ILogger<WindowManagerService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // 窗口实例缓存
    private Window? _translationWindow;
    private Window? _settingsWindow;
    private Window? _screenCaptureWindow;
    private Window? _ocrResultWindow;

    public WindowManagerService(
        IServiceProvider serviceProvider,
        ISettingsService settingsService,
        ICursorPositionProvider cursorPositionProvider,
        ILogger<WindowManagerService> logger)
    {
        _serviceProvider = serviceProvider;
        _settingsService = settingsService;
        _cursorPositionProvider = cursorPositionProvider;
        _logger = logger;
        _logger.ZLogInformation($"窗口管理器服务已初始化");
    }

    /// <summary>
    /// 显示或激活翻译窗口
    /// </summary>
    public async Task ShowTranslationWindowAsync(string selectedText)
    {
        await _semaphore.WaitAsync();
        try
        {
            var settings = await _settingsService.LoadAsync();
            var popupConfig = settings.TranslationPopup;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_translationWindow == null)
                {
                    _logger.ZLogInformation($"创建新的翻译窗口");
                    
                    // 使用作用域创建 ViewModel（因为它是 Transient）
                    using var scope = _serviceProvider.CreateScope();
                    var viewModel = scope.ServiceProvider.GetRequiredService<PopupWindowViewModel>();
                    viewModel.SourceText = selectedText;

                    var popupWindow = new PopupWindowView
                    {
                        DataContext = viewModel
                    };
                    popupWindow.Hiding += TranslationWindow_Hiding;
                    _translationWindow = popupWindow;

                    // 订阅窗口关闭事件，清理引用
                    _translationWindow.Closed += async (s, e) =>
                    {
                        await _semaphore.WaitAsync();
                        try
                        {
                            _logger.ZLogInformation($"翻译窗口已关闭，清理引用");
                            if (s is PopupWindowView closedPopupWindow)
                                closedPopupWindow.Hiding -= TranslationWindow_Hiding;
                            _translationWindow = null;
                        }
                        finally
                        {
                            _semaphore.Release();
                        }
                    };

                    _translationWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                    ApplyTranslationWindowPosition(_translationWindow, popupConfig, false);
                    _translationWindow.Show();
                    _translationWindow.Activate();
                    
                    // 执行翻译
                    _ = viewModel.TranslateAsync(CancellationToken.None);
                }
                else
                {
                    _logger.ZLogInformation($"翻译窗口已存在，更新内容并激活");
                    
                    // 更新翻译内容
                    if (_translationWindow.DataContext is PopupWindowViewModel vm)
                    {
                        vm.SourceText = selectedText;
                        _ = vm.TranslateAsync(CancellationToken.None);
                    }

                    var wasVisible = _translationWindow.IsVisible;
                    ApplyTranslationWindowPosition(_translationWindow, popupConfig, wasVisible);

                    // 激活窗口
                    ActivateWindow(_translationWindow);
                }

            });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 显示或激活设置窗口
    /// </summary>
    public async Task ShowSettingsWindowAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (_settingsWindow == null)
                {
                    _logger.ZLogInformation($"创建新的设置窗口");
                    
                    var view = _serviceProvider.GetRequiredService<MainWindowView>();
                    var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
                    view.DataContext = viewModel;

                    _settingsWindow = view;

                    // 订阅窗口关闭事件，清理引用
                    _settingsWindow.Closed += async (s, e) =>
                    {
                        await _semaphore.WaitAsync();
                        try
                        {
                            _logger.ZLogInformation($"设置窗口已关闭，清理引用");
                            _settingsWindow = null;
                        }
                        finally
                        {
                            _semaphore.Release();
                        }
                    };

                    _settingsWindow.Show();
                    
                    // 初始化设置
                    await viewModel.InitializeAsync();
                }
                else
                {
                    _logger.ZLogInformation($"设置窗口已存在，激活");
                    
                    // 激活窗口
                    ActivateWindow(_settingsWindow);
                }

            });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 显示或激活截图窗口
    /// </summary>
    public void ShowScreenCaptureWindow()
    {
        Dispatcher.UIThread.Post(async () => await ShowScreenCaptureWindowCoreAsync());
    }

    private async Task ShowScreenCaptureWindowCoreAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_screenCaptureWindow == null)
            {
                _logger.ZLogInformation($"创建新的截图窗口");

                using var scope = _serviceProvider.CreateScope();
                var newViewModel = scope.ServiceProvider.GetRequiredService<ScreenCaptureViewModel>();

                _screenCaptureWindow = new ScreenCaptureWindow
                {
                    DataContext = newViewModel
                };

                // 订阅窗口关闭事件，清理引用
                _screenCaptureWindow.Closed += (s, e) =>
                {
                    _semaphore.Wait();
                    try
                    {
                        _logger.ZLogInformation($"截图窗口已关闭，清理引用");
                        _screenCaptureWindow = null;
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                };
            }

            if (_screenCaptureWindow is not ScreenCaptureWindow captureWindow ||
                captureWindow.DataContext is not ScreenCaptureViewModel viewModel)
            {
                _logger.ZLogError($"截图窗口状态无效，无法显示");
                return;
            }

            if (captureWindow.IsVisible)
            {
                _logger.ZLogInformation($"截图窗口已存在，激活");
                ActivateWindow(captureWindow);
                return;
            }

            var prepared = await viewModel.PrepareCaptureAsync();
            if (!prepared)
            {
                _logger.ZLogError($"截图窗口启动前预捕获屏幕背景失败");
                return;
            }

            captureWindow.PrepareForCapture(viewModel.ScreenBackgroundBounds);
            captureWindow.Show();
            captureWindow.Activate();
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"显示截图窗口时发生错误");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 显示或激活 OCR 结果窗口
    /// </summary>
    public void ShowOcrResultWindow(WriteableBitmap screenshot, string? recognizedText = null)
    {
        _semaphore.Wait();
        try
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                if (_ocrResultWindow == null)
                {
                    _logger.ZLogInformation($"创建新的 OCR 结果窗口");

                    using var scope = _serviceProvider.CreateScope();
                    var viewModel = scope.ServiceProvider.GetRequiredService<OcrResultViewModel>();
                    viewModel.LoadScreenshot(screenshot, recognizedText);

                    _ocrResultWindow = new OcrResultWindowView
                    {
                        DataContext = viewModel
                    };

                    _ocrResultWindow.Closed += (s, e) =>
                    {
                        _semaphore.Wait();
                        try
                        {
                            _logger.ZLogInformation($"OCR 结果窗口已关闭，清理引用");
                            _ocrResultWindow = null;
                        }
                        finally
                        {
                            _semaphore.Release();
                        }
                    };

                    _ocrResultWindow.Show();
                }
                else
                {
                    _logger.ZLogInformation($"OCR 结果窗口已存在，更新内容并激活");

                    if (_ocrResultWindow.DataContext is OcrResultViewModel vm)
                        vm.LoadScreenshot(screenshot, recognizedText);

                    ActivateWindow(_ocrResultWindow);
                }

            });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 关闭所有窗口
    /// </summary>
    public void CloseAllWindows()
    {
        List<Window?> windows;

        _semaphore.Wait();
        try
        {
            _logger.ZLogInformation($"关闭所有窗口");

            windows = new List<Window?>
            {
                _translationWindow,
                _settingsWindow,
                _screenCaptureWindow,
                _ocrResultWindow
            };

            // 清理所有引用
            _translationWindow = null;
            _settingsWindow = null;
            _screenCaptureWindow = null;
            _ocrResultWindow = null;
        }
        finally
        {
            _semaphore.Release();
        }

        foreach (var window in windows)
        {
            if (window == null)
                continue;

            try
            {
                if (window is ScreenCaptureWindow screenCaptureWindow)
                    screenCaptureWindow.ForceClose();
                else
                    window.Close();
            }
            catch (Exception ex)
            {
                _logger.ZLogError(ex, $"关闭窗口时发生错误");
            }
        }
    }

    private void TranslationWindow_Hiding(object? sender, EventArgs e)
    {
        if (sender is not Window window)
            return;

        _ = SaveTranslationWindowPositionAsync(window.Position);
    }

    private async Task SaveTranslationWindowPositionAsync(PixelPoint position)
    {
        try
        {
            var settings = await _settingsService.LoadAsync();
            settings.TranslationPopup ??= new TranslationPopupConfig();
            settings.TranslationPopup.X = position.X;
            settings.TranslationPopup.Y = position.Y;
            await _settingsService.SaveAsync(settings);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"保存翻译窗口位置失败");
        }
    }

    private void ApplyTranslationWindowPosition(
        Window window,
        TranslationPopupConfig config,
        bool windowIsVisible)
    {
        if (config.PositionMode == TranslationPopupPositionMode.RememberPosition)
        {
            if (windowIsVisible)
                return;

            if (TryGetRememberedPosition(config, out var rememberedPosition))
            {
                SetClampedWindowPosition(window, rememberedPosition);
                return;
            }
        }

        if (TryGetCursorPosition(out var cursorPosition))
        {
            SetClampedWindowPosition(
                window,
                new PixelPoint(
                    cursorPosition.X + TranslationWindowPointerOffset,
                    cursorPosition.Y + TranslationWindowPointerOffset));
            return;
        }

        if (TryGetRememberedPosition(config, out var fallbackPosition))
        {
            SetClampedWindowPosition(window, fallbackPosition);
            return;
        }

        CenterWindowInPrimaryScreen(window);
    }

    private static bool TryGetRememberedPosition(
        TranslationPopupConfig config,
        out PixelPoint position)
    {
        if (config.X.HasValue && config.Y.HasValue)
        {
            position = new PixelPoint(config.X.Value, config.Y.Value);
            return true;
        }

        position = default;
        return false;
    }

    private bool TryGetCursorPosition(out PixelPoint position)
    {
        position = default;

        if (!_cursorPositionProvider.TryGetCursorPosition(out var cursorPosition))
            return false;

        position = new PixelPoint(cursorPosition.X, cursorPosition.Y);
        return true;
    }

    private static void SetClampedWindowPosition(Window window, PixelPoint target)
    {
        var screen = window.Screens.ScreenFromPoint(target) ?? window.Screens.Primary;
        if (screen == null)
        {
            window.Position = target;
            return;
        }

        var workingArea = screen.WorkingArea;
        var windowSize = GetWindowPixelSize(window);
        var maxX = workingArea.X + Math.Max(0, workingArea.Width - windowSize.Width);
        var maxY = workingArea.Y + Math.Max(0, workingArea.Height - windowSize.Height);

        window.Position = new PixelPoint(
            Math.Clamp(target.X, workingArea.X, maxX),
            Math.Clamp(target.Y, workingArea.Y, maxY));
    }

    private static void CenterWindowInPrimaryScreen(Window window)
    {
        var screen = window.Screens.Primary;
        if (screen == null)
            return;

        var workingArea = screen.WorkingArea;
        var windowSize = GetWindowPixelSize(window);

        window.Position = new PixelPoint(
            workingArea.X + Math.Max(0, (workingArea.Width - windowSize.Width) / 2),
            workingArea.Y + Math.Max(0, (workingArea.Height - windowSize.Height) / 2));
    }

    private static (int Width, int Height) GetWindowPixelSize(Window window)
    {
        var width = GetWindowDimension(
            window.Bounds.Width,
            window.Width,
            TranslationWindowDefaultWidth);
        var height = GetWindowDimension(
            window.Bounds.Height,
            window.Height,
            TranslationWindowDefaultHeight);
        var scaling = double.IsFinite(window.RenderScaling) && window.RenderScaling > 0
            ? window.RenderScaling
            : 1;

        return (
            Math.Max(1, (int)Math.Ceiling(width * scaling)),
            Math.Max(1, (int)Math.Ceiling(height * scaling)));
    }

    private static double GetWindowDimension(double actual, double configured, double fallback)
    {
        if (double.IsFinite(actual) && actual > 0)
            return actual;

        if (double.IsFinite(configured) && configured > 0)
            return configured;

        return fallback;
    }

    /// <summary>
    /// 激活窗口（显示并置于前台）
    /// </summary>
    private void ActivateWindow(Window window)
    {
        try
        {
            // 如果窗口是隐藏状态，先显示
            if (!window.IsVisible)
            {
                window.Show();
            }

            // 激活窗口到前台
            window.Activate();
            
            // 确保窗口不是最小化状态
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            _logger.ZLogDebug($"窗口已激活: {window.GetType().Name}");
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"激活窗口时发生错误");
        }
    }
}
