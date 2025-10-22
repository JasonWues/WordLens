using System.Threading.Tasks;
using WordLens.Services;

namespace WordLens.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public SettingsViewModel SettingsViewModel { get; }

        public MainWindowViewModel()
        {
            // 设计时构造函数
            SettingsViewModel = new SettingsViewModel();
        }

        public MainWindowViewModel(ISettingsService settingsService, IHotkeyManagerService hotkeyManagerService)
        {
            SettingsViewModel = new SettingsViewModel(settingsService, hotkeyManagerService);
        }

        public async Task InitializeAsync()
        {
            await SettingsViewModel.InitializeAsync();
        }
    }
}