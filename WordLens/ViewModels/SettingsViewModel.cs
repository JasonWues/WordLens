using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WordLens.Messages;
using WordLens.Models;
using WordLens.Services;

namespace WordLens.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IHotkeyManagerService _hotkeyManagerService;
    private readonly ISettingsService _settingsService;
    private AppSettings? _originalSettings;

    [ObservableProperty] private int selectedSettingsSectionIndex;

    public SettingsViewModel(
        ISettingsService settingsService,
        IHotkeyManagerService hotkeyManagerService,
        GeneralSettingsViewModel generalSettings,
        TranslationSettingsViewModel translationSettings,
        OcrSettingsViewModel ocrSettings,
        TtsSettingsViewModel ttsSettings,
        NetworkSettingsViewModel networkSettings)
    {
        _settingsService = settingsService;
        _hotkeyManagerService = hotkeyManagerService;
        GeneralSettings = generalSettings;
        TranslationSettings = translationSettings;
        OcrSettings = ocrSettings;
        TtsSettings = ttsSettings;
        NetworkSettings = networkSettings;
    }

    public GeneralSettingsViewModel GeneralSettings { get; }

    public TranslationSettingsViewModel TranslationSettings { get; }

    public OcrSettingsViewModel OcrSettings { get; }

    public TtsSettingsViewModel TtsSettings { get; }

    public NetworkSettingsViewModel NetworkSettings { get; }

    public bool IsGeneralSectionSelected => SelectedSettingsSectionIndex == 0;

    public bool IsTranslationSectionSelected => SelectedSettingsSectionIndex == 1;

    public bool IsOcrSectionSelected => SelectedSettingsSectionIndex == 2;

    public bool IsTtsSectionSelected => SelectedSettingsSectionIndex == 3;

    public bool IsNetworkSectionSelected => SelectedSettingsSectionIndex == 4;

    public bool IsAboutSectionSelected => SelectedSettingsSectionIndex == 5;

    public async Task InitializeAsync()
    {
        await LoadSettingsAsync();
    }

    partial void OnSelectedSettingsSectionIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsGeneralSectionSelected));
        OnPropertyChanged(nameof(IsTranslationSectionSelected));
        OnPropertyChanged(nameof(IsOcrSectionSelected));
        OnPropertyChanged(nameof(IsTtsSectionSelected));
        OnPropertyChanged(nameof(IsNetworkSectionSelected));
        OnPropertyChanged(nameof(IsAboutSectionSelected));

        if (value == 1)
            _ = TranslationSettings.LoadProviderModelsOnceAsync();
    }

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        var settings = await _settingsService.LoadAsync();
        _originalSettings = CloneSettings(settings);
        LoadIntoSections(settings);
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        await SaveSettingsCoreAsync();
        WeakReferenceMessenger.Default.Send(new CloseWindowMessage());
    }

    [RelayCommand]
    private async Task ApplySettingsAsync()
    {
        await SaveSettingsCoreAsync();
        await _hotkeyManagerService.ReloadConfigAsync();
    }

    [RelayCommand]
    private void CancelSettings()
    {
        if (_originalSettings != null)
            LoadIntoSections(_originalSettings);

        WeakReferenceMessenger.Default.Send(new CloseWindowMessage());
    }

    private void LoadIntoSections(AppSettings settings)
    {
        GeneralSettings.Load(settings);
        TranslationSettings.Load(settings);
        OcrSettings.Load(settings);
        TtsSettings.Load(settings.Tts);
        NetworkSettings.Load(settings.Proxy);
    }

    private async Task SaveSettingsCoreAsync()
    {
        var settings = BuildSettingsFromViewModels();
        GeneralSettings.ApplyStartupSetting();
        await _settingsService.SaveAsync(settings);
        _originalSettings = CloneSettings(settings);
    }

    private AppSettings BuildSettingsFromViewModels()
    {
        return new AppSettings
        {
            UILanguage = GeneralSettings.UiLanguage,
            LastTargetLanguage = _originalSettings?.LastTargetLanguage ?? "en",
            StartWithSystem = GeneralSettings.StartWithSystem,
            Hotkey = GeneralSettings.BuildHotkeyConfig(),
            OcrHotkey = GeneralSettings.BuildOcrHotkeyConfig(),
            SelectedProvider = TranslationSettings.SelectedProvider?.Name ??
                               TranslationSettings.Providers.FirstOrDefault()?.Name,
            Providers = TranslationSettings.BuildProviderConfigs(),
            SelectedOcrProvider = OcrSettings.SelectedOcrProvider?.Name ??
                                  OcrSettings.OcrProviders.FirstOrDefault()?.Name,
            OcrProviders = OcrSettings.BuildProviderConfigs(),
            Streaming = GeneralSettings.BuildStreamingConfig(),
            Proxy = NetworkSettings.BuildProxyConfig(),
            Tts = TtsSettings.BuildTtsConfig()
        };
    }

    private static AppSettings CloneSettings(AppSettings settings)
    {
        return new AppSettings
        {
            UILanguage = settings.UILanguage,
            LastTargetLanguage = settings.LastTargetLanguage,
            StartWithSystem = settings.StartWithSystem,
            Hotkey = GeneralSettingsViewModel.CloneHotkeyConfig(settings.Hotkey),
            OcrHotkey = GeneralSettingsViewModel.CloneHotkeyConfig(settings.OcrHotkey),
            SelectedProvider = settings.SelectedProvider,
            Providers = settings.Providers.Select(TranslationSettingsViewModel.CloneProviderForPersistence).ToList(),
            SelectedOcrProvider = settings.SelectedOcrProvider,
            OcrProviders = settings.OcrProviders.Select(OcrSettingsViewModel.CloneProviderForPersistence).ToList(),
            Streaming = new StreamingConfig
            {
                Enabled = settings.Streaming.Enabled,
                TypewriterDelayMs = settings.Streaming.TypewriterDelayMs,
                CharsPerUpdate = settings.Streaming.CharsPerUpdate
            },
            Proxy = NetworkSettingsViewModel.CloneProxyConfig(settings.Proxy),
            Tts = TtsSettingsViewModel.CloneTtsConfig(settings.Tts)
        };
    }
}
