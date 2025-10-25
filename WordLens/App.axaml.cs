using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ScreenCapture.NET;
using SharpHook;
using WordLens.Services;
using WordLens.Services.Implementations;
using WordLens.Services.Implementations.Screenshot;
using WordLens.ViewModels;
using WordLens.Views;
using ZLogger;
using ZLogger.Providers;

namespace WordLens;

public class App : Application
{
    private IServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            var collection = new ServiceCollection();
            ConfigureServices(collection);
            _services = collection.BuildServiceProvider();

            DataContext = _services.GetRequiredService<ApplicationViewModel>();

            var hotkeyManager = _services.GetRequiredService<IHotkeyManagerService>();
            _ = hotkeyManager.StartAsync();

            desktop.ShutdownRequested += (s, e) => { hotkeyManager.Dispose(); };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove) BindingPlugins.DataValidators.Remove(plugin);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // ViewModels
        services.AddSingleton<ApplicationViewModel>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<PopupWindowViewModel>();
        services.AddSingleton<ScreenCaptureViewModel>();
        services.AddSingleton<AboutViewModel>();
        services.AddSingleton<TranslationHistoryViewModel>();


        // Views
        services.AddTransient<MainWindowView>();

        // Services
        services.AddSingleton<IHotkeyManagerService, HotkeyManagerService>();
        services.AddSingleton<IEncryptionService, EncryptionService>();
        services.AddSingleton<IModelProviderService, OpenAIModelProviderService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<TranslationService>();
        services.AddSingleton<ISelectionService, SelectionService>();
        services.AddSingleton<ITranslationHistoryService, TranslationHistoryService>();
        services.AddSingleton<IGlobalHook, TaskPoolGlobalHook>();
        services.AddHttpClient();

        // 截图服务 - 根据平台注册不同实现
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IScreenCaptureService, DX11ScreenCaptureService>();
            services.AddSingleton<IScreenshotService, WindowsScreenshotService>();
        }
        else if (OperatingSystem.IsLinux())
        {
            services.AddSingleton<IScreenCaptureService, X11ScreenCaptureService>();
            services.AddSingleton<IScreenshotService, LinuxScreenshotService>();
        }
        else if (OperatingSystem.IsMacOS())
        {
            services.AddSingleton<IScreenshotService, MacScreenshotService>();
        }

        // 配置日志
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Information);

            // 控制台输出（开发时）
            logging.AddZLoggerConsole();

            // 文件输出到 AppData 目录
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WordLens",
                "logs"
            );
            Directory.CreateDirectory(logDir);

            logging.AddZLoggerRollingFile(opt =>
            {
                opt.FilePathSelector = (dt, index) =>
                    Path.Combine(logDir, $"wordlens-{dt:yyyy-MM-dd}_{index}.log");
                opt.RollingInterval = RollingInterval.Day;
                opt.RollingSizeKB = 10240; // 10MB per file
            });
        });
    }
}