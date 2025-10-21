using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            services.AddTransient<PopupWindowViewModel>();
            
            // Views
            services.AddTransient<MainWindowView>();
            
            // Services
            services.AddSingleton<IHotkeyService, HotkeyService>();
            services.AddSingleton<IHotkeyManagerService, HotkeyManagerService>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<TranslationService>();
            services.AddSingleton<ISelectionService, SelectionService>();
            services.AddHttpClient();
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Trace);
                logging.AddZLoggerConsole();
                logging.AddZLoggerRollingFile(opt =>
                {
                    opt.FilePathSelector = (dt, index) =>
                        $"logs/{dt:yyyy-MM-dd}_{index}.log";
                    opt.RollingInterval = RollingInterval.Day;
                    opt.RollingSizeKB = 1024;
                });
            });
        }
    }
}