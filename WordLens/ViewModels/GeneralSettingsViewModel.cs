using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using SharpHook.Data;
using WordLens.Abstractions.Models;
using WordLens.Abstractions.Services;
using WordLens.Infrastructure.Avalonia;
using WordLens.Infrastructure.Security;
using WordLens.Messages;
using WordLens.Models;
using WordLens.Services;
using WordLens.Util;
using ZLogger;

namespace WordLens.ViewModels;

public partial class GeneralSettingsViewModel : ViewModelBase
{
    private readonly IStartupService _startupService;
    private readonly IBackupService _backupService;
    private readonly IPathPickerService _pathPickerService;
    private readonly AvaloniaThemeService _themeService;
    private readonly ILocalizationService _localizationService;
    private readonly EncryptionService _encryptionService;
    private readonly ILogger<GeneralSettingsViewModel> _logger;
    private string _currentCapturingType = string.Empty;
    private HotkeyConfig _hotkeyConfig = HotkeyConfig.Default();
    private HotkeyConfig _ocrHotkeyConfig = HotkeyConfig.DefaultOcr();

    [ObservableProperty] private string backupStatus = string.Empty;
    [ObservableProperty] private int charsPerUpdate = 1;
    [ObservableProperty] private string eudicCategoryId = "0";
    [ObservableProperty] private bool eudicEnabled;
    [ObservableProperty] private string eudicLanguage = "en";
    [ObservableProperty] private int eudicStar = 1;
    [ObservableProperty] private string eudicToken = string.Empty;
    [ObservableProperty] private string fontFamily = string.Empty;
    [ObservableProperty] private string hotkeyDisplay = "Ctrl+Shift+T";
    [ObservableProperty] private bool isCapturingHotkey;
    [ObservableProperty] private bool isBackupRunning;
    [ObservableProperty] private bool isRestoreRunning;
    [ObservableProperty] private bool isStartupSupported = true;
    [ObservableProperty] private bool localApiEnabled;
    [ObservableProperty] private int localApiPort = 49631;
    [ObservableProperty] private string localApiToken = string.Empty;
    [ObservableProperty] private string ocrHotkeyDisplay = "Ctrl+Shift+W";
    [ObservableProperty] private string restoreStatus = string.Empty;
    [ObservableProperty] private bool startWithSystem;
    [ObservableProperty] private bool streamingEnabled = true;
    [ObservableProperty] private TranslationPopupPositionMode translationPopupPositionMode = TranslationPopupPositionMode.FollowMouse;
    [ObservableProperty] private int typewriterDelayMs;
    [ObservableProperty] private string uiLanguage = "zh-CN";

    public GeneralSettingsViewModel(
        AvaloniaThemeService themeService,
        ILocalizationService localizationService,
        IStartupService startupService,
        EncryptionService encryptionService,
        IBackupService backupService,
        IPathPickerService pathPickerService,
        ILogger<GeneralSettingsViewModel> logger)
    {
        _themeService = themeService;
        _localizationService = localizationService;
        _startupService = startupService;
        _encryptionService = encryptionService;
        _backupService = backupService;
        _pathPickerService = pathPickerService;
        _logger = logger;
        AvailableFontFamilies = CreateFontFamilyOptions(_localizationService);
        RefreshLocalizedOptions();
        _localizationService.CultureChanged += OnCultureChanged;

        WeakReferenceMessenger.Default.Register<CapturingKeyMessage>(this, (r, m) =>
        {
            if (!IsCapturingHotkey)
                return;

            m.Handled = true;
            CaptureKey(m.KeyCode, m.Modifiers);
        });
    }

    public List<FontFamilyOption> AvailableFontFamilies { get; }

    public bool HasBackupStatus => !string.IsNullOrWhiteSpace(BackupStatus);

    public bool HasRestoreStatus => !string.IsNullOrWhiteSpace(RestoreStatus);

    public bool IsBackupOperationIdle => !IsBackupRunning && !IsRestoreRunning;

    public List<LanguageOption> AvailableUILanguages { get; } = new()
    {
        new("zh-CN", "General_Language_Chinese"),
        new("en", "General_Language_English"),
        new("ja", "General_Language_Japanese")
    };

    public List<EudicLanguageOption> AvailableEudicLanguages { get; } = new()
    {
        new("en", "General_EudicLanguage_English"),
        new("fr", "General_EudicLanguage_French"),
        new("de", "General_EudicLanguage_German"),
        new("es", "General_EudicLanguage_Spanish")
    };

    public List<TranslationPopupPositionModeOption> AvailableTranslationPopupPositionModes { get; } = new()
    {
        new(TranslationPopupPositionMode.FollowMouse, "General_PopupPosition_FollowMouse"),
        new(TranslationPopupPositionMode.RememberPosition, "General_PopupPosition_RememberPosition")
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
            Y = current?.Y,
            IsTopmost = current?.IsTopmost ?? false
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

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        if (!IsBackupOperationIdle)
            return;

        var suggestedFileName = $"WordLens-backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip";
        BackupStatus = _localizationService.GetString("General_Backup_ChooseDestination");

        var destinationPath = await _pathPickerService.PickSaveFileAsync(
            _localizationService.GetString("General_Backup_SaveDialogTitle"),
            suggestedFileName,
            new[] { "*.zip" });

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            BackupStatus = _localizationService.GetString("General_Backup_Canceled");
            return;
        }

        try
        {
            IsBackupRunning = true;
            BackupStatus = _localizationService.GetString("General_Backup_Running");
            var result = await _backupService.CreateBackupAsync(destinationPath);
            BackupStatus = _localizationService.GetString(
                "General_Backup_DoneFormat",
                result.FileCount,
                FormatFileSize(result.SizeBytes));
        }
        catch (Exception ex)
        {
            BackupStatus = _localizationService.GetString("General_Backup_FailedFormat", ex.Message);
            _logger.ZLogError(ex, $"创建备份失败: {ex.Message}");
        }
        finally
        {
            IsBackupRunning = false;
        }
    }

    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        if (!IsBackupOperationIdle)
            return;

        RestoreStatus = _localizationService.GetString("General_Restore_ChooseSource");

        var sourcePath = await _pathPickerService.PickFileAsync(
            _localizationService.GetString("General_Restore_OpenDialogTitle"),
            new[] { "*.zip" });

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            RestoreStatus = _localizationService.GetString("General_Restore_Canceled");
            return;
        }

        try
        {
            IsRestoreRunning = true;
            RestoreStatus = _localizationService.GetString("General_Restore_Running");
            var result = await _backupService.RestoreBackupAsync(sourcePath);
            RestoreStatus = _localizationService.GetString(
                "General_Restore_DoneFormat",
                result.FileCount,
                System.IO.Path.GetFileName(result.PreRestoreBackupPath));
            WeakReferenceMessenger.Default.Send(new BackupRestoredMessage(result)); 
        }
        catch (Exception ex)
        {
            RestoreStatus = _localizationService.GetString("General_Restore_FailedFormat", ex.Message);
            _logger.ZLogError(ex, $"恢复备份失败: {ex.Message}");
        }
        finally
        {
            IsRestoreRunning = false;
        }
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
            _localizationService.ApplyCulture(value);
    }

    partial void OnFontFamilyChanged(string value)
    {
        _themeService.ApplyFontFamily(value);
    }

    partial void OnBackupStatusChanged(string value)
    {
        OnPropertyChanged(nameof(HasBackupStatus));
    }

    partial void OnRestoreStatusChanged(string value)
    {
        OnPropertyChanged(nameof(HasRestoreStatus));
    }

    partial void OnIsBackupRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBackupOperationIdle));
    }

    partial void OnIsRestoreRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBackupOperationIdle));
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
            OcrHotkeyDisplay = _localizationService.GetString("General_Hotkey_CapturePlaceholder");
        else
            HotkeyDisplay = _localizationService.GetString("General_Hotkey_CapturePlaceholder");
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

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";

        var kilobytes = bytes / 1024d;
        if (kilobytes < 1024)
            return $"{kilobytes:F1} KB";

        var megabytes = kilobytes / 1024d;
        return $"{megabytes:F1} MB";
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        RefreshLocalizedOptions();

        if (!IsCapturingHotkey)
            return;

        if (_currentCapturingType == "ocr")
            OcrHotkeyDisplay = _localizationService.GetString("General_Hotkey_CapturePlaceholder");
        else
            HotkeyDisplay = _localizationService.GetString("General_Hotkey_CapturePlaceholder");
    }

    private void RefreshLocalizedOptions()
    {
        foreach (var option in AvailableUILanguages)
            option.DisplayName = _localizationService.GetString(option.ResourceKey);

        foreach (var option in AvailableEudicLanguages)
            option.DisplayName = _localizationService.GetString(option.ResourceKey);

        foreach (var option in AvailableTranslationPopupPositionModes)
            option.DisplayName = _localizationService.GetString(option.ResourceKey);

        var defaultFontOption = AvailableFontFamilies.FirstOrDefault(static option => option.Family.Length == 0);
        if (defaultFontOption != null)
        {
            defaultFontOption.DisplayName = _localizationService.GetString(
                "General_DefaultFontFormat",
                AvaloniaThemeService.GetDefaultFontDisplayName());
        }
    }

    private static List<FontFamilyOption> CreateFontFamilyOptions(ILocalizationService localizationService)
    {
        var options = new List<FontFamilyOption>
        {
            new(
                string.Empty,
                localizationService.GetString(
                    "General_DefaultFontFormat",
                    AvaloniaThemeService.GetDefaultFontDisplayName()))
        };

        options.AddRange(AvaloniaThemeService.GetSystemFontFamilyNames()
            .Select(static fontFamily => new FontFamilyOption(fontFamily, fontFamily)));

        return options;
    }
}

public class LanguageOption : ObservableObject
{
    private string _displayName;

    public LanguageOption(string code, string resourceKey)
    {
        Code = code;
        ResourceKey = resourceKey;
        _displayName = resourceKey;
    }

    public string Code { get; set; }
    public string ResourceKey { get; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }
}

public class EudicLanguageOption : ObservableObject
{
    private string _displayName;

    public EudicLanguageOption(string code, string resourceKey)
    {
        Code = code;
        ResourceKey = resourceKey;
        _displayName = resourceKey;
    }

    public string Code { get; set; }
    public string ResourceKey { get; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }
}

public class TranslationPopupPositionModeOption : ObservableObject
{
    private string _displayName;

    public TranslationPopupPositionModeOption(TranslationPopupPositionMode mode, string resourceKey)
    {
        Mode = mode;
        ResourceKey = resourceKey;
        _displayName = resourceKey;
    }

    public TranslationPopupPositionMode Mode { get; set; }
    public string ResourceKey { get; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }
}

public class FontFamilyOption : ObservableObject
{
    private string _displayName;

    public FontFamilyOption(string family, string displayName)
    {
        Family = family;
        _displayName = displayName;
    }

    public string Family { get; set; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }
}
