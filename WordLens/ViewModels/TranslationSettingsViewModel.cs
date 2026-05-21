using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Sortable.Avalonia;
using WordLens.Models;
using WordLens.Services.Implementations;
using ZLogger;

namespace WordLens.ViewModels;

public partial class TranslationSettingsViewModel : ViewModelBase
{
    private readonly EncryptionService _encryptionService;
    private readonly ILogger<TranslationSettingsViewModel> _logger;
    private readonly OpenAIModelProviderService _modelProviderService;
    private readonly NetworkSettingsViewModel _networkSettings;
    private bool _hasLoadedProviderModels;

    [ObservableProperty] private bool hasModelLoadError;
    [ObservableProperty] private bool isLoadingModels;
    [ObservableProperty] private string modelLoadErrorMessage = string.Empty;
    [ObservableProperty] private ObservableCollection<ProviderConfig> providers = new();
    [ObservableProperty] private ModelInfo? selectedModelInfo;
    [ObservableProperty] private ProviderConfig? selectedProvider;

    public TranslationSettingsViewModel(
        OpenAIModelProviderService modelProviderService,
        EncryptionService encryptionService,
        NetworkSettingsViewModel networkSettings,
        ILogger<TranslationSettingsViewModel> logger)
    {
        _modelProviderService = modelProviderService;
        _encryptionService = encryptionService;
        _networkSettings = networkSettings;
        _logger = logger;
    }

    public void Load(AppSettings settings)
    {
        Providers.Clear();
        foreach (var provider in settings.Providers)
            Providers.Add(CloneProviderForEditing(provider, _encryptionService));

        SelectedProvider = Providers.FirstOrDefault(p => p.Name == settings.SelectedProvider) ??
                           Providers.FirstOrDefault();
        SelectedModelInfo = null;
        HasModelLoadError = false;
        ModelLoadErrorMessage = string.Empty;
        _hasLoadedProviderModels = false;
    }

    public List<ProviderConfig> BuildProviderConfigs()
    {
        return Providers.Select(CloneProviderForPersistence).ToList();
    }

    public async Task LoadProviderModelsOnceAsync()
    {
        if (_hasLoadedProviderModels)
            return;

        _hasLoadedProviderModels = true;

        try
        {
            await LoadModelsForAllProvidersAsync();
        }
        catch (Exception ex)
        {
            _hasLoadedProviderModels = false;
            _logger.ZLogWarning(ex, $"加载模型列表失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SelectProvider(ProviderConfig? provider)
    {
        if (provider != null)
            SelectedProvider = provider;
    }

    [RelayCommand]
    private void AddProvider()
    {
        var newProvider = new ProviderConfig
        {
            Name = $"新翻译源 {Providers.Count + 1}",
            Type = ProviderType.OpenAI,
            BaseUrl = "https://api.openai.com",
            Model = "gpt-4o-mini",
            RequestArguments = string.Empty,
            SystemPromptTemplate = string.Empty,
            UserPromptTemplate = string.Empty
        };
        Providers.Add(newProvider);
        SelectedProvider = newProvider;
    }

    [RelayCommand]
    private void DeleteProvider()
    {
        if (SelectedProvider == null || Providers.Count <= 1)
            return;

        var index = Providers.IndexOf(SelectedProvider);
        Providers.Remove(SelectedProvider);

        if (Providers.Count > 0)
            SelectedProvider = Providers[Math.Min(index, Providers.Count - 1)];
    }

    [RelayCommand]
    private void ReorderProvider(SortableUpdateEventArgs? args)
    {
        if (args == null)
            return;

        var movedProvider = args.Item as ProviderConfig;
        if (args.ApplyUpdateMutation() && movedProvider != null)
            SelectedProvider = movedProvider;
    }

    [RelayCommand]
    private async Task RefreshModelsAsync(ProviderConfig? provider)
    {
        if (provider == null || string.IsNullOrEmpty(provider.ApiKey))
        {
            _logger.ZLogWarning($"无法刷新模型：Provider或API Key为空");
            return;
        }

        IsLoadingModels = true;
        HasModelLoadError = false;
        ModelLoadErrorMessage = string.Empty;

        try
        {
            _logger.ZLogInformation($"开始刷新 {provider.Name} 的模型列表");
            var decryptedKey = _encryptionService.Decrypt(provider.ApiKey);
            var models = await _modelProviderService.GetAvailableModelsAsync(
                decryptedKey,
                provider.BaseUrl,
                _networkSettings.BuildProxyConfig(),
                CancellationToken.None);

            if (!string.IsNullOrEmpty(provider.Model) &&
                models.All(m => m.Id != provider.Model))
            {
                models.Insert(0, new ModelInfo { Id = provider.Model, OwnedBy = "custom" });
                _logger.ZLogInformation($"当前模型 {provider.Model} 不在列表中，已添加");
            }

            provider.AvailableModels ??= new ObservableCollection<ModelInfo>();
            provider.AvailableModels.Clear();
            foreach (var modelInfo in models)
                provider.AvailableModels.Add(modelInfo);

            SelectedModelInfo = provider.AvailableModels.FirstOrDefault(m => m.Id == provider.Model);
            _logger.ZLogInformation($"成功获取 {models.Count} 个模型");
        }
        catch (ArgumentException ex)
        {
            SetModelLoadError($"参数错误: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            SetModelLoadError($"网络请求失败: {ex.Message}", ex);
        }
        catch (TimeoutException ex)
        {
            SetModelLoadError($"请求超时: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            SetModelLoadError($"未知错误: {ex.Message}", ex);
        }
        finally
        {
            IsLoadingModels = false;
        }
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

    partial void OnSelectedModelInfoChanged(ModelInfo? value)
    {
        if (value != null && SelectedProvider != null)
        {
            SelectedProvider.Model = value.Id;
            _logger.ZLogInformation($"模型已更新为: {value.Id}");
        }
    }

    private async Task LoadModelsForAllProvidersAsync()
    {
        var providersToLoad = Providers
            .Where(p => p.IsEnabled && !string.IsNullOrEmpty(p.ApiKey))
            .ToList();

        foreach (var provider in providersToLoad)
            try
            {
                await RefreshModelsAsync(provider);
            }
            catch (Exception ex)
            {
                _logger.ZLogWarning(ex, $"为Provider {provider.Name} 加载模型失败: {ex.Message}");
            }
    }

    private void SetModelLoadError(string message, Exception ex)
    {
        HasModelLoadError = true;
        ModelLoadErrorMessage = message;
        _logger.ZLogError(ex, $"刷新模型列表失败: {ex.Message}");
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
