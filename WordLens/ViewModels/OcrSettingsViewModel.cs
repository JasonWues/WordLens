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

public partial class OcrSettingsViewModel : ViewModelBase
{
    private readonly EncryptionService _encryptionService;

    [ObservableProperty] private ObservableCollection<ProviderConfig> ocrProviders = new();
    [ObservableProperty] private ProviderConfig? selectedOcrProvider;

    public OcrSettingsViewModel(EncryptionService encryptionService)
    {
        _encryptionService = encryptionService;
    }

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
            BaseUrl = "https://api.openai.com",
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
