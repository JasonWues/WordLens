using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WordLens.Messages;
using WordLens.Models;
using WordLens.Services;
using ZLogger;

namespace WordLens.ViewModels;

public partial class TranslationHistoryViewModel : ViewModelBase
{
    private readonly ITranslationHistoryService _historyService;
    private readonly ILogger<TranslationHistoryViewModel> _logger;

    [ObservableProperty] private ObservableCollection<TranslationHistory> histories = new();

    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private bool isShowingFavoritesOnly;

    [ObservableProperty] private string searchKeyword = string.Empty;

    [ObservableProperty] private TranslationHistory? selectedHistory;

    [ObservableProperty] private int totalCount;

    public TranslationHistoryViewModel()
    {
        _historyService = null!;
        _logger = null!;
    }

    public TranslationHistoryViewModel(
        ITranslationHistoryService historyService,
        ILogger<TranslationHistoryViewModel> logger)
    {
        _historyService = historyService;
        _logger = logger;
    }

    /// <summary>
    /// 初始化并加载历史记录
    /// </summary>
    public async Task InitializeAsync()
    {
        await LoadHistoriesAsync();
    }

    /// <summary>
    /// 加载历史记录
    /// </summary>
    [RelayCommand]
    private async Task LoadHistoriesAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            _logger.ZLogInformation($"开始加载历史记录，搜索关键词: '{SearchKeyword}', 仅显示收藏: {IsShowingFavoritesOnly}");

            List<TranslationHistory> historyList;

            if (IsShowingFavoritesOnly)
            {
                // 只显示收藏的记录
                historyList = await _historyService.GetFavoritesAsync();
            }
            else if (!string.IsNullOrWhiteSpace(SearchKeyword))
            {
                // 搜索模式
                historyList = await _historyService.SearchAsync(SearchKeyword);
            }
            else
            {
                // 显示所有记录
                historyList = await _historyService.GetAllAsync();
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Histories.Clear();
                foreach (var history in historyList)
                {
                    Histories.Add(history);
                }
            });

            TotalCount = await _historyService.GetCountAsync();

            _logger.ZLogInformation($"历史记录加载完成，共 {Histories.Count} 条显示，总计 {TotalCount} 条");
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"加载历史记录失败: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 搜索历史记录
    /// </summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        await LoadHistoriesAsync();
    }

    /// <summary>
    /// 切换显示收藏
    /// </summary>
    [RelayCommand]
    private async Task ToggleShowFavoritesAsync()
    {
        IsShowingFavoritesOnly = !IsShowingFavoritesOnly;
        await LoadHistoriesAsync();
    }

    /// <summary>
    /// 删除指定的历史记录
    /// </summary>
    [RelayCommand]
    private async Task DeleteHistoryAsync(TranslationHistory? history)
    {
        if (history == null) return;

        try
        {
            _logger.ZLogInformation($"删除历史记录，ID: {history.Id}");
            await _historyService.DeleteAsync(history.Id);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Histories.Remove(history);
            });

            TotalCount = await _historyService.GetCountAsync();

            _logger.ZLogInformation($"历史记录删除成功");
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"删除历史记录失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 清空所有历史记录
    /// </summary>
    [RelayCommand]
    private async Task ClearAllAsync()
    {
        try
        {
            _logger.ZLogInformation($"清空所有历史记录");
            await _historyService.ClearAllAsync();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Histories.Clear();
            });

            TotalCount = 0;

            _logger.ZLogInformation($"所有历史记录已清空");
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"清空历史记录失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 切换收藏状态
    /// </summary>
    [RelayCommand]
    private async Task ToggleFavoriteAsync(TranslationHistory? history)
    {
        if (history == null) return;

        try
        {
            _logger.ZLogInformation($"切换收藏状态，ID: {history.Id}");
            await _historyService.ToggleFavoriteAsync(history.Id);

            // 更新UI中的状态
            history.IsFavorite = !history.IsFavorite;

            // 如果当前是仅显示收藏模式，且取消了收藏，则从列表中移除
            if (IsShowingFavoritesOnly && !history.IsFavorite)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Histories.Remove(history);
                });
            }

            _logger.ZLogInformation($"收藏状态已更新");
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"切换收藏状态失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 打开翻译窗口并重新翻译
    /// </summary>
    [RelayCommand]
    private void OpenTranslation(TranslationHistory? history)
    {
        if (history == null) return;

        try
        {
            _logger.ZLogInformation($"从历史记录打开翻译，ID: {history.Id}");

            // 发送消息触发翻译
            WeakReferenceMessenger.Default.Send(
                new TriggerTranslationMessage(history.SourceText),
                "text");

            _logger.ZLogInformation($"已触发翻译窗口");
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"打开翻译失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 刷新列表
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadHistoriesAsync();
    }

    /// <summary>
    /// 获取翻译结果的摘要文本（用于显示）
    /// </summary>
    public static string GetResultSummary(TranslationHistory history)
    {
        try
        {
            if (string.IsNullOrEmpty(history.ResultsJson))
                return "无翻译结果";

            var results = JsonSerializer.Deserialize<List<TranslationResultDto>>(history.ResultsJson);
            if (results == null || results.Count == 0)
                return "无翻译结果";

            // 返回第一个翻译结果的前100个字符
            var firstResult = results[0].Result ?? string.Empty;
            return firstResult.Length > 100 ? firstResult.Substring(0, 100) + "..." : firstResult;
        }
        catch
        {
            return "解析结果失败";
        }
    }

    /// <summary>
    /// 用于JSON反序列化的DTO
    /// </summary>
    private class TranslationResultDto
    {
        public string? ProviderName { get; set; }
        public string? Result { get; set; }
    }
}