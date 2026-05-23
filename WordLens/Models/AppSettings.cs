using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using SharpHook.Data;

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

    public string? SelectedProvider { get; set; } = "OpenAI";

    public string? SelectedOcrProvider { get; set; } = "OpenAI OCR";

    public List<ProviderConfig> OcrProviders { get; set; } = new()
    {
        CreateDefaultOcrProvider()
    };

    public ProxyConfig Proxy { get; set; } = new();

    public TtsConfig Tts { get; set; } = new();

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
}

public class HotkeyConfig
{
    public EventMask Modifiers { get; set; }
    public KeyCode Key { get; set; }

    public static HotkeyConfig Default()
    {
        return new HotkeyConfig
        {
            Modifiers = EventMask.LeftCtrl | EventMask.LeftShift,
            Key = KeyCode.VcT
        };
    }

    public static HotkeyConfig DefaultOcr()
    {
        return new HotkeyConfig
        {
            Modifiers = EventMask.LeftCtrl | EventMask.LeftShift,
            Key = KeyCode.VcW
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
}

public enum ProviderType
{
    OpenAI
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
        set => SetProperty(ref _type, value);
    }

    public string BaseUrl
    {
        get => _baseUrl;
        set => SetProperty(ref _baseUrl, value);
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
        set => SetProperty(ref _model, value);
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
    public bool Enabled { get; set; } = false;
    public TtsModelType ModelType { get; set; } = TtsModelType.Vits;
    public string ModelPath { get; set; } = string.Empty;
    public string TokensPath { get; set; } = string.Empty;
    public string VoicesPath { get; set; } = string.Empty;
    public string DataDir { get; set; } = string.Empty;
    public string LexiconPath { get; set; } = string.Empty;
    public string DictDir { get; set; } = string.Empty;
    public string VocoderPath { get; set; } = string.Empty;
    public string RuleFsts { get; set; } = string.Empty;
    public string RuleFars { get; set; } = string.Empty;
    public string Provider { get; set; } = "cpu";
    public int NumThreads { get; set; } = 2;
    public int SpeakerId { get; set; } = 0;
    public double Speed { get; set; } = 1.0;
}

public enum TtsModelType
{
    Vits,
    Kokoro,
    Matcha
}
