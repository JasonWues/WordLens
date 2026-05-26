using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sortable.Avalonia;
using WordLens.Infrastructure.Security;
using WordLens.Models;

namespace WordLens.ViewModels;

public partial class OcrSettingsViewModel : ViewModelBase
{
    private const string OpenAIDefaultBaseUrl = "https://api.openai.com";

    private readonly EncryptionService _encryptionService;

    [ObservableProperty] private ObservableCollection<ProviderConfig> ocrProviders = new();
    [ObservableProperty] private ProviderConfig? selectedOcrProvider;

    public OcrSettingsViewModel(EncryptionService encryptionService)
    {
        _encryptionService = encryptionService;
    }

    public List<ProviderTypeOption> AvailableOcrProviderTypes { get; } = new()
    {
        new ProviderTypeOption(ProviderType.OpenAI, "OpenAI 兼容"),
        new ProviderTypeOption(ProviderType.Local, "本地")
    };

    public bool IsSelectedOcrProviderOpenAI => SelectedOcrProvider?.Type == ProviderType.OpenAI;

    public bool IsSelectedOcrProviderLocal => SelectedOcrProvider?.Type == ProviderType.Local;

    public void Load(AppSettings settings)
    {
        OcrProviders.Clear();
        foreach (var provider in settings.OcrProviders)
            OcrProviders.Add(CloneProviderForEditing(provider, _encryptionService));

        SelectedOcrProvider = OcrProviders.FirstOrDefault(p => p.Name == settings.SelectedOcrProvider) ??
                              OcrProviders.FirstOrDefault();
    }

    public List<ProviderConfig> BuildProviderConfigs()
    {
        return OcrProviders.Select(CloneProviderForPersistence).ToList();
    }

    [RelayCommand]
    private void SelectOcrProvider(ProviderConfig? provider)
    {
        if (provider != null)
            SelectedOcrProvider = provider;
    }

    [RelayCommand]
    private void AddOcrProvider()
    {
        var newProvider = new ProviderConfig
        {
            Name = $"新OCR源 {OcrProviders.Count + 1}",
            Type = ProviderType.OpenAI,
            BaseUrl = OpenAIDefaultBaseUrl,
            Model = "gpt-4o-mini",
            RequestArguments = string.Empty,
            UserPromptTemplate = string.Empty
        };
        OcrProviders.Add(newProvider);
        SelectedOcrProvider = newProvider;
    }

    [RelayCommand]
    private void DeleteOcrProvider()
    {
        if (SelectedOcrProvider == null || OcrProviders.Count <= 1)
            return;

        var index = OcrProviders.IndexOf(SelectedOcrProvider);
        OcrProviders.Remove(SelectedOcrProvider);

        if (OcrProviders.Count > 0)
            SelectedOcrProvider = OcrProviders[Math.Min(index, OcrProviders.Count - 1)];
    }

    [RelayCommand]
    private void ReorderOcrProvider(SortableUpdateEventArgs? args)
    {
        if (args == null)
            return;

        var movedProvider = args.Item as ProviderConfig;
        if (args.ApplyUpdateMutation() && movedProvider != null)
            SelectedOcrProvider = movedProvider;
    }

    public static ProviderConfig CloneProviderForPersistence(ProviderConfig provider)
    {
        return new ProviderConfig
        {
            Name = provider.Name,
            Type = provider.Type,
            BaseUrl = provider.BaseUrl,
            ApiKey = provider.ApiKey,
            Model = provider.Model,
            IsEnabled = provider.IsEnabled,
            RequestArguments = provider.RequestArguments,
            SystemPromptTemplate = provider.SystemPromptTemplate,
            UserPromptTemplate = provider.UserPromptTemplate,
            AllowManualModelInput = provider.AllowManualModelInput
        };
    }

    partial void OnSelectedOcrProviderChanged(ProviderConfig? value)
    {
        if (value != null)
            value.PropertyChanged += OnSelectedOcrProviderPropertyChanged;

        NotifySelectedOcrProviderTypeProperties();
    }

    partial void OnSelectedOcrProviderChanging(ProviderConfig? oldValue, ProviderConfig? newValue)
    {
        if (oldValue != null)
            oldValue.PropertyChanged -= OnSelectedOcrProviderPropertyChanged;
    }

    private void OnSelectedOcrProviderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ProviderConfig.Type))
            return;

        if (sender is ProviderConfig provider)
            ApplyProviderTypeDefaults(provider);

        NotifySelectedOcrProviderTypeProperties();
    }

    private void NotifySelectedOcrProviderTypeProperties()
    {
        OnPropertyChanged(nameof(IsSelectedOcrProviderOpenAI));
        OnPropertyChanged(nameof(IsSelectedOcrProviderLocal));
    }

    private static void ApplyProviderTypeDefaults(ProviderConfig provider)
    {
        switch (provider.Type)
        {
            case ProviderType.OpenAI:
                if (string.IsNullOrWhiteSpace(provider.BaseUrl))
                    provider.BaseUrl = OpenAIDefaultBaseUrl;
                if (string.IsNullOrWhiteSpace(provider.Model))
                    provider.Model = "gpt-4o-mini";
                provider.AllowManualModelInput = true;
                break;

            case ProviderType.Local:
                provider.BaseUrl = string.Empty;
                provider.Model = string.Empty;
                provider.AllowManualModelInput = false;
                break;
        }
    }

    private static ProviderConfig CloneProviderForEditing(
        ProviderConfig provider,
        EncryptionService encryptionService)
    {
        var clone = CloneProviderForPersistence(provider);
        clone.ApiKey = string.IsNullOrEmpty(provider.ApiKey)
            ? provider.ApiKey
            : encryptionService.Decrypt(provider.ApiKey);
        return clone;
    }
}
