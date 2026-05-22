using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WordLens.Models;

/// <summary>
/// 翻译历史记录模型
/// </summary>
public class TranslationHistory : ObservableObject
{
    private bool _isFavorite;

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
    public string? ResultsJson { get; set; }

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
