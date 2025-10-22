using CommunityToolkit.Mvvm.ComponentModel;

namespace WordLens.Models
{
    /// <summary>
    /// 翻译结果模型，用于存储单个翻译源的翻译结果
    /// </summary>
    public partial class TranslationResult : ObservableObject
    {
        /// <summary>
        /// 翻译源名称
        /// </summary>
        [ObservableProperty]
        private string providerName = string.Empty;

        /// <summary>
        /// 翻译结果文本
        /// </summary>
        [ObservableProperty]
        private string? result;

        /// <summary>
        /// 是否翻译成功
        /// </summary>
        [ObservableProperty]
        private bool isSuccess;

        /// <summary>
        /// 错误信息（如果失败）
        /// </summary>
        [ObservableProperty]
        private string? errorMessage;

        /// <summary>
        /// 是否正在加载
        /// </summary>
        [ObservableProperty]
        private bool isLoading;
    }
}