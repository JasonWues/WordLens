using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using WordLens.Abstractions.Models;

namespace WordLens.Models;

public class AppSettings
{
    public HotkeyConfig Hotkey { get; set; } = HotkeyConfig.Default();

    public HotkeyConfig OcrHotkey { get; set; } = HotkeyConfig.DefaultOcr();

    /// <summary>
    ///     应用界面语言（用于UI显示）
    /// </summary>
    public string UILanguage { get; set; } = "zh-CN";

    /// <summary>
    ///     上次选择的翻译目标语言（记住用户偏好）
    /// </summary>
    public string LastTargetLanguage { get; set; } = "en";

    public bool StartWithSystem { get; set; } = false;

    /// <summary>
    ///     应用界面字体。留空时使用 Avalonia 默认字体。
    /// </summary>
    public string FontFamily { get; set; } = string.Empty;

    public string? SelectedProvider { get; set; } = "OpenAI";

    public string? SelectedOcrProvider { get; set; } = "OpenAI OCR";

    public List<ProviderConfig> OcrProviders { get; set; } = new()
    {
        CreateDefaultOcrProvider()
    };

    public ProxyConfig Proxy { get; set; } = new();

    public TtsConfig Tts { get; set; } = new();

    public LocalApiConfig LocalApi { get; set; } = new();

    public EudicVocabularyConfig EudicVocabulary { get; set; } = new();

    public TranslationPopupConfig TranslationPopup { get; set; } = new();

    /// <summary>
    ///     流式输出配置
    /// </summary>
    public StreamingConfig Streaming { get; set; } = new();

    public List<ProviderConfig> Providers { get; set; } = new()
    {
        new ProviderConfig
        {
            Name = "OpenAI",
            BaseUrl = "https://api.openai.com",
            ApiKey = null,
            Model = "gpt-4o-mini",
            Type = ProviderType.OpenAI,
            RequestArguments = string.Empty,
            SystemPromptTemplate = string.Empty,
            UserPromptTemplate = string.Empty
        }
    };

    public static ProviderConfig CreateDefaultOcrProvider()
    {
        return new ProviderConfig
        {
            Name = "OpenAI OCR",
            BaseUrl = "https://api.openai.com",
            ApiKey = null,
            Model = "gpt-4o-mini",
            Type = ProviderType.OpenAI,
            IsEnabled = true,
            RequestArguments = string.Empty,
            UserPromptTemplate = string.Empty
        };
    }

    public static ProviderConfig CreateDefaultLocalOcrProvider()
    {
        return new ProviderConfig
        {
            Name = GetDefaultLocalOcrProviderName(),
            Type = ProviderType.Local,
            IsEnabled = true,
            AllowManualModelInput = false,
            BaseUrl = string.Empty,
            ApiKey = null,
            Model = string.Empty,
            RequestArguments = string.Empty,
            UserPromptTemplate = string.Empty
        };
    }

    private static string GetDefaultLocalOcrProviderName()
    {
        if (OperatingSystem.IsWindows())
            return "Windows OCR";

        if (OperatingSystem.IsMacOS())
            return "macOS Vision OCR";

        if (OperatingSystem.IsLinux())
            return "Tesseract OCR";

        return "Local OCR";
    }

    public static TtsProviderConfig CreateDefaultTtsProvider()
    {
        return new TtsProviderConfig
        {
            Name = "OpenAI TTS",
            Type = TtsProviderType.Llm,
            BaseUrl = "https://api.openai.com",
            ApiKey = null,
            Model = "gpt-4o-mini-tts",
            Voice = "alloy",
            IsEnabled = false,
            RequestArguments = string.Empty,
            Speed = 1.0
        };
    }
}

public enum TranslationPopupPositionMode
{
    FollowMouse,
    RememberPosition
}

public class TranslationPopupConfig
{
    public TranslationPopupPositionMode PositionMode { get; set; } = TranslationPopupPositionMode.FollowMouse;

    public int? X { get; set; }

    public int? Y { get; set; }

    /// <summary>
    ///     翻译弹窗是否默认置顶。用户在弹窗内切换置顶时会写回此字段。
    /// </summary>
    public bool IsTopmost { get; set; }
}

public enum ProviderType
{
    OpenAI,
    DeepL,
    Local
}

public class LocalApiConfig
{
    public bool Enabled { get; set; }

    public int Port { get; set; } = 49631;

    public string Token { get; set; } = string.Empty;
}

public class EudicVocabularyConfig
{
    public bool Enabled { get; set; }

    /// <summary>
    ///     欧路 OpenAPI 授权 Token。存储时为加密格式：ENC::xxxxx
    /// </summary>
    public string Token { get; set; } = string.Empty;

    public string Language { get; set; } = "en";

    public string CategoryId { get; set; } = "0";

    public int Star { get; set; } = 1;
}

public class ProviderConfig : ObservableObject
{
    private string? _apiKey;
    private bool _allowManualModelInput = true;
    private ObservableCollection<ModelInfo>? _availableModels = new();
    private string _baseUrl = string.Empty;
    private bool _isEnabled = true;
    private string _model = string.Empty;
    private string _name = string.Empty;
    private string _requestArguments = string.Empty;
    private string _systemPromptTemplate = string.Empty;
    private ProviderType _type = ProviderType.OpenAI;
    private string _userPromptTemplate = string.Empty;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public ProviderType Type
    {
        get => _type;
        set
        {
            if (SetProperty(ref _type, value))
            {
                OnPropertyChanged(nameof(TypeDisplayName));
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    [JsonIgnore]
    public string TypeDisplayName => Type switch
    {
        ProviderType.OpenAI => "OpenAI 兼容",
        ProviderType.DeepL => "DeepL",
        ProviderType.Local => "本地",
        _ => Type.ToString()
    };

    [JsonIgnore]
    public string Summary => Type switch
    {
        ProviderType.Local => GetLocalOcrSummary(),
        ProviderType.DeepL => BaseUrl,
        _ => Model
    };

    private static string GetLocalOcrSummary()
    {
        if (OperatingSystem.IsWindows())
            return "Windows 系统 OCR";

        if (OperatingSystem.IsMacOS())
            return "macOS Vision OCR";

        if (OperatingSystem.IsLinux())
            return "Tesseract OCR";

        return "本地 OCR";
    }

    public string BaseUrl
    {
        get => _baseUrl;
        set
        {
            if (SetProperty(ref _baseUrl, value))
                OnPropertyChanged(nameof(Summary));
        }
    }

    /// <summary>
    ///     API密钥（存储时为加密格式：ENC::xxxxx）
    /// </summary>
    public string? ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    public string Model
    {
        get => _model;
        set
        {
            if (SetProperty(ref _model, value))
                OnPropertyChanged(nameof(Summary));
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    /// <summary>
    ///     附加到 LLM 请求体的 JSON 对象参数，例如 {"temperature":0.2}
    /// </summary>
    public string RequestArguments
    {
        get => _requestArguments;
        set => SetProperty(ref _requestArguments, value);
    }

    /// <summary>
    ///     系统提示词模板。留空时使用内置默认模板。
    /// </summary>
    public string SystemPromptTemplate
    {
        get => _systemPromptTemplate;
        set => SetProperty(ref _systemPromptTemplate, value);
    }

    /// <summary>
    ///     用户提示词模板。翻译源可使用 {text}、{sourceLanguage}、{targetLanguage}；OCR 源可使用 {languageCode}。
    /// </summary>
    public string UserPromptTemplate
    {
        get => _userPromptTemplate;
        set => SetProperty(ref _userPromptTemplate, value);
    }

    /// <summary>
    ///     是否允许手动输入模型名称（兼容模式）
    /// </summary>
    public bool AllowManualModelInput
    {
        get => _allowManualModelInput;
        set => SetProperty(ref _allowManualModelInput, value);
    }

    /// <summary>
    ///     可用模型列表（运行时缓存，不持久化）
    /// </summary>
    [JsonIgnore]
    public ObservableCollection<ModelInfo>? AvailableModels
    {
        get => _availableModels;
        set => SetProperty(ref _availableModels, value);
    }
}

/// <summary>
///     流式输出配置
/// </summary>
public class StreamingConfig
{
    /// <summary>
    ///     是否启用流式输出（默认启用）
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     打字机效果延迟（毫秒，0表示无延迟）
    /// </summary>
    public int TypewriterDelayMs { get; set; } = 0;

    /// <summary>
    ///     每次显示的字符数（1=逐字，0=实时无延迟）
    /// </summary>
    public int CharsPerUpdate { get; set; } = 1;
}

public class ProxyConfig
{
    public bool Enabled { get; set; } = false;
    public bool UseSystemProxy { get; set; } = false;
    public string Address { get; set; } = "http://127.0.0.1";
    public int Port { get; set; } = 8080;
    public bool UseAuthentication { get; set; } = false;
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public class TtsConfig
{
    public string? SelectedProvider { get; set; } = "OpenAI TTS";
    public List<TtsProviderConfig> Providers { get; set; } = new();
}

public enum TtsProviderType
{
    Local,
    Llm
}

public class TtsProviderConfig : ObservableObject
{
    private string? _apiKey;
    private string _baseUrl = string.Empty;
    private bool _isEnabled;
    private string _model = string.Empty;
    private string _name = string.Empty;
    private string _requestArguments = string.Empty;
    private double _speed = 1.0;
    private TtsProviderType _type = TtsProviderType.Llm;
    private string _voice = string.Empty;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public TtsProviderType Type
    {
        get => _type;
        set
        {
            if (SetProperty(ref _type, value))
            {
                OnPropertyChanged(nameof(TypeDisplayName));
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    [JsonIgnore]
    public string TypeDisplayName => Type switch
    {
        TtsProviderType.Local => "本地",
        TtsProviderType.Llm => "LLM",
        _ => Type.ToString()
    };

    [JsonIgnore]
    public string Summary => Type == TtsProviderType.Local
        ? Voice
        : Model;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public string BaseUrl
    {
        get => _baseUrl;
        set => SetProperty(ref _baseUrl, value);
    }

    public string? ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    public string Model
    {
        get => _model;
        set
        {
            if (SetProperty(ref _model, value))
                OnPropertyChanged(nameof(Summary));
        }
    }

    public string Voice
    {
        get => _voice;
        set
        {
            if (SetProperty(ref _voice, value))
                OnPropertyChanged(nameof(Summary));
        }
    }

    public double Speed
    {
        get => _speed;
        set => SetProperty(ref _speed, value);
    }

    public string RequestArguments
    {
        get => _requestArguments;
        set => SetProperty(ref _requestArguments, value);
    }
}
