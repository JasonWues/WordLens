using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sortable.Avalonia;
using WordLens.Infrastructure.Security;
using WordLens.Models;

namespace WordLens.ViewModels;

public partial class TtsSettingsViewModel : ViewModelBase
{
    private readonly EncryptionService _encryptionService;

    [ObservableProperty] private ObservableCollection<TtsProviderConfig> ttsProviders = new();
    [ObservableProperty] private TtsProviderConfig? selectedTtsProvider;

    public TtsSettingsViewModel(EncryptionService encryptionService)
    {
        _encryptionService = encryptionService;
    }

    public List<TtsProviderTypeOption> AvailableTtsProviderTypes { get; } = new()
    {
        new TtsProviderTypeOption(TtsProviderType.Local, "本地"),
        new TtsProviderTypeOption(TtsProviderType.Llm, "LLM")
    };

    public bool IsSelectedTtsProviderLocal => SelectedTtsProvider?.Type == TtsProviderType.Local;

    public bool IsSelectedTtsProviderLlm => SelectedTtsProvider?.Type == TtsProviderType.Llm;

    public void Load(TtsConfig config)
    {
        TtsProviders.Clear();
        foreach (var provider in config.Providers)
            TtsProviders.Add(CloneProviderForEditing(provider, _encryptionService));

        SelectedTtsProvider = TtsProviders.FirstOrDefault(p => p.Name == config.SelectedProvider) ??
                              TtsProviders.FirstOrDefault();
    }

    public TtsConfig BuildTtsConfig()
    {
        return new TtsConfig
        {
            SelectedProvider = SelectedTtsProvider?.Name ?? TtsProviders.FirstOrDefault()?.Name,
            Providers = TtsProviders.Select(CloneProviderForPersistence).ToList()
        };
    }

    public static TtsConfig CloneTtsConfig(TtsConfig config)
    {
        return new TtsConfig
        {
            SelectedProvider = config.SelectedProvider,
            Providers = config.Providers.Select(CloneProviderForPersistence).ToList()
        };
    }

    public static TtsProviderConfig CloneProviderForPersistence(TtsProviderConfig provider)
    {
        return new TtsProviderConfig
        {
            Name = provider.Name,
            Type = provider.Type,
            IsEnabled = provider.IsEnabled,
            BaseUrl = provider.BaseUrl,
            ApiKey = provider.ApiKey,
            Model = provider.Model,
            Voice = provider.Voice,
            Speed = Math.Clamp(provider.Speed, 0.25, 4.0),
            RequestArguments = provider.RequestArguments
        };
    }

    [RelayCommand]
    private void SelectTtsProvider(TtsProviderConfig? provider)
    {
        if (provider != null)
            SelectedTtsProvider = provider;
    }

    [RelayCommand]
    private void AddTtsProvider()
    {
        var newProvider = new TtsProviderConfig
        {
            Name = $"新TTS源 {TtsProviders.Count + 1}",
            Type = TtsProviderType.Llm,
            BaseUrl = "https://api.openai.com",
            Model = "gpt-4o-mini-tts",
            Voice = "alloy",
            RequestArguments = string.Empty,
            Speed = 1.0
        };

        TtsProviders.Add(newProvider);
        SelectedTtsProvider = newProvider;
    }

    [RelayCommand]
    private void DeleteTtsProvider()
    {
        if (SelectedTtsProvider == null || TtsProviders.Count <= 1)
            return;

        var index = TtsProviders.IndexOf(SelectedTtsProvider);
        TtsProviders.Remove(SelectedTtsProvider);

        if (TtsProviders.Count > 0)
            SelectedTtsProvider = TtsProviders[Math.Min(index, TtsProviders.Count - 1)];
    }

    [RelayCommand]
    private void ReorderTtsProvider(SortableUpdateEventArgs? args)
    {
        if (args == null)
            return;

        var movedProvider = args.Item as TtsProviderConfig;
        if (args.ApplyUpdateMutation() && movedProvider != null)
            SelectedTtsProvider = movedProvider;
    }

    partial void OnSelectedTtsProviderChanged(TtsProviderConfig? value)
    {
        if (value != null)
            value.PropertyChanged += OnSelectedProviderPropertyChanged;

        OnPropertyChanged(nameof(IsSelectedTtsProviderLocal));
        OnPropertyChanged(nameof(IsSelectedTtsProviderLlm));
    }

    partial void OnSelectedTtsProviderChanging(TtsProviderConfig? oldValue, TtsProviderConfig? newValue)
    {
        if (oldValue != null)
            oldValue.PropertyChanged -= OnSelectedProviderPropertyChanged;
    }

    private void OnSelectedProviderPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TtsProviderConfig.Type))
            return;

        OnPropertyChanged(nameof(IsSelectedTtsProviderLocal));
        OnPropertyChanged(nameof(IsSelectedTtsProviderLlm));
    }

    private static TtsProviderConfig CloneProviderForEditing(
        TtsProviderConfig provider,
        EncryptionService encryptionService)
    {
        var clone = CloneProviderForPersistence(provider);
        clone.ApiKey = string.IsNullOrEmpty(provider.ApiKey)
            ? provider.ApiKey
            : encryptionService.Decrypt(provider.ApiKey);
        return clone;
    }
}

public class TtsProviderTypeOption
{
    public TtsProviderTypeOption(TtsProviderType value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }

    public TtsProviderType Value { get; set; }
    public string DisplayName { get; set; }
}
