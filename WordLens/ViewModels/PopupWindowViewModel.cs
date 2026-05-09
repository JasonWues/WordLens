using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
    private readonly ISettingsService _settingsService;
    private readonly ITranslationHistoryService _historyService;
    private readonly TranslationService _translationService;
    private readonly Task _languageInitializationTask = Task.CompletedTask;
    private bool _isInitializingLanguages;

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
        _translationService = null!;
        _settingsService = null!;
        _historyService = null!;
        _logger = null!;

        InitializeLanguageCollections();
    }

    public PopupWindowViewModel(
        TranslationService translationService,
        ISettingsService settingsService,
        ITranslationHistoryService historyService,
        ILogger<PopupWindowViewModel> logger)
    {
        _translationService = translationService;
        _settingsService = settingsService;
        _historyService = historyService;
        _logger = logger;

        // 初始化语言列表
        _languageInitializationTask = InitializeLanguagesAsync();
    }

    public bool CanCopySource => !string.IsNullOrWhiteSpace(SourceText);

    public bool HasTranslationResults => TranslationResults.Count > 0;

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
    }

    partial void OnTranslationResultsChanged(ObservableCollection<TranslationResult> value)
    {
        OnPropertyChanged(nameof(HasTranslationResults));
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

            // 保存到历史记录
            await SaveToHistoryAsync(results, sourceText, sourceLanguageCode, targetLanguageCode);
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
            var resultsJson = System.Text.Json.JsonSerializer.Serialize(
                successResults.Select(r => new { r.ProviderName, r.Result }).ToList());

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
