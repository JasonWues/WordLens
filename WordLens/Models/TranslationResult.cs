using CommunityToolkit.Mvvm.ComponentModel;

namespace WordLens.Models;

/// <summary>
///     翻译结果模型，用于存储单个翻译源的翻译结果
/// </summary>
public partial class TranslationResult : ObservableObject
{
    public bool HasVisibleResult => !string.IsNullOrEmpty(Result) && (IsLoading || IsSuccess);

    public bool IsError => !IsLoading && !IsSuccess;

    public bool HasDuration => DurationMs is > 0;

    /// <summary>
    ///     错误信息（如果失败）
    /// </summary>
    [ObservableProperty] private string? errorMessage;

    /// <summary>
    ///     是否正在加载
    /// </summary>
    [ObservableProperty] private bool isLoading;

    /// <summary>
    ///     是否翻译成功
    /// </summary>
    [ObservableProperty] private bool isSuccess;

    /// <summary>
    ///     翻译源名称
    /// </summary>
    [ObservableProperty] private string providerName = string.Empty;

    /// <summary>
    ///     翻译结果文本
    /// </summary>
    [ObservableProperty] private string? result;

    /// <summary>
    ///     复制成功反馈状态（仅 UI 状态，不参与历史序列化）
    /// </summary>
    [ObservableProperty] private bool isCopied;

    /// <summary>
    ///     本次翻译耗时（毫秒）。流式场景为从启动到完成/失败的总时长。
    /// </summary>
    [ObservableProperty] private long? durationMs;

    /// <summary>
    ///     面向 UI 的耗时文本，例如 "1.2s" / "320ms"。
    /// </summary>
    public string DurationText
    {
        get
        {
            if (DurationMs is not > 0)
                return string.Empty;

            return DurationMs.Value >= 1000
                ? $"{DurationMs.Value / 1000.0:0.0}s"
                : $"{DurationMs.Value}ms";
        }
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(HasVisibleResult));
        OnPropertyChanged(nameof(IsError));
    }

    partial void OnIsSuccessChanged(bool value)
    {
        OnPropertyChanged(nameof(HasVisibleResult));
        OnPropertyChanged(nameof(IsError));
    }

    partial void OnResultChanged(string? value)
    {
        OnPropertyChanged(nameof(HasVisibleResult));
    }

    partial void OnDurationMsChanged(long? value)
    {
        OnPropertyChanged(nameof(HasDuration));
        OnPropertyChanged(nameof(DurationText));
    }
}
