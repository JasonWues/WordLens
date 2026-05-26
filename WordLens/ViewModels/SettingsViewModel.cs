using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using WordLens.Abstractions.Models;
using WordLens.Models;
using WordLens.Services;
using ZLogger;

namespace WordLens.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private const int AutoSaveDelayMilliseconds = 700;

    private readonly SemaphoreSlim _autoSaveSemaphore = new(1, 1);
    private readonly IHotkeyManagerService _hotkeyManagerService;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly ISettingsService _settingsService;
    private readonly HashSet<ProviderConfig> _trackedOcrProviders = new();
    private readonly HashSet<ProviderConfig> _trackedTranslationProviders = new();
    private readonly HashSet<TtsProviderConfig> _trackedTtsProviders = new();
    private CancellationTokenSource? _autoSaveCts;
    private bool _hasLoadedSettings;
    private bool _isLoadingSettings;
    private AppSettings? _originalSettings;

    [ObservableProperty] private string autoSaveStatus = "已保存";

    [ObservableProperty] private bool isAutoSaving;

    [ObservableProperty] private int selectedSettingsSectionIndex;

    public SettingsViewModel(
        ISettingsService settingsService,
        IHotkeyManagerService hotkeyManagerService,
        GeneralSettingsViewModel generalSettings,
        TranslationSettingsViewModel translationSettings,
        OcrSettingsViewModel ocrSettings,
        TtsSettingsViewModel ttsSettings,
        NetworkSettingsViewModel networkSettings,
        TranslationHistoryViewModel history,
        AboutViewModel aboutViewModel,
        ILogger<SettingsViewModel> logger)
    {
        _settingsService = settingsService;
        _hotkeyManagerService = hotkeyManagerService;
        _logger = logger;
        GeneralSettings = generalSettings;
        TranslationSettings = translationSettings;
        OcrSettings = ocrSettings;
        TtsSettings = ttsSettings;
        NetworkSettings = networkSettings;
        History = history;
        About = aboutViewModel;

        ObserveSettingsChanges();
    }

    public GeneralSettingsViewModel GeneralSettings { get; }

    public TranslationSettingsViewModel TranslationSettings { get; }

    public OcrSettingsViewModel OcrSettings { get; }

    public TtsSettingsViewModel TtsSettings { get; }

    public NetworkSettingsViewModel NetworkSettings { get; }

    public TranslationHistoryViewModel History { get; }

    public AboutViewModel About { get; }

    public ViewModelBase CurrentSectionViewModel => SelectedSettingsSectionIndex switch
    {
        0 => GeneralSettings,
        1 => TranslationSettings,
        2 => OcrSettings,
        3 => TtsSettings,
        4 => NetworkSettings,
        5 => History,
        _ => About
    };

    public bool IsGeneralSectionSelected => SelectedSettingsSectionIndex == 0;

    public bool IsTranslationSectionSelected => SelectedSettingsSectionIndex == 1;

    public bool IsOcrSectionSelected => SelectedSettingsSectionIndex == 2;

    public bool IsTtsSectionSelected => SelectedSettingsSectionIndex == 3;

    public bool IsNetworkSectionSelected => SelectedSettingsSectionIndex == 4;

    public bool IsHistorySectionSelected => SelectedSettingsSectionIndex == 5;

    public bool IsAboutSectionSelected => SelectedSettingsSectionIndex == 6;

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
        OnPropertyChanged(nameof(IsHistorySectionSelected));
        OnPropertyChanged(nameof(IsAboutSectionSelected));
        OnPropertyChanged(nameof(CurrentSectionViewModel));

        if (value == 1)
            _ = TranslationSettings.LoadProviderModelsOnceAsync();
        else if (value == 5)
            _ = History.InitializeAsync();
    }

    private async Task LoadSettingsAsync()
    {
        _isLoadingSettings = true;

        try
        {
            var settings = await _settingsService.LoadAsync();
            _originalSettings = CloneSettings(settings);
            LoadIntoSections(settings);
            SyncProviderHandlers(TranslationSettings.Providers, _trackedTranslationProviders);
            SyncProviderHandlers(OcrSettings.OcrProviders, _trackedOcrProviders);
            SyncProviderHandlers(TtsSettings.TtsProviders, _trackedTtsProviders);
            _hasLoadedSettings = true;
            AutoSaveStatus = "已保存";
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private void LoadIntoSections(AppSettings settings)
    {
        GeneralSettings.Load(settings);
        TranslationSettings.Load(settings);
        OcrSettings.Load(settings);
        TtsSettings.Load(settings.Tts);
        NetworkSettings.Load(settings.Proxy);
    }

    private void ObserveSettingsChanges()
    {
        GeneralSettings.PropertyChanged += OnSettingsPropertyChanged;
        TranslationSettings.PropertyChanged += OnSettingsPropertyChanged;
        OcrSettings.PropertyChanged += OnSettingsPropertyChanged;
        TtsSettings.PropertyChanged += OnSettingsPropertyChanged;
        NetworkSettings.PropertyChanged += OnSettingsPropertyChanged;

        TranslationSettings.Providers.CollectionChanged += OnTranslationProvidersChanged;
        OcrSettings.OcrProviders.CollectionChanged += OnOcrProvidersChanged;
        TtsSettings.TtsProviders.CollectionChanged += OnTtsProvidersChanged;
        SyncProviderHandlers(TranslationSettings.Providers, _trackedTranslationProviders);
        SyncProviderHandlers(OcrSettings.OcrProviders, _trackedOcrProviders);
        SyncProviderHandlers(TtsSettings.TtsProviders, _trackedTtsProviders);
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ShouldIgnoreAutoSaveProperty(sender, e.PropertyName))
            return;

        QueueAutoSave();
    }

    private void OnTranslationProvidersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncProviderHandlers(TranslationSettings.Providers, _trackedTranslationProviders);
        QueueAutoSave();
    }

    private void OnOcrProvidersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncProviderHandlers(OcrSettings.OcrProviders, _trackedOcrProviders);
        QueueAutoSave();
    }

    private void OnTtsProvidersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncProviderHandlers(TtsSettings.TtsProviders, _trackedTtsProviders);
        QueueAutoSave();
    }

    private void OnProviderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProviderConfig.AvailableModels) ||
            e.PropertyName == nameof(ProviderConfig.TypeDisplayName) ||
            e.PropertyName == nameof(ProviderConfig.Summary) ||
            e.PropertyName == nameof(TtsProviderConfig.TypeDisplayName) ||
            e.PropertyName == nameof(TtsProviderConfig.Summary))
            return;

        QueueAutoSave();
    }

    private bool ShouldIgnoreAutoSaveProperty(object? sender, string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return false;

        if (ReferenceEquals(sender, GeneralSettings))
        {
            return propertyName is
                nameof(GeneralSettingsViewModel.IsCapturingHotkey) or
                nameof(GeneralSettingsViewModel.IsStartupSupported);
        }

        if (ReferenceEquals(sender, TranslationSettings))
        {
            return propertyName is
                nameof(TranslationSettingsViewModel.HasModelLoadError) or
                nameof(TranslationSettingsViewModel.IsSelectedProviderDeepL) or
                nameof(TranslationSettingsViewModel.IsSelectedProviderOpenAI) or
                nameof(TranslationSettingsViewModel.IsLoadingModels) or
                nameof(TranslationSettingsViewModel.ModelLoadErrorMessage) or
                nameof(TranslationSettingsViewModel.SelectedModelInfo);
        }

        if (ReferenceEquals(sender, TtsSettings))
        {
            return propertyName is
                nameof(TtsSettingsViewModel.IsSelectedTtsProviderLocal) or
                nameof(TtsSettingsViewModel.IsSelectedTtsProviderLlm);
        }

        if (ReferenceEquals(sender, OcrSettings))
        {
            return propertyName is
                nameof(OcrSettingsViewModel.IsSelectedOcrProviderOpenAI) or
                nameof(OcrSettingsViewModel.IsSelectedOcrProviderLocal);
        }

        return false;
    }

    private void QueueAutoSave()
    {
        if (_isLoadingSettings || !_hasLoadedSettings)
            return;

        AutoSaveStatus = "待保存";
        _autoSaveCts?.Cancel();
        _autoSaveCts?.Dispose();

        var cts = new CancellationTokenSource();
        _autoSaveCts = cts;
        _ = AutoSaveAfterDelayAsync(cts.Token);
    }

    private async Task AutoSaveAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(AutoSaveDelayMilliseconds, cancellationToken);
            await _autoSaveSemaphore.WaitAsync(cancellationToken);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                IsAutoSaving = true;
                AutoSaveStatus = "保存中...";

                var hotkeysChanged = await SaveSettingsCoreAsync();
                if (hotkeysChanged)
                    await _hotkeyManagerService.ReloadConfigAsync();

                if (!cancellationToken.IsCancellationRequested)
                    AutoSaveStatus = "已保存";
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                    IsAutoSaving = false;

                _autoSaveSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            IsAutoSaving = false;
            AutoSaveStatus = "保存失败";
            _logger.ZLogError(ex, $"自动保存设置失败: {ex.Message}");
        }
    }

    private async Task<bool> SaveSettingsCoreAsync()
    {
        var currentSettings = await _settingsService.LoadAsync();
        var settings = BuildSettingsFromViewModels(currentSettings.TranslationPopup);
        var previousSettings = _originalSettings;
        var hotkeysChanged = previousSettings == null ||
                             !AreHotkeysEqual(settings.Hotkey, previousSettings.Hotkey) ||
                             !AreHotkeysEqual(settings.OcrHotkey, previousSettings.OcrHotkey);
        var startupChanged = previousSettings == null ||
                             settings.StartWithSystem != previousSettings.StartWithSystem;

        if (startupChanged)
            GeneralSettings.ApplyStartupSetting();

        await _settingsService.SaveAsync(settings);
        _originalSettings = CloneSettings(settings);
        return hotkeysChanged;
    }

    private AppSettings BuildSettingsFromViewModels(TranslationPopupConfig? currentTranslationPopup)
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
            TranslationPopup = GeneralSettings.BuildTranslationPopupConfig(currentTranslationPopup),
            Proxy = NetworkSettings.BuildProxyConfig(),
            Tts = TtsSettings.BuildTtsConfig()
        };
    }

    private void SyncProviderHandlers(
        ObservableCollection<ProviderConfig> providers,
        HashSet<ProviderConfig> trackedProviders)
    {
        var currentProviders = providers.ToHashSet();
        foreach (var removedProvider in trackedProviders.Except(currentProviders).ToList())
        {
            removedProvider.PropertyChanged -= OnProviderPropertyChanged;
            trackedProviders.Remove(removedProvider);
        }

        foreach (var provider in currentProviders)
        {
            if (!trackedProviders.Add(provider))
                continue;

            provider.PropertyChanged += OnProviderPropertyChanged;
        }
    }

    private void SyncProviderHandlers(
        ObservableCollection<TtsProviderConfig> providers,
        HashSet<TtsProviderConfig> trackedProviders)
    {
        var currentProviders = providers.ToHashSet();
        foreach (var removedProvider in trackedProviders.Except(currentProviders).ToList())
        {
            removedProvider.PropertyChanged -= OnProviderPropertyChanged;
            trackedProviders.Remove(removedProvider);
        }

        foreach (var provider in currentProviders)
        {
            if (!trackedProviders.Add(provider))
                continue;

            provider.PropertyChanged += OnProviderPropertyChanged;
        }
    }

    private static bool AreHotkeysEqual(HotkeyConfig left, HotkeyConfig right)
    {
        return left.Modifiers == right.Modifiers &&
               left.Key == right.Key;
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
            TranslationPopup = CloneTranslationPopupConfig(settings.TranslationPopup),
            Proxy = NetworkSettingsViewModel.CloneProxyConfig(settings.Proxy),
            Tts = TtsSettingsViewModel.CloneTtsConfig(settings.Tts)
        };
    }

    private static TranslationPopupConfig CloneTranslationPopupConfig(TranslationPopupConfig config)
    {
        return new TranslationPopupConfig
        {
            PositionMode = config.PositionMode,
            X = config.X,
            Y = config.Y
        };
    }
}
