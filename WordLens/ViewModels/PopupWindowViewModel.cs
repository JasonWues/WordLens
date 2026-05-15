using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WordLens.Models;
using WordLens.Services;
using ZLogger;

namespace WordLens.ViewModels;

public partial class PopupWindowViewModel : ViewModelBase
{
    private readonly ILogger<PopupWindowViewModel> _logger;
    private readonly IClipboardService? _clipboardService;
    private readonly ISettingsService _settingsService;
    private readonly ITranslationHistoryService _historyService;
    private readonly TranslationService _translationService;
    private readonly Task _languageInitializationTask = Task.CompletedTask;
    private PendingHistorySave? _pendingHistorySave;
    private bool _isInitializingLanguages;
    private bool _isSavingPendingHistory;
    private ObservableCollection<TranslationResult>? _trackedTranslationResults;

    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private bool isTopmost;

    // 源语言选择
    [ObservableProperty] private LanguageInfo? selectedSourceLanguage;

    // 目标语言选择
    [ObservableProperty] private LanguageInfo? selectedTargetLanguage;

    [ObservableProperty] private ObservableCollection<LanguageInfo> sourceLanguages = new();

    [ObservableProperty] private string? sourceText;

    [ObservableProperty] private ObservableCollection<LanguageInfo> targetLanguages = new();

    [ObservableProperty] private ObservableCollection<TranslationResult> translationResults = new();


    public PopupWindowViewModel()
    {
        _clipboardService = null;
        _translationService = null!;
        _settingsService = null!;
        _historyService = null!;
        _logger = null!;

        InitializeLanguageCollections();
        TrackTranslationResults(TranslationResults);
    }

    public PopupWindowViewModel(
        TranslationService translationService,
        ISettingsService settingsService,
        ITranslationHistoryService historyService,
        IClipboardService clipboardService,
        ILogger<PopupWindowViewModel> logger)
    {
        _translationService = translationService;
        _settingsService = settingsService;
        _historyService = historyService;
        _clipboardService = clipboardService;
        _logger = logger;

        // 初始化语言列表
        _languageInitializationTask = InitializeLanguagesAsync();
        TrackTranslationResults(TranslationResults);
    }

    public bool CanCopySource => !string.IsNullOrWhiteSpace(SourceText);

    public bool CanUseTranslationAsSource =>
        TranslationResults.Any(r => r.IsSuccess && !string.IsNullOrWhiteSpace(r.Result));

    public bool HasTranslationResults => TranslationResults.Count > 0;

    public int SourceCharacterCount => SourceText?.Length ?? 0;

    private void InitializeLanguageCollections()
    {
        if (SourceLanguages.Count == 0)
        {
            // 加载源语言列表（包含自动检测）
            foreach (var lang in LanguageInfo.GetCommonLanguages()) SourceLanguages.Add(lang);
        }

        if (TargetLanguages.Count == 0)
        {
            // 加载目标语言列表（不包含自动检测）
            foreach (var lang in LanguageInfo.GetTargetLanguages()) TargetLanguages.Add(lang);
        }

        // 先设置同步默认值，避免首次快捷键触发时异步设置尚未加载完成。
        SelectedSourceLanguage ??= SourceLanguages.FirstOrDefault(l => l.Code == "auto");
        SelectedTargetLanguage ??= TargetLanguages.FirstOrDefault(l => l.Code == "en") ?? TargetLanguages.FirstOrDefault();
    }

    private async Task InitializeLanguagesAsync()
    {
        _isInitializingLanguages = true;
        try
        {
            InitializeLanguageCollections();

            // 从设置中加载上次选择的目标语言
            var settings = await _settingsService.LoadAsync();
            SelectedTargetLanguage = TargetLanguages.FirstOrDefault(l => l.Code == settings.LastTargetLanguage) ??
                                     TargetLanguages.FirstOrDefault(l => l.Code == "en") ??
                                     TargetLanguages.FirstOrDefault();

            _logger.ZLogInformation(
                $"语言初始化完成，源语言: {SelectedSourceLanguage?.Code}, 目标语言: {SelectedTargetLanguage?.Code}");
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"初始化语言列表失败");
        }
        finally
        {
            _isInitializingLanguages = false;
        }
    }

    partial void OnSourceTextChanged(string? value)
    {
        OnPropertyChanged(nameof(CanCopySource));
        OnPropertyChanged(nameof(SourceCharacterCount));
        CopySourceCommand.NotifyCanExecuteChanged();
    }

    partial void OnTranslationResultsChanged(ObservableCollection<TranslationResult> value)
    {
        TrackTranslationResults(value);
    }

    private void TrackTranslationResults(ObservableCollection<TranslationResult>? results)
    {
        if (_trackedTranslationResults != null)
        {
            _trackedTranslationResults.CollectionChanged -= OnTranslationResultsCollectionChanged;
            foreach (var result in _trackedTranslationResults)
                result.PropertyChanged -= OnTranslationResultPropertyChanged;
        }

        _trackedTranslationResults = results;

        if (_trackedTranslationResults != null)
        {
            _trackedTranslationResults.CollectionChanged += OnTranslationResultsCollectionChanged;
            foreach (var result in _trackedTranslationResults)
                result.PropertyChanged += OnTranslationResultPropertyChanged;
        }

        NotifyTranslationResultsStateChanged();
    }

    private void OnTranslationResultsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (TranslationResult result in e.OldItems)
                result.PropertyChanged -= OnTranslationResultPropertyChanged;

        if (e.NewItems != null)
            foreach (TranslationResult result in e.NewItems)
                result.PropertyChanged += OnTranslationResultPropertyChanged;

        NotifyTranslationResultsStateChanged();
    }

    private void OnTranslationResultPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TranslationResult.IsSuccess) or nameof(TranslationResult.Result))
            NotifyTranslationResultsStateChanged();

        if (e.PropertyName is nameof(TranslationResult.IsLoading) or nameof(TranslationResult.IsSuccess))
            _ = TrySavePendingHistoryAsync();
    }

    private void NotifyTranslationResultsStateChanged()
    {
        OnPropertyChanged(nameof(HasTranslationResults));
        OnPropertyChanged(nameof(CanUseTranslationAsSource));
    }

    partial void OnSelectedTargetLanguageChanged(LanguageInfo? value)
    {
        // 保存用户的选择
        if (value != null && !_isInitializingLanguages && _settingsService != null)
            _ = SaveLastTargetLanguageAsync(value.Code);
    }

    private async Task SaveLastTargetLanguageAsync(string languageCode)
    {
        try
        {
            var settings = await _settingsService.LoadAsync();
            settings.LastTargetLanguage = languageCode;
            await _settingsService.SaveAsync(settings);
            _logger.ZLogInformation($"已保存目标语言偏好: {languageCode}");
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"保存目标语言设置失败");
        }
    }

    [RelayCommand]
    public async Task TranslateAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        _pendingHistorySave = null;
        TranslationResults.Clear();

        try
        {
            await _languageInitializationTask;

            var sourceText = SourceText;
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                _logger.ZLogWarning($"原文为空，跳过翻译");
                return;
            }

            var targetLanguage = SelectedTargetLanguage ??
                                 TargetLanguages.FirstOrDefault(l => l.Code == "en") ??
                                 TargetLanguages.FirstOrDefault();
            if (targetLanguage == null)
            {
                _logger.ZLogWarning($"目标语言未初始化，跳过翻译");
                return;
            }

            var sourceLanguageCode = SelectedSourceLanguage?.Code ?? "auto";
            var targetLanguageCode = targetLanguage.Code;

            _logger.ZLogInformation($"开始翻译，源语言: {sourceLanguageCode}, 目标语言: {targetLanguageCode}");

            var results = await _translationService.TranslateAsync(
                sourceText,
                targetLanguageCode,
                sourceLanguageCode,
                cancellationToken);

            foreach (var result in results) TranslationResults.Add(result);

            _logger.ZLogInformation($"翻译结果已添加到UI，共 {results.Count} 个");

            if (results.Any(static r => r.IsLoading))
            {
                _pendingHistorySave = new PendingHistorySave(sourceText, sourceLanguageCode, targetLanguageCode);
                await TrySavePendingHistoryAsync();
            }
            else
            {
                await SaveToHistoryAsync(results, sourceText, sourceLanguageCode, targetLanguageCode);
            }
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"翻译过程中发生异常");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task TrySavePendingHistoryAsync()
    {
        if (_pendingHistorySave == null ||
            _isSavingPendingHistory ||
            TranslationResults.Any(static r => r.IsLoading))
        {
            return;
        }

        var pending = _pendingHistorySave;
        _pendingHistorySave = null;
        _isSavingPendingHistory = true;

        try
        {
            await SaveToHistoryAsync(
                TranslationResults.ToList(),
                pending.SourceText,
                pending.SourceLanguageCode,
                pending.TargetLanguageCode);
        }
        finally
        {
            _isSavingPendingHistory = false;
        }
    }

    /// <summary>
    /// 保存翻译结果到历史记录
    /// </summary>
    private async Task SaveToHistoryAsync(
        List<TranslationResult> results,
        string sourceText,
        string sourceLanguageCode,
        string targetLanguageCode)
    {
        try
        {
            // 只保存成功的翻译结果
            var successResults = results.Where(r => r.IsSuccess).ToList();
            if (successResults.Count == 0)
            {
                _logger.ZLogDebug($"没有成功的翻译结果，跳过保存历史");
                return;
            }

            // 将翻译结果序列化为JSON
            var historyResults = successResults
                .Select(static r => new TranslationHistoryResult
                {
                    ProviderName = r.ProviderName,
                    Result = r.Result
                })
                .ToList();
            var resultsJson = JsonSerializer.Serialize(
                historyResults,
                SourceGenerationContext.Default.ListTranslationHistoryResult);

            // 获取提供商名称列表
            var providerNames = string.Join(", ", successResults.Select(r => r.ProviderName));

            var history = new Models.TranslationHistory
            {
                SourceText = sourceText,
                SourceLanguage = sourceLanguageCode,
                TargetLanguage = targetLanguageCode,
                ResultsJson = resultsJson,
                ProviderNames = providerNames,
                CreatedAt = DateTime.Now,
                IsFavorite = false
            };

            await _historyService.SaveAsync(history);
            _logger.ZLogInformation($"翻译历史保存成功");
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"保存翻译历史失败: {ex.Message}");
            // 不抛出异常，避免影响正常翻译流程
        }
    }

    private sealed record PendingHistorySave(
        string SourceText,
        string SourceLanguageCode,
        string TargetLanguageCode);

    [RelayCommand]
    public void ToggleTopmost()
    {
        IsTopmost = !IsTopmost;
    }

    [RelayCommand]
    public void ClearSource()
    {
        SourceText = string.Empty;
        TranslationResults.Clear();
    }

    [RelayCommand(CanExecute = nameof(CanCopySource))]
    public async Task CopySourceAsync()
    {
        await CopyTextAsync(SourceText);
    }

    [RelayCommand]
    public async Task CopyTranslationAsync(string? text)
    {
        await CopyTextAsync(text);
    }

    [RelayCommand]
    public void RemoveTranslation(TranslationResult? result)
    {
        if (result != null)
            TranslationResults.Remove(result);
    }

    private async Task CopyTextAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || _clipboardService == null)
            return;

        await _clipboardService.SetTextAsync(text);
    }

    [RelayCommand]
    public void UseTranslationAsSource()
    {
        var result = TranslationResults.FirstOrDefault(r => r.IsSuccess && !string.IsNullOrWhiteSpace(r.Result));
        if (result?.Result == null)
        {
            _logger?.ZLogWarning($"没有可用译文，无法转为原文");
            return;
        }

        SourceText = result.Result;
        TranslationResults.Clear();

        if (SelectedTargetLanguage != null)
            SelectedSourceLanguage = SourceLanguages.FirstOrDefault(l => l.Code == SelectedTargetLanguage.Code) ??
                                     SelectedSourceLanguage;

        _logger?.ZLogInformation($"已将译文转为原文，长度: {SourceText.Length}");
    }

    [RelayCommand]
    public void SwapLanguages()
    {
        // 如果源语言不是自动检测，可以交换
        if (SelectedSourceLanguage?.Code != "auto" && SelectedTargetLanguage != null)
        {
            var tempCode = SelectedSourceLanguage?.Code;

            // 将当前目标语言设置为源语言
            SelectedSourceLanguage = SourceLanguages.FirstOrDefault(l => l.Code == SelectedTargetLanguage.Code);

            // 将原来的源语言设置为目标语言
            if (tempCode != null)
                SelectedTargetLanguage = TargetLanguages.FirstOrDefault(l => l.Code == tempCode);

            _logger.ZLogInformation($"已交换语言，源语言: {SelectedSourceLanguage?.Code}, 目标语言: {SelectedTargetLanguage?.Code}");
        }
        else
        {
            _logger.ZLogWarning($"无法交换语言：源语言为自动检测");
        }
    }
}
