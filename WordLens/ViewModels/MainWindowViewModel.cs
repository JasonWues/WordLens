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

        public MainWindowViewModel(ISettingsService settingsService, IHotkeyService hotkeyService)
        {
            SettingsViewModel = new SettingsViewModel(settingsService, hotkeyService);
        }

        public async Task InitializeAsync()
        {
            await SettingsViewModel.InitializeAsync();
        }
    }
}