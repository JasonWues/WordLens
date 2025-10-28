using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WindowManagerService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // 窗口实例缓存
    private Window? _translationWindow;
    private Window? _settingsWindow;
    private Window? _screenCaptureWindow;
    private Window? _historyWindow;

    public WindowManagerService(
        IServiceProvider serviceProvider,
        ILogger<WindowManagerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _logger.ZLogInformation($"窗口管理器服务已初始化");
    }

    /// <summary>
    /// 显示或激活翻译窗口
    /// </summary>
    public async Task<Window> ShowTranslationWindowAsync(string selectedText)
    {
        await _semaphore.WaitAsync();
        try
        {
            return await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_translationWindow == null)
                {
                    _logger.ZLogInformation($"创建新的翻译窗口");
                    
                    // 使用作用域创建 ViewModel（因为它是 Transient）
                    using var scope = _serviceProvider.CreateScope();
                    var viewModel = scope.ServiceProvider.GetRequiredService<PopupWindowViewModel>();
                    viewModel.SourceText = selectedText;

                    _translationWindow = new PopupWindowView
                    {
                        DataContext = viewModel
                    };

                    // 订阅窗口关闭事件，清理引用
                    _translationWindow.Closed += async (s, e) =>
                    {
                        await _semaphore.WaitAsync();
                        try
                        {
                            _logger.ZLogInformation($"翻译窗口已关闭，清理引用");
                            _translationWindow = null;
                        }
                        finally
                        {
                            _semaphore.Release();
                        }
                    };

                    _translationWindow.Show();
                    
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

                    // 激活窗口
                    ActivateWindow(_translationWindow);
                }

                return _translationWindow;
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
    public async Task<Window> ShowSettingsWindowAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
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

                return _settingsWindow;
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
    public Window ShowScreenCaptureWindow()
    {
        _semaphore.Wait();
        try
        {
            return Dispatcher.UIThread.Invoke(() =>
            {
                if (_screenCaptureWindow == null)
                {
                    _logger.ZLogInformation($"创建新的截图窗口");
                    
                    using var scope = _serviceProvider.CreateScope();
                    var viewModel = scope.ServiceProvider.GetRequiredService<ScreenCaptureViewModel>();

                    _screenCaptureWindow = new ScreenCaptureWindow
                    {
                        DataContext = viewModel
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

                    _screenCaptureWindow.Show();
                }
                else
                {
                    _logger.ZLogInformation($"截图窗口已存在，激活");
                    
                    // 激活窗口
                    ActivateWindow(_screenCaptureWindow);
                }

                return _screenCaptureWindow;
            });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 显示或激活历史记录窗口
    /// </summary>
    public Window ShowHistoryWindow()
    {
        _semaphore.Wait();
        try
        {
            return Dispatcher.UIThread.Invoke(() =>
            {
                if (_historyWindow == null)
                {
                    _logger.ZLogInformation($"创建新的历史记录窗口");
                    
                    var viewModel = _serviceProvider.GetRequiredService<TranslationHistoryViewModel>();
                    
                    _historyWindow = new TranslationHistoryView
                    {
                        DataContext = viewModel
                    };

                    // 订阅窗口关闭事件，清理引用
                    _historyWindow.Closed += (s, e) =>
                    {
                        _semaphore.Wait();
                        try
                        {
                            _logger.ZLogInformation($"历史记录窗口已关闭，清理引用");
                            _historyWindow = null;
                        }
                        finally
                        {
                            _semaphore.Release();
                        }
                    };

                    _historyWindow.Show();
                }
                else
                {
                    _logger.ZLogInformation($"历史记录窗口已存在，激活");
                    
                    // 激活窗口
                    ActivateWindow(_historyWindow);
                }

                return _historyWindow;
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
        _semaphore.Wait();
        try
        {
            _logger.ZLogInformation($"关闭所有窗口");

            var windows = new List<Window?>
            {
                _translationWindow,
                _settingsWindow,
                _screenCaptureWindow,
                _historyWindow
            };

            foreach (var window in windows)
            {
                if (window != null)
                {
                    try
                    {
                        window.Close();
                    }
                    catch (Exception ex)
                    {
                        _logger.ZLogError(ex, $"关闭窗口时发生错误");
                    }
                }
            }

            // 清理所有引用
            _translationWindow = null;
            _settingsWindow = null;
            _screenCaptureWindow = null;
            _historyWindow = null;
        }
        finally
        {
            _semaphore.Release();
        }
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