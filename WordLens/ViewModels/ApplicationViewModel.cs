using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using WordLens.Messages;
using WordLens.Views;

namespace WordLens.ViewModels
{
    public partial class ApplicationViewModel : ViewModelBase
    {
        readonly private IServiceProvider _services;

        public ApplicationViewModel(IServiceProvider services)
        {

            _services = services;

            WeakReferenceMessenger.Default.Register<ShowPopupMessage>(this, async (recipient, message) =>
            {
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    try
                    {
                        using var scope = _services.CreateScope();
                        var vm = scope.ServiceProvider.GetRequiredService<PopupWindowViewModel>();
                        vm.SourceText = message.SelectedText;

                        var window = new PopupWindowView { DataContext = vm };
                        window.Topmost = true;

                        window.Show();
                        await vm.TranslateAsync(CancellationToken.None);
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
            var view = _services.GetRequiredService<MainWindowView>();
            var viewModel = _services.GetRequiredService<MainWindowViewModel>();
            view.DataContext = viewModel;
            view.Show();
            
            // 初始化设置
            await viewModel.InitializeAsync();
        }

        [RelayCommand]
        private void Exit()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime application)
            {
                application.Shutdown();
            }
        }
    }
}