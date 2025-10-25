using System.Threading.Tasks;

namespace WordLens.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
    {
    }

    public MainWindowViewModel(SettingsViewModel settingsViewModel, AboutViewModel aboutViewModel)
    {
        SettingsViewModel = settingsViewModel;
        AboutViewModel = aboutViewModel;
    }

    public SettingsViewModel SettingsViewModel { get; }

    public AboutViewModel AboutViewModel { get; }

    public async Task InitializeAsync()
    {
        await SettingsViewModel.InitializeAsync();
    }
}