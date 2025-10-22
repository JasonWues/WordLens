using System.Collections.Generic;
using System.Linq;

namespace WordLens.Models
{
    /// <summary>
    /// 语言信息模型，包含语言代码和显示名称
    /// </summary>
    public class LanguageInfo
    {
        /// <summary>
        /// 语言代码（如：zh-CN, en, ja）
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 本地化显示名称（如：简体中文）
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 原生语言名称（如：简体中文）
        /// </summary>
        public string NativeName { get; set; } = string.Empty;

        public LanguageInfo()
        {
        }

        public LanguageInfo(string code, string displayName, string nativeName)
        {
            Code = code;
            DisplayName = displayName;
            NativeName = nativeName;
        }

        public override string ToString() => $"{DisplayName} ({NativeName})";

        /// <summary>
        /// 获取常用语言列表（包含自动检测选项）
        /// </summary>
        public static List<LanguageInfo> GetCommonLanguages() => new()
        {
            new("auto", "自动检测", "Auto Detect"),
            new("zh-CN", "简体中文", "简体中文"),
            new("zh-TW", "繁体中文", "繁體中文"),
            new("en", "英语", "English"),
            new("ja", "日语", "日本語"),
            new("ko", "韩语", "한국어"),
            new("fr", "法语", "Français"),
            new("de", "德语", "Deutsch"),
            new("es", "西班牙语", "Español"),
            new("ru", "俄语", "Русский"),
            new("ar", "阿拉伯语", "العربية"),
            new("pt", "葡萄牙语", "Português"),
            new("it", "意大利语", "Italiano"),
        };

        /// <summary>
        /// 获取目标语言列表（不包含自动检测）
        /// </summary>
        public static List<LanguageInfo> GetTargetLanguages() =>
            GetCommonLanguages().Where(l => l.Code != "auto").ToList();
    }
}