using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SharpHook.Data;
using WordLens.Messages;
using WordLens.Models;
using WordLens.Services;
using WordLens.Services.Implementations;
using WordLens.Util;

namespace WordLens.ViewModels;

public partial class GeneralSettingsViewModel : ViewModelBase
{
    private readonly IStartupService _startupService;
    private readonly AvaloniaThemeService _themeService;
    private string _currentCapturingType = string.Empty;
    private HotkeyConfig _hotkeyConfig = HotkeyConfig.Default();
    private HotkeyConfig _ocrHotkeyConfig = HotkeyConfig.DefaultOcr();

    [ObservableProperty] private int charsPerUpdate = 1;
    [ObservableProperty] private string hotkeyDisplay = "Ctrl+Shift+T";
    [ObservableProperty] private bool isCapturingHotkey;
    [ObservableProperty] private bool isStartupSupported = true;
    [ObservableProperty] private string ocrHotkeyDisplay = "Ctrl+Shift+W";
    [ObservableProperty] private bool startWithSystem;
    [ObservableProperty] private bool streamingEnabled = true;
    [ObservableProperty] private TranslationPopupPositionMode translationPopupPositionMode = TranslationPopupPositionMode.FollowMouse;
    [ObservableProperty] private int typewriterDelayMs;
    [ObservableProperty] private string uiLanguage = "zh-CN";

    public GeneralSettingsViewModel(
        AvaloniaThemeService themeService,
        IStartupService startupService)
    {
        _themeService = themeService;
        _startupService = startupService;

        WeakReferenceMessenger.Default.Register<CapturingKeyMessage>(this, (r, m) =>
        {
            if (!IsCapturingHotkey)
                return;

            m.Handled = true;
            CaptureKey(m.KeyCode, m.Modifiers);
        });
    }

    public List<LanguageOption> AvailableUILanguages { get; } = new()
    {
        new LanguageOption("zh-CN", "简体中文"),
        new LanguageOption("en", "English"),
        new LanguageOption("ja", "日本語")
    };

    public List<TranslationPopupPositionModeOption> AvailableTranslationPopupPositionModes { get; } = new()
    {
        new TranslationPopupPositionModeOption(TranslationPopupPositionMode.FollowMouse, "跟随鼠标"),
        new TranslationPopupPositionModeOption(TranslationPopupPositionMode.RememberPosition, "记住位置")
    };

    public void Load(AppSettings settings)
    {
        UiLanguage = settings.UILanguage;
        _hotkeyConfig = CloneHotkeyConfig(settings.Hotkey);
        _ocrHotkeyConfig = CloneHotkeyConfig(settings.OcrHotkey);
        IsStartupSupported = _startupService.IsSupported;
        StartWithSystem = IsStartupSupported ? _startupService.IsEnabled() : settings.StartWithSystem;
        StreamingEnabled = settings.Streaming.Enabled;
        TypewriterDelayMs = settings.Streaming.TypewriterDelayMs;
        CharsPerUpdate = settings.Streaming.CharsPerUpdate;
        TranslationPopupPositionMode = settings.TranslationPopup.PositionMode;
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
