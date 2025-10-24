using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WordLens.Services;

namespace WordLens.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public SettingsViewModel SettingsViewModel { get; }
        
        public AboutViewModel AboutViewModel { get; }
        

        public MainWindowViewModel(SettingsViewModel settingsViewModel,AboutViewModel aboutViewModel)
        {
            SettingsViewModel = settingsViewModel;
            AboutViewModel = aboutViewModel;
        }

        public async Task InitializeAsync()
        {
            await SettingsViewModel.InitializeAsync();
        }
    }
}