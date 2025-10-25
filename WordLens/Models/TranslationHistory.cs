using System;
using SQLite;

namespace WordLens.Models;

/// <summary>
/// 翻译历史记录模型
/// </summary>
[Table("TranslationHistory")]
public class TranslationHistory
{
    /// <summary>
    /// 主键，自增
    /// </summary>
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// 源文本
    /// </summary>
    [NotNull, MaxLength(10000)]
    public string SourceText { get; set; } = string.Empty;

    /// <summary>
    /// 源语言代码（如 "auto", "en", "zh" 等）
    /// </summary>
    [NotNull, MaxLength(10)]
    public string SourceLanguage { get; set; } = string.Empty;

    /// <summary>
    /// 目标语言代码
    /// </summary>
    [NotNull, MaxLength(10)]
    public string TargetLanguage { get; set; } = string.Empty;

    /// <summary>
    /// 翻译结果（JSON格式，存储多个翻译源的结果）
    /// </summary>
    [MaxLength(50000)]
    public string? ResultsJson { get; set; }

    /// <summary>
    /// 翻译提供商名称（逗号分隔）
    /// </summary>
    [MaxLength(500)]
    public string? ProviderNames { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [NotNull, Indexed]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 是否收藏
    /// </summary>
    public bool IsFavorite { get; set; }
}