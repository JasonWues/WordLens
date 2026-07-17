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
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WordLens.Messages;
using WordLens.Models;
using WordLens.Services;
using ZLogger;

namespace WordLens.ViewModels;

public partial class PopupWindowViewModel : ViewModelBase
{
    private readonly ILogger<PopupWindowViewModel> _logger;
    private readonly IClipboardService? _clipboardService;
    private readonly ILocalizationService? _localizationService;
    private readonly ISettingsService _settingsService;
    private readonly ITranslationHistoryService _historyService;
    private readonly IEudicVocabularyService? _eudicVocabularyService;
    private readonly ITtsService? _ttsService;
    private readonly TranslationService _translationService;
    private readonly Task _languageInitializationTask = Task.CompletedTask;
    private PendingHistorySave? _pendingHistorySave;
    private bool _isInitializingLanguages;
    private bool _isSavingPendingHistory;
    private ObservableCollection<TranslationResult>? _trackedTranslationResults;
    private CancellationTokenSource? _translationCts;
    private CancellationTokenSource? _speechCts;
    private CancellationTokenSource? _sourceCopyFeedbackCts;
    private readonly Dictionary<TranslationResult, CancellationTokenSource> _copyFeedbackTokens = new();
    private int _activeSpeechCount;
    private static readonly TimeSpan CopyFeedbackDuration = TimeSpan.FromSeconds(1.5);

    [ObservableProperty] private bool isAddingToVocabulary;

    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private bool isTopmost;

    // 源语言选择
    [ObservableProperty] private LanguageInfo? selectedSourceLanguage;

    [ObservableProperty] private ProviderConfig? selectedTranslationProvider;

    // 目标语言选择
    [ObservableProperty] private LanguageInfo? selectedTargetLanguage;

    [ObservableProperty] private ObservableCollection<ProviderConfig> availableTranslationProviders = new();

    [ObservableProperty] private ObservableCollection<LanguageInfo> sourceLanguages = new();

    [ObservableProperty] private string? sourceText;

    [ObservableProperty] private ObservableCollection<LanguageInfo> targetLanguages = new();

    [ObservableProperty] private ObservableCollection<TranslationResult> translationResults = new();

    [ObservableProperty] private string vocabularyStatus = string.Empty;

    // 复制原文成功后的短暂反馈状态
    [ObservableProperty] private bool isSourceCopied;

    // 是否正在朗读（源文本或译文）
    [ObservableProperty] private bool isSpeaking;


    public PopupWindowViewModel()
    {
        _clipboardService = null;
        _localizationService = null;
        _translationService = null!;
        _settingsService = null!;
        _historyService = null!;
        _eudicVocabularyService = null;
        _ttsService = null;
        _logger = null!;

        InitializeLanguageCollections();
        TrackTranslationResults(TranslationResults);
    }

    public PopupWindowViewModel(
        TranslationService translationService,
        ISettingsService settingsService,
        ITranslationHistoryService historyService,
        IClipboardService clipboardService,
        ILocalizationService localizationService,
        IEudicVocabularyService eudicVocabularyService,
        ITtsService ttsService,
        ILogger<PopupWindowViewModel> logger)
    {
        _translationService = translationService;
        _settingsService = settingsService;
        _historyService = historyService;
        _clipboardService = clipboardService;
        _localizationService = localizationService;
        _eudicVocabularyService = eudicVocabularyService;
        _ttsService = ttsService;
        _logger = logger;

        // 初始化语言列表
        _languageInitializationTask = InitializeLanguagesAsync();
        TrackTranslationResults(TranslationResults);
        _localizationService.CultureChanged += OnCultureChanged;
    }

    public bool CanCopySource => !string.IsNullOrWhiteSpace(SourceText);

    public bool CanSpeakSource => CanCopySource;

    public bool CanAddSourceToVocabulary =>
        CanCopySource &&
        !IsAddingToVocabulary &&
        _eudicVocabularyService != null;

    public bool CanUseTranslationAsSource =>
        TranslationResults.Any(r => r.IsSuccess && !string.IsNullOrWhiteSpace(r.Result));

    public bool CanRetranslateWithSelectedProvider =>
        SelectedTranslationProvider != null &&
        !string.IsNullOrWhiteSpace(SourceText) &&
        !IsBusy &&
        TranslationResults.All(static r => !r.IsLoading);

    public bool HasTranslationResults => TranslationResults.Count > 0;

    public bool IsTranslating => IsBusy || TranslationResults.Any(static r => r.IsLoading);

    public bool CanSwapLanguages =>
        SelectedSourceLanguage is { } sourceLanguage &&
        sourceLanguage.Code != "auto" &&
        SelectedTargetLanguage != null;

    public bool HasEnabledProviders => AvailableTranslationProviders.Count > 0;

    public bool ShowEmptyHint => !HasTranslationResults && !IsTranslating;

    public int SourceCharacterCount => SourceText?.Length ?? 0;

    public string SourceCharacterCountText => _localizationService?.GetString(
        "Popup_CharacterCountFormat",
        SourceCharacterCount) ?? $"{SourceCharacterCount} 字符";

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
            ApplyTranslationProviders(settings);

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
        OnPropertyChanged(nameof(CanSpeakSource));
        OnPropertyChanged(nameof(CanAddSourceToVocabulary));
        OnPropertyChanged(nameof(SourceCharacterCount));
        OnPropertyChanged(nameof(SourceCharacterCountText));
        OnPropertyChanged(nameof(CanRetranslateWithSelectedProvider));
        VocabularyStatus = string.Empty;
        CopySourceCommand.NotifyCanExecuteChanged();
        SpeakSourceCommand.NotifyCanExecuteChanged();
        AddSourceToVocabularyCommand.NotifyCanExecuteChanged();
        RetranslateWithSelectedProviderCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsAddingToVocabularyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanAddSourceToVocabulary));
        AddSourceToVocabularyCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRetranslateWithSelectedProvider));
        OnPropertyChanged(nameof(IsTranslating));
        OnPropertyChanged(nameof(ShowEmptyHint));
        RetranslateWithSelectedProviderCommand.NotifyCanExecuteChanged();
        StopTranslationCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedTranslationProviderChanged(ProviderConfig? value)
    {
        OnPropertyChanged(nameof(CanRetranslateWithSelectedProvider));
        RetranslateWithSelectedProviderCommand.NotifyCanExecuteChanged();
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
        if (e.PropertyName is nameof(TranslationResult.IsLoading)
            or nameof(TranslationResult.IsSuccess)
            or nameof(TranslationResult.Result))
            NotifyTranslationResultsStateChanged();

        if (e.PropertyName is nameof(TranslationResult.IsLoading) or nameof(TranslationResult.IsSuccess))
            _ = TrySavePendingHistoryAsync();
    }

    private void NotifyTranslationResultsStateChanged()
    {
        OnPropertyChanged(nameof(HasTranslationResults));
        OnPropertyChanged(nameof(CanUseTranslationAsSource));
        OnPropertyChanged(nameof(CanRetranslateWithSelectedProvider));
        OnPropertyChanged(nameof(IsTranslating));
        OnPropertyChanged(nameof(ShowEmptyHint));
        RetranslateWithSelectedProviderCommand.NotifyCanExecuteChanged();
        StopTranslationCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedSourceLanguageChanged(LanguageInfo? value)
    {
        OnPropertyChanged(nameof(CanSwapLanguages));
        SwapLanguagesCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedTargetLanguageChanged(LanguageInfo? value)
    {
        // 保存用户的选择
        if (value != null && !_isInitializingLanguages && _settingsService != null)
            _ = SaveLastTargetLanguageAsync(value.Code);

        OnPropertyChanged(nameof(CanSwapLanguages));
        SwapLanguagesCommand.NotifyCanExecuteChanged();
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

    private async Task RefreshTranslationProvidersAsync()
    {
        try
        {
            var settings = await _settingsService.LoadAsync();
            ApplyTranslationProviders(settings);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"刷新翻译源列表失败");
        }
    }

    private void ApplyTranslationProviders(AppSettings settings)
    {
        var selectedName = SelectedTranslationProvider?.Name ?? settings.SelectedProvider;

        AvailableTranslationProviders.Clear();
        foreach (var provider in settings.Providers.Where(static p => p.IsEnabled))
            AvailableTranslationProviders.Add(CloneProviderForSelection(provider));

        SelectedTranslationProvider =
            AvailableTranslationProviders.FirstOrDefault(p => p.Name == selectedName) ??
            AvailableTranslationProviders.FirstOrDefault(p => p.Name == settings.SelectedProvider) ??
            AvailableTranslationProviders.FirstOrDefault();

        OnPropertyChanged(nameof(HasEnabledProviders));
        OnPropertyChanged(nameof(CanRetranslateWithSelectedProvider));
        RetranslateWithSelectedProviderCommand.NotifyCanExecuteChanged();
    }

    private static ProviderConfig CloneProviderForSelection(ProviderConfig provider)
    {
        return new ProviderConfig
        {
            Name = provider.Name,
            Type = provider.Type,
            BaseUrl = provider.BaseUrl,
            Model = provider.Model,
            IsEnabled = provider.IsEnabled,
            RequestArguments = provider.RequestArguments,
            SystemPromptTemplate = provider.SystemPromptTemplate,
            UserPromptTemplate = provider.UserPromptTemplate,
            AllowManualModelInput = provider.AllowManualModelInput
        };
    }

    /// <summary>
    ///     开始一次翻译运行并返回其取消令牌。
    ///     窗口管理器直调 TranslateAsync 时传入的是 CancellationToken.None，
    ///     因此取消能力由 VM 自持的 CTS 提供；停止按钮取消该 CTS。
    /// </summary>
    /// <param name="cancelActiveRun">
    ///     true 表示取消并替换当前运行（新翻译）；false 表示并入当前运行（单卡刷新，不打断其他卡片的流式输出）。
    /// </param>
    private CancellationToken BeginTranslationRun(CancellationToken externalToken, bool cancelActiveRun = true)
    {
        if (!cancelActiveRun && _translationCts is { IsCancellationRequested: false })
            return _translationCts.Token;

        var previous = _translationCts;
        _translationCts = externalToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(externalToken)
            : new CancellationTokenSource();
        previous?.Cancel();
        previous?.Dispose();
        return _translationCts.Token;
    }

    [RelayCommand]
    public async Task TranslateAsync(CancellationToken cancellationToken)
    {
        var runToken = BeginTranslationRun(cancellationToken);

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
            await RefreshTranslationProvidersAsync();

            _logger.ZLogInformation($"开始翻译，源语言: {sourceLanguageCode}, 目标语言: {targetLanguageCode}");

            runToken.ThrowIfCancellationRequested();

            var results = await _translationService.TranslateAsync(
                sourceText,
                targetLanguageCode,
                sourceLanguageCode,
                runToken);

            // 等待期间被取消（例如新的翻译已启动）时，不把过期结果加进集合
            runToken.ThrowIfCancellationRequested();

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
        catch (OperationCanceledException)
        {
            _logger.ZLogInformation($"翻译已被用户取消");
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

    [RelayCommand(CanExecute = nameof(CanRetranslateWithSelectedProvider))]
    public async Task RetranslateWithSelectedProviderAsync(CancellationToken cancellationToken)
    {
        await RetranslateProviderCoreAsync(SelectedTranslationProvider?.Name, cancellationToken, cancelActiveRun: true);
    }

    [RelayCommand(CanExecute = nameof(IsTranslating))]
    public void StopTranslation()
    {
        _logger.ZLogInformation($"用户请求停止翻译");
        _translationCts?.Cancel();
    }

    /// <summary>
    ///     重新翻译单个结果卡片对应的翻译源，不打断其他卡片的进行中任务。
    /// </summary>
    [RelayCommand]
    public async Task RefreshTranslationAsync(TranslationResult? result, CancellationToken cancellationToken)
    {
        if (result == null || result.IsLoading || string.IsNullOrWhiteSpace(result.ProviderName))
            return;

        await RetranslateProviderCoreAsync(result.ProviderName, cancellationToken, cancelActiveRun: false);
    }

    private async Task RetranslateProviderCoreAsync(
        string? providerName,
        CancellationToken externalToken,
        bool cancelActiveRun)
    {
        var runToken = BeginTranslationRun(externalToken, cancelActiveRun);

        IsBusy = true;

        try
        {
            await _languageInitializationTask;
            await RefreshTranslationProvidersAsync();

            if (string.IsNullOrWhiteSpace(providerName))
            {
                _logger.ZLogWarning($"未选择翻译源，跳过重新翻译");
                return;
            }

            var sourceText = SourceText;
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                _logger.ZLogWarning($"原文为空，跳过重新翻译");
                return;
            }

            var targetLanguage = SelectedTargetLanguage ??
                                 TargetLanguages.FirstOrDefault(l => l.Code == "en") ??
                                 TargetLanguages.FirstOrDefault();
            if (targetLanguage == null)
            {
                _logger.ZLogWarning($"目标语言未初始化，跳过重新翻译");
                return;
            }

            var sourceLanguageCode = SelectedSourceLanguage?.Code ?? "auto";
            var targetLanguageCode = targetLanguage.Code;

            _logger.ZLogInformation(
                $"使用翻译源重新翻译，翻译源: {providerName}, 源语言: {sourceLanguageCode}, 目标语言: {targetLanguageCode}");

            runToken.ThrowIfCancellationRequested();

            var result = await _translationService.TranslateWithProviderAsync(
                providerName,
                sourceText,
                targetLanguageCode,
                sourceLanguageCode,
                runToken);

            // 等待期间被取消时，不把过期结果加进集合
            runToken.ThrowIfCancellationRequested();

            InsertOrReplaceTranslationResult(result);

            if (result.IsLoading)
            {
                _pendingHistorySave = new PendingHistorySave(
                    sourceText,
                    sourceLanguageCode,
                    targetLanguageCode,
                    providerName);
                await TrySavePendingHistoryAsync();
            }
            else
            {
                await SaveToHistoryAsync(new List<TranslationResult> { result }, sourceText, sourceLanguageCode, targetLanguageCode);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.ZLogInformation($"重新翻译已被用户取消");
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"重新翻译过程中发生异常");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void InsertOrReplaceTranslationResult(TranslationResult result)
    {
        for (var i = 0; i < TranslationResults.Count; i++)
            if (TranslationResults[i].ProviderName == result.ProviderName)
            {
                TranslationResults[i] = result;
                return;
            }

        TranslationResults.Add(result);
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
            var results = string.IsNullOrWhiteSpace(pending.ProviderName)
                ? TranslationResults.ToList()
                : TranslationResults.Where(r => r.ProviderName == pending.ProviderName).ToList();

            await SaveToHistoryAsync(
                results,
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
            WeakReferenceMessenger.Default.Send(new TranslationHistoryChangedMessage());
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
        string TargetLanguageCode,
        string? ProviderName = null);

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
        if (!await CopyTextAsync(SourceText))
            return;

        _sourceCopyFeedbackCts?.Cancel();
        _sourceCopyFeedbackCts?.Dispose();
        var cts = _sourceCopyFeedbackCts = new CancellationTokenSource();

        IsSourceCopied = true;
        try
        {
            await Task.Delay(CopyFeedbackDuration, cts.Token);
            IsSourceCopied = false;
        }
        catch (OperationCanceledException)
        {
            // 新的复制操作重启了反馈计时
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddSourceToVocabulary))]
    public async Task AddSourceToVocabularyAsync(CancellationToken cancellationToken)
    {
        if (_eudicVocabularyService == null || string.IsNullOrWhiteSpace(SourceText))
            return;

        IsAddingToVocabulary = true;
        VocabularyStatus = _localizationService?.GetString("Popup_AddVocabularyRunning") ?? "正在加入欧路生词本...";

        try
        {
            var result = await _eudicVocabularyService.AddWordAsync(
                SourceText,
                null,
                cancellationToken);
            VocabularyStatus = result.Message;
        }
        finally
        {
            IsAddingToVocabulary = false;
        }
    }

    [RelayCommand]
    public async Task CopyTranslationAsync(TranslationResult? result)
    {
        if (result == null || !await CopyTextAsync(result.Result))
            return;

        await ShowCopyFeedbackAsync(result);
    }

    private async Task ShowCopyFeedbackAsync(TranslationResult result)
    {
        if (_copyFeedbackTokens.TryGetValue(result, out var previous))
            previous.Cancel();

        var cts = new CancellationTokenSource();
        _copyFeedbackTokens[result] = cts;

        result.IsCopied = true;
        try
        {
            await Task.Delay(CopyFeedbackDuration, cts.Token);
            result.IsCopied = false;
        }
        catch (OperationCanceledException)
        {
            // 新的复制操作重启了反馈计时
        }
        finally
        {
            if (_copyFeedbackTokens.TryGetValue(result, out var current) && ReferenceEquals(current, cts))
                _copyFeedbackTokens.Remove(result);
            cts.Dispose();
        }
    }

    [RelayCommand(CanExecute = nameof(CanSpeakSource))]
    public async Task SpeakSourceAsync()
    {
        await SpeakTextAsync(SourceText);
    }

    [RelayCommand]
    public async Task SpeakTranslationAsync(string? text)
    {
        await SpeakTextAsync(text);
    }

    [RelayCommand]
    public void StopSpeech()
    {
        // 取消令牌可以中断 LLM TTS 的生成阶段，Stop 只能停止已开始的播放
        _speechCts?.Cancel();
        _ttsService?.Stop();
    }

    [RelayCommand]
    public void RemoveTranslation(TranslationResult? result)
    {
        if (result != null)
            TranslationResults.Remove(result);
    }

    private async Task<bool> CopyTextAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || _clipboardService == null)
            return false;

        await _clipboardService.SetTextAsync(text);
        return true;
    }

    private async Task SpeakTextAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || _ttsService == null)
            return;

        // 抢占进行中的朗读，避免在 TTS 服务的串行队列里排队等待
        _speechCts?.Cancel();
        _speechCts?.Dispose();
        var cts = _speechCts = new CancellationTokenSource();

        _activeSpeechCount++;
        IsSpeaking = true;
        try
        {
            await _ttsService.SpeakAsync(text, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // 用户停止朗读
        }
        catch (Exception ex)
        {
            _logger?.ZLogError(ex, $"朗读文本失败: {ex.Message}");
        }
        finally
        {
            _activeSpeechCount--;
            if (_activeSpeechCount == 0)
                IsSpeaking = false;
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(SourceCharacterCountText));
    }

    [RelayCommand]
    public void UseTranslationAsSource(TranslationResult? result)
    {
        // 优先使用所点卡片的译文，未传参时回落到第一条成功结果
        var chosen = result is { IsSuccess: true } && !string.IsNullOrWhiteSpace(result.Result)
            ? result
            : TranslationResults.FirstOrDefault(r => r.IsSuccess && !string.IsNullOrWhiteSpace(r.Result));
        if (chosen?.Result == null)
        {
            _logger?.ZLogWarning($"没有可用译文，无法转为原文");
            return;
        }

        SourceText = chosen.Result;
        TranslationResults.Clear();

        if (SelectedTargetLanguage != null)
            SelectedSourceLanguage = SourceLanguages.FirstOrDefault(l => l.Code == SelectedTargetLanguage.Code) ??
                                     SelectedSourceLanguage;

        _logger?.ZLogInformation($"已将译文转为原文，长度: {SourceText.Length}");
    }

    [RelayCommand(CanExecute = nameof(CanSwapLanguages))]
    public void SwapLanguages()
    {
        if (!CanSwapLanguages || SelectedTargetLanguage == null)
            return;

        var tempCode = SelectedSourceLanguage?.Code;

        // 将当前目标语言设置为源语言
        SelectedSourceLanguage = SourceLanguages.FirstOrDefault(l => l.Code == SelectedTargetLanguage.Code);

        // 将原来的源语言设置为目标语言
        if (tempCode != null)
            SelectedTargetLanguage = TargetLanguages.FirstOrDefault(l => l.Code == tempCode);

        _logger.ZLogInformation($"已交换语言，源语言: {SelectedSourceLanguage?.Code}, 目标语言: {SelectedTargetLanguage?.Code}");
    }
}
