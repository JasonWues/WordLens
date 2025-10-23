using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpHook;
using WordLens.Services;
using WordLens.ViewModels;
using WordLens.Views;
using ZLogger;
using ZLogger.Providers;

namespace WordLens
{
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
                
                desktop.ShutdownRequested += (s, e) =>
                {
                    hotkeyManager.Dispose();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // ViewModels
            services.AddSingleton<ApplicationViewModel>();
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddSingleton<PopupWindowViewModel>();
            
            // Views
            services.AddTransient<MainWindowView>();
            
            // Services
            // 注意：移除了独立的 HotkeyService 和 OcrHotkeyService，
            // 现在所有快捷键统一在 HotkeyManagerService 中管理，避免重复触发
            services.AddSingleton<IHotkeyManagerService, HotkeyManagerService>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<TranslationService>();
            services.AddSingleton<ISelectionService, SelectionService>();
            services.AddSingleton<IGlobalHook, TaskPoolGlobalHook>();  // 使用 TaskPoolGlobalHook
            services.AddHttpClient();
            
            // 配置日志
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Information);
                
                // 控制台输出（开发时）
                logging.AddZLoggerConsole();
                
                // 文件输出到 AppData 目录
                var logDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WordLens",
                    "logs"
                );
                System.IO.Directory.CreateDirectory(logDir);
                
                logging.AddZLoggerRollingFile(opt =>
                {
                    opt.FilePathSelector = (dt, index) =>
                        System.IO.Path.Combine(logDir, $"wordlens-{dt:yyyy-MM-dd}_{index}.log");
                    opt.RollingInterval = RollingInterval.Day;
                    opt.RollingSizeKB = 10240; // 10MB per file
                });
            });
        }
    }
}