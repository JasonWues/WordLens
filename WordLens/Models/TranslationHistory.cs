using System;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WordLens.Models;

/// <summary>
/// 翻译历史记录模型
/// </summary>
public class TranslationHistory : ObservableObject
{
    private bool _isFavorite;
    private string? _resultsJson;
    private string _resultSummary = TranslationHistorySummary.Create(null);

    /// <summary>
    /// 主键，自增
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 源文本
    /// </summary>
    public string SourceText { get; set; } = string.Empty;

    /// <summary>
    /// 源语言代码（如 "auto", "en", "zh" 等）
    /// </summary>
    public string SourceLanguage { get; set; } = string.Empty;

    /// <summary>
    /// 目标语言代码
    /// </summary>
    public string TargetLanguage { get; set; } = string.Empty;

    /// <summary>
    /// 翻译结果（JSON格式，存储多个翻译源的结果）
    /// </summary>
    public string? ResultsJson
    {
        get => _resultsJson;
        set
        {
            if (!SetProperty(ref _resultsJson, value))
                return;

            ResultSummary = TranslationHistorySummary.Create(value);
        }
    }

    public string ResultSummary
    {
        get => _resultSummary;
        private set => SetProperty(ref _resultSummary, value);
    }

    /// <summary>
    /// 翻译提供商名称（逗号分隔）
    /// </summary>
    public string? ProviderNames { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 是否收藏
    /// </summary>
    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetProperty(ref _isFavorite, value);
    }
}

internal static class TranslationHistorySummary
{
    public const int DefaultMaxLength = 160;

    public static string Create(string? resultsJson, int maxLength = DefaultMaxLength)
    {
        if (string.IsNullOrWhiteSpace(resultsJson))
            return "无翻译结果";

        try
        {
            var results = JsonSerializer.Deserialize(
                resultsJson,
                SourceGenerationContext.Default.ListTranslationHistoryResult);

            if (results == null || results.Count == 0)
                return "无翻译结果";

            foreach (var result in results)
            {
                var text = result.Result;
                if (!string.IsNullOrWhiteSpace(text))
                    return Truncate(text, maxLength);
            }

            return "无翻译结果";
        }
        catch (JsonException)
        {
            return "解析结果失败";
        }
    }

    private static string Truncate(string text, int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        return text.Length > maxLength
            ? text[..maxLength] + "..."
            : text;
    }
}
