using System.Threading.Tasks;
using WordLens.Services;

namespace WordLens.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public SettingsViewModel SettingsViewModel { get; }
        
        public AboutViewModel AboutViewModel { get; }

        public MainWindowViewModel()
        {
            // 设计时构造函数
            SettingsViewModel = new SettingsViewModel();
            AboutViewModel = new AboutViewModel();
        }

        public MainWindowViewModel(ISettingsService settingsService, IHotkeyManagerService hotkeyManagerService)
        {
            SettingsViewModel = new SettingsViewModel(settingsService, hotkeyManagerService);
            AboutViewModel = new AboutViewModel();
        }

        public async Task InitializeAsync()
        {
            await SettingsViewModel.InitializeAsync();
        }
    }
}