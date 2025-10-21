using System;
using System.Threading;
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
        private void ShowSetting()
        {
            var view = _services.GetRequiredService<MainWindowView>();
            view.DataContext =  _services.GetRequiredService<MainWindowViewModel>();
            view.Show();
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