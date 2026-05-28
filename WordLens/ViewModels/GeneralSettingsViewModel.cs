using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SharpHook.Data;
using WordLens.Abstractions.Models;
using WordLens.Abstractions.Services;
using WordLens.Infrastructure.Avalonia;
using WordLens.Infrastructure.Security;
using WordLens.Messages;
using WordLens.Models;
using WordLens.Services;
using WordLens.Util;

namespace WordLens.ViewModels;

public partial class GeneralSettingsViewModel : ViewModelBase
{
    private readonly IStartupService _startupService;
    private readonly AvaloniaThemeService _themeService;
    private readonly EncryptionService _encryptionService;
    private string _currentCapturingType = string.Empty;
    private HotkeyConfig _hotkeyConfig = HotkeyConfig.Default();
    private HotkeyConfig _ocrHotkeyConfig = HotkeyConfig.DefaultOcr();

    [ObservableProperty] private int charsPerUpdate = 1;
    [ObservableProperty] private string eudicCategoryId = "0";
    [ObservableProperty] private bool eudicEnabled;
    [ObservableProperty] private string eudicLanguage = "en";
    [ObservableProperty] private int eudicStar = 1;
    [ObservableProperty] private string eudicToken = string.Empty;
    [ObservableProperty] private string fontFamily = string.Empty;
    [ObservableProperty] private string hotkeyDisplay = "Ctrl+Shift+T";
    [ObservableProperty] private bool isCapturingHotkey;
    [ObservableProperty] private bool isStartupSupported = true;
    [ObservableProperty] private bool localApiEnabled;
    [ObservableProperty] private int localApiPort = 49631;
    [ObservableProperty] private string localApiToken = string.Empty;
    [ObservableProperty] private string ocrHotkeyDisplay = "Ctrl+Shift+W";
    [ObservableProperty] private bool startWithSystem;
    [ObservableProperty] private bool streamingEnabled = true;
    [ObservableProperty] private TranslationPopupPositionMode translationPopupPositionMode = TranslationPopupPositionMode.FollowMouse;
    [ObservableProperty] private int typewriterDelayMs;
    [ObservableProperty] private string uiLanguage = "zh-CN";

    public GeneralSettingsViewModel(
        AvaloniaThemeService themeService,
        IStartupService startupService,
        EncryptionService encryptionService)
    {
        _themeService = themeService;
        _startupService = startupService;
        _encryptionService = encryptionService;

        WeakReferenceMessenger.Default.Register<CapturingKeyMessage>(this, (r, m) =>
        {
            if (!IsCapturingHotkey)
                return;

            m.Handled = true;
            CaptureKey(m.KeyCode, m.Modifiers);
        });
    }

    public List<FontFamilyOption> AvailableFontFamilies { get; } = CreateFontFamilyOptions();

    public List<LanguageOption> AvailableUILanguages { get; } = new()
    {
        new LanguageOption("zh-CN", "简体中文"),
        new LanguageOption("en", "English"),
        new LanguageOption("ja", "日本語")
    };

    public List<EudicLanguageOption> AvailableEudicLanguages { get; } = new()
    {
        new EudicLanguageOption("en", "英语"),
        new EudicLanguageOption("fr", "法语"),
        new EudicLanguageOption("de", "德语"),
        new EudicLanguageOption("es", "西语")
    };

    public List<TranslationPopupPositionModeOption> AvailableTranslationPopupPositionModes { get; } = new()
    {
        new TranslationPopupPositionModeOption(TranslationPopupPositionMode.FollowMouse, "跟随鼠标"),
        new TranslationPopupPositionModeOption(TranslationPopupPositionMode.RememberPosition, "记住位置")
    };

    public void Load(AppSettings settings)
    {
        UiLanguage = settings.UILanguage;
        FontFamily = settings.FontFamily ?? string.Empty;
        _hotkeyConfig = CloneHotkeyConfig(settings.Hotkey);
        _ocrHotkeyConfig = CloneHotkeyConfig(settings.OcrHotkey);
        IsStartupSupported = _startupService.IsSupported;
        StartWithSystem = IsStartupSupported ? _startupService.IsEnabled() : settings.StartWithSystem;
        StreamingEnabled = settings.Streaming.Enabled;
        TypewriterDelayMs = settings.Streaming.TypewriterDelayMs;
        CharsPerUpdate = settings.Streaming.CharsPerUpdate;
        TranslationPopupPositionMode = settings.TranslationPopup.PositionMode;
        LocalApiEnabled = settings.LocalApi.Enabled;
        LocalApiPort = settings.LocalApi.Port;
        LocalApiToken = settings.LocalApi.Token;
        EudicEnabled = settings.EudicVocabulary.Enabled;
        EudicToken = string.IsNullOrWhiteSpace(settings.EudicVocabulary.Token)
            ? string.Empty
            : _encryptionService.IsEncrypted(settings.EudicVocabulary.Token)
                ? _encryptionService.Decrypt(settings.EudicVocabulary.Token)
                : settings.EudicVocabulary.Token;
        EudicLanguage = settings.EudicVocabulary.Language;
        EudicCategoryId = settings.EudicVocabulary.CategoryId;
        EudicStar = settings.EudicVocabulary.Star;
        UpdateHotkeyDisplay();
        UpdateOcrHotkeyDisplay();
    }

    public void ApplyStartupSetting()
    {
        if (_startupService.IsSupported)
            _startupService.SetEnabled(StartWithSystem);
    }

    public HotkeyConfig BuildHotkeyConfig()
    {
        return CloneHotkeyConfig(_hotkeyConfig);
    }

    public HotkeyConfig BuildOcrHotkeyConfig()
    {
        return CloneHotkeyConfig(_ocrHotkeyConfig);
    }

    public StreamingConfig BuildStreamingConfig()
    {
        return new StreamingConfig
        {
            Enabled = StreamingEnabled,
            TypewriterDelayMs = TypewriterDelayMs,
            CharsPerUpdate = CharsPerUpdate
        };
    }

    public TranslationPopupConfig BuildTranslationPopupConfig(TranslationPopupConfig? current)
    {
        return new TranslationPopupConfig
        {
            PositionMode = TranslationPopupPositionMode,
            X = current?.X,
            Y = current?.Y
        };
    }

    public LocalApiConfig BuildLocalApiConfig()
    {
        return new LocalApiConfig
        {
            Enabled = LocalApiEnabled,
            Port = LocalApiPort,
            Token = string.IsNullOrWhiteSpace(LocalApiToken) ? GenerateLocalApiToken() : LocalApiToken
        };
    }

    public EudicVocabularyConfig BuildEudicVocabularyConfig()
    {
        return new EudicVocabularyConfig
        {
            Enabled = EudicEnabled,
            Token = EudicToken.Trim(),
            Language = string.IsNullOrWhiteSpace(EudicLanguage) ? "en" : EudicLanguage.Trim().ToLowerInvariant(),
            CategoryId = string.IsNullOrWhiteSpace(EudicCategoryId) ? "0" : EudicCategoryId.Trim(),
            Star = Math.Clamp(EudicStar, 0, 5)
        };
    }

    [RelayCommand]
    private void ResetFontFamily()
    {
        FontFamily = string.Empty;
    }

    public static HotkeyConfig CloneHotkeyConfig(HotkeyConfig config)
    {
        return new HotkeyConfig
        {
            Modifiers = config.Modifiers,
            Key = config.Key
        };
    }

    partial void OnUiLanguageChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
            _themeService.ApplyLocale(value);
    }

    partial void OnFontFamilyChanged(string value)
    {
        _themeService.ApplyFontFamily(value);
    }

    [RelayCommand]
    private void RegenerateLocalApiToken()
    {
        LocalApiToken = GenerateLocalApiToken();
    }

    [RelayCommand]
    private void StartCaptureHotkey(string type)
    {
        IsCapturingHotkey = true;
        _currentCapturingType = type;

        if (type == "ocr")
            OcrHotkeyDisplay = "请按下快捷键...";
        else
            HotkeyDisplay = "请按下快捷键...";
    }

    private void CaptureKey(KeyCode keyCode, EventMask modifiers)
    {
        if (!IsCapturingHotkey || keyCode == KeyCode.VcUndefined)
            return;

        var newConfig = new HotkeyConfig
        {
            Modifiers = modifiers,
            Key = keyCode
        };

        if (_currentCapturingType == "ocr")
        {
            _ocrHotkeyConfig = newConfig;
            UpdateOcrHotkeyDisplay();
        }
        else
        {
            _hotkeyConfig = newConfig;
            UpdateHotkeyDisplay();
        }

        IsCapturingHotkey = false;
        _currentCapturingType = string.Empty;
    }

    private void UpdateHotkeyDisplay()
    {
        HotkeyDisplay = FormatHotkey(_hotkeyConfig);
    }

    private void UpdateOcrHotkeyDisplay()
    {
        OcrHotkeyDisplay = FormatHotkey(_ocrHotkeyConfig);
    }

    private static string FormatHotkey(HotkeyConfig config)
    {
        var parts = new List<string>();

        if (config.Modifiers.HasFlag(EventMask.LeftCtrl) || config.Modifiers.HasFlag(EventMask.RightCtrl))
            parts.Add("Ctrl");
        if (config.Modifiers.HasFlag(EventMask.LeftShift) || config.Modifiers.HasFlag(EventMask.RightShift))
            parts.Add("Shift");
        if (config.Modifiers.HasFlag(EventMask.LeftAlt) || config.Modifiers.HasFlag(EventMask.RightAlt))
            parts.Add("Alt");
        if (config.Modifiers.HasFlag(EventMask.LeftMeta) || config.Modifiers.HasFlag(EventMask.RightMeta))
            parts.Add("Win");

        parts.Add(KeyCodeUtil.GetKeyName(config.Key));
        return string.Join("+", parts);
    }

    private static string GenerateLocalApiToken()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static List<FontFamilyOption> CreateFontFamilyOptions()
    {
        var options = new List<FontFamilyOption>
        {
            new(string.Empty, $"默认字体 ({AvaloniaThemeService.GetDefaultFontDisplayName()})")
        };

        options.AddRange(AvaloniaThemeService.GetSystemFontFamilyNames()
            .Select(static fontFamily => new FontFamilyOption(fontFamily, fontFamily)));

        return options;
    }
}

public class LanguageOption
{
    public LanguageOption(string code, string displayName)
    {
        Code = code;
        DisplayName = displayName;
    }

    public string Code { get; set; }
    public string DisplayName { get; set; }
}

public class EudicLanguageOption
{
    public EudicLanguageOption(string code, string displayName)
    {
        Code = code;
        DisplayName = displayName;
    }

    public string Code { get; set; }
    public string DisplayName { get; set; }
}

public class TranslationPopupPositionModeOption
{
    public TranslationPopupPositionModeOption(TranslationPopupPositionMode mode, string displayName)
    {
        Mode = mode;
        DisplayName = displayName;
    }

    public TranslationPopupPositionMode Mode { get; set; }
    public string DisplayName { get; set; }
}

public class FontFamilyOption
{
    public FontFamilyOption(string family, string displayName)
    {
        Family = family;
        DisplayName = displayName;
    }

    public string Family { get; set; }
    public string DisplayName { get; set; }
}
