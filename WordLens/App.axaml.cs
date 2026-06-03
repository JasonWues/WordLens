using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SharpHook;
using WordLens.Abstractions.Services;
using WordLens.Infrastructure.Avalonia;
using WordLens.Infrastructure.Http;
using WordLens.Infrastructure.Screenshot;
using WordLens.Infrastructure.Security;
using WordLens.Models;
using WordLens.Providers.OpenAI;
using WordLens.Services;
using WordLens.Services.Implementations;
using WordLens.Services.LocalApi;
using WordLens.ViewModels;
using WordLens.Views;
#if WINDOWS
using WordLens.Windows;
#elif MACOS
using WordLens.Macos;
#else
using WordLens.Linux;
using WordLens.Macos;
#endif
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
            var collection = new ServiceCollection();
            ConfigureServices(collection);
            _services = collection.BuildServiceProvider();

            DataContext = _services.GetRequiredService<ApplicationViewModel>();

            var hotkeyManager = _services.GetRequiredService<IHotkeyManagerService>();
            var clipboardMonitor = _services.GetRequiredService<IClipboardMonitorService>();
            var settingsService = _services.GetRequiredService<ISettingsService>();
            var localApiService = _services.GetRequiredService<ILocalApiService>();
            var themeService = _services.GetRequiredService<AvaloniaThemeService>();
            var localizationService = _services.GetRequiredService<ILocalizationService>();
            var logger = _services.GetRequiredService<ILogger<App>>();
            _ = StartApplicationServicesAsync(
                settingsService,
                hotkeyManager,
                localApiService,
                themeService,
                localizationService,
                logger);

            var windowManager = _services.GetRequiredService<IWindowManagerService>();

            desktop.ShutdownRequested += (s, e) =>
            {
                hotkeyManager.Dispose();
                clipboardMonitor.Dispose();
                _ = localApiService.StopAsync();
                windowManager.CloseAllWindows();
            };

            if (ShouldShowSettingsOnStartup(desktop.Args))
            {
                Dispatcher.UIThread.Post(async () => await windowManager.ShowSettingsWindowAsync());
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static bool ShouldShowSettingsOnStartup(string[]? args)
    {
        if (args == null || args.Length == 0)
            return false;

        return args.Any(static arg =>
            string.Equals(arg, "--show-settings", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "--settings", StringComparison.OrdinalIgnoreCase));
    }


    private static void ConfigureServices(IServiceCollection services)
    {
        // ViewModels
        services.AddSingleton<ApplicationViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<GeneralSettingsViewModel>();
        services.AddSingleton<TranslationSettingsViewModel>();
        services.AddSingleton<OcrSettingsViewModel>();
        services.AddSingleton<TtsSettingsViewModel>();
        services.AddSingleton<NetworkSettingsViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddTransient<PopupWindowViewModel>();
        services.AddTransient<ScreenCaptureViewModel>();
        services.AddTransient<OcrResultViewModel>();
        services.AddSingleton<AboutViewModel>();
        services.AddSingleton<TranslationHistoryViewModel>();

        services.AddSingleton<MainWindowView>();

        // Services
        services.AddSingleton<IWindowManagerService, WindowManagerService>();
#if WINDOWS
        services.AddWordLensWindows();
#else
        services.AddSingleton<IGlobalHook, SimpleGlobalHook>();
        services.AddSingleton<IHotkeyBackend, SharpHookHotkeyBackend>();
        services.AddSingleton<IClipboardMonitorBackend, UnsupportedClipboardMonitorBackend>();
#if MACOS
        services.AddWordLensMacos();
#else
        if (OperatingSystem.IsLinux())
            services.AddWordLensLinux();
        else if (OperatingSystem.IsMacOS())
            services.AddWordLensMacos();
#endif
#endif
        services.TryAddSingleton<ICursorPositionProvider, UnsupportedCursorPositionProvider>();
        services.AddSingleton<IHotkeyManagerService, HotkeyManagerService>();
        services.AddSingleton<IClipboardMonitorService, ClipboardMonitorService>();
        services.AddSingleton<EncryptionService>();
        services.AddSingleton<AvaloniaThemeService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.TryAddSingleton<IStartupService, UnsupportedStartupService>();
        services.AddSingleton<IClipboardService, AvaloniaClipboardService>();
        services.AddSingleton<IPathPickerService, AvaloniaPathPickerService>();
        services.AddSingleton<IAudioPlayerService, SoundFlowAudioPlayerService>();
        services.TryAddSingleton<ILocalTtsBackend, UnsupportedLocalTtsBackend>();
        services.TryAddSingleton<ILocalOcrBackend, UnsupportedLocalOcrBackend>();
        services.AddSingleton<ITtsService, TtsService>();
        services.AddSingleton<OpenAIModelProviderService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ProxyAwareHttpClientFactory>();
        services.AddSingleton<UpdateService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IOcrService, OpenAIOcrService>();
        services.AddSingleton<TranslationService>();
        services.AddSingleton<LocalApiBridge>();
        services.AddSingleton<ILocalApiService, LocalApiService>();
        services.AddSingleton<ISelectionService, SelectionService>();
        services.AddSingleton<ITranslationHistoryService, TranslationHistoryService>();
        services.AddSingleton<IEudicVocabularyService, EudicVocabularyService>();
        services.AddHttpClient();

        // 截图服务 - Rust xcap 跨平台实现
        services.AddSingleton<IScreenshotService, XcapScreenshotService>();

        // 配置日志
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Information);

#if DEBUG
            // 控制台输出（开发时）
            logging.AddZLoggerConsole();
#endif

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

    private static async Task StartApplicationServicesAsync(
        ISettingsService settingsService,
        IHotkeyManagerService hotkeyManager,
        ILocalApiService localApiService,
        AvaloniaThemeService themeService,
        ILocalizationService localizationService,
        ILogger<App> logger)
    {
        try
        {
            var settings = await settingsService.LoadAsync();
            await Task.WhenAll(
                hotkeyManager.StartAsync(settings),
                ApplyStartupSettingsAsync(settings, localApiService, themeService, localizationService));
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"启动应用服务失败: {ex.Message}");
        }
    }

    private static async Task ApplyStartupSettingsAsync(
        AppSettings settings,
        ILocalApiService localApiService,
        AvaloniaThemeService themeService,
        ILocalizationService localizationService)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            localizationService.ApplyCulture(settings.UILanguage);
            themeService.ApplyFontFamily(settings.FontFamily);
        });

        await localApiService.ApplyConfigAsync(settings.LocalApi);
    }
}
