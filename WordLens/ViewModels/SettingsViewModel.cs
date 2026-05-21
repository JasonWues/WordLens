using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using SharpHook.Data;
using Sortable.Avalonia;
using WordLens.Messages;
using WordLens.Models;
using WordLens.Services;
using WordLens.Services.Implementations;
using WordLens.Util;
using ZLogger;

namespace WordLens.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly EncryptionService? _encryptionService;
    private readonly IHotkeyManagerService? _hotkeyManagerService;
    private readonly ILogger<SettingsViewModel>? _logger;
    private readonly OpenAIModelProviderService? _modelProviderService;
    private readonly IPathPickerService? _pathPickerService;
    private readonly ISettingsService? _settingsService;
    private readonly IStartupService? _startupService;
    private readonly AvaloniaThemeService? _themeService;

    // 当前正在捕获的热键类型
    private string _currentCapturingType = string.Empty;

    // 快捷键配置
    private HotkeyConfig _hotkeyConfig = HotkeyConfig.Default();
    private HotkeyConfig _ocrHotkeyConfig = HotkeyConfig.Default();
    private AppSettings? _originalSettings;
    private static readonly IReadOnlyList<string> OnnxFilePatterns = new[] { "*.onnx" };
    private static readonly IReadOnlyList<string> TextFilePatterns = new[] { "*.txt" };
    private static readonly IReadOnlyList<string> BinFilePatterns = new[] { "*.bin" };
    private static readonly IReadOnlyList<string> FstFilePatterns = new[] { "*.fst" };
    private static readonly IReadOnlyList<string> FarFilePatterns = new[] { "*.far" };

    [ObservableProperty] private bool hasModelLoadError;

    [ObservableProperty] private string hotkeyDisplay = "Ctrl+Shift+T";

    [ObservableProperty] private bool isCapturingHotkey;

    // 模型管理相关属性
    [ObservableProperty] private bool isLoadingModels;

    [ObservableProperty] private string modelLoadErrorMessage = string.Empty;

    [ObservableProperty] private string ocrHotkeyDisplay = "Ctrl+Shift+W";

    [ObservableProperty] private ObservableCollection<ProviderConfig> ocrProviders = new();

    [ObservableProperty] private ObservableCollection<ProviderConfig> providers = new();

    [ObservableProperty] private string proxyAddress = "http://127.0.0.1";

    // 代理设置
    [ObservableProperty] private bool proxyEnabled;

    [ObservableProperty] private string? proxyPassword;

    [ObservableProperty] private int proxyPort = 8080;

    [ObservableProperty] private bool proxyUseAuthentication;

    [ObservableProperty] private string? proxyUsername;

    [ObservableProperty] private bool proxyUseSystemProxy;

    [ObservableProperty] private ModelInfo? selectedModelInfo;

    [ObservableProperty] private ProviderConfig? selectedOcrProvider;

    [ObservableProperty] private ProviderConfig? selectedProvider;

    [ObservableProperty] private string? selectedProviderName;

    [ObservableProperty] private string uiLanguage = "zh-CN";

    [ObservableProperty] private bool startWithSystem;

    [ObservableProperty] private bool isStartupSupported = true;

    [ObservableProperty] private bool ttsEnabled;

    [ObservableProperty] private TtsModelType ttsModelType = TtsModelType.Vits;

    [ObservableProperty] private string ttsModelPath = string.Empty;

    [ObservableProperty] private string ttsTokensPath = string.Empty;

    [ObservableProperty] private string ttsVoicesPath = string.Empty;

    [ObservableProperty] private string ttsDataDir = string.Empty;

    [ObservableProperty] private string ttsLexiconPath = string.Empty;

    [ObservableProperty] private string ttsDictDir = string.Empty;

    [ObservableProperty] private string ttsVocoderPath = string.Empty;

    [ObservableProperty] private string ttsRuleFsts = string.Empty;

    [ObservableProperty] private string ttsRuleFars = string.Empty;

    [ObservableProperty] private string ttsProvider = "cpu";

    [ObservableProperty] private int ttsNumThreads = 2;

    [ObservableProperty] private int ttsSpeakerId;

    [ObservableProperty] private double ttsSpeed = 1.0;

    // 流式输出配置
    [ObservableProperty] private bool streamingEnabled = true;

    [ObservableProperty] private int typewriterDelayMs = 0;

    [ObservableProperty] private int charsPerUpdate = 1;


    public SettingsViewModel(
        ISettingsService settingsService,
        IHotkeyManagerService hotkeyManagerService,
        OpenAIModelProviderService modelProviderService,
        EncryptionService encryptionService,
        AvaloniaThemeService themeService,
        IStartupService startupService,
        IPathPickerService pathPickerService,
        ILogger<SettingsViewModel> logger)
    {
        _settingsService = settingsService;
        _hotkeyManagerService = hotkeyManagerService;
        _modelProviderService = modelProviderService;
        _encryptionService = encryptionService;
        _themeService = themeService;
        _startupService = startupService;
        _pathPickerService = pathPickerService;
        _logger = logger;

        WeakReferenceMessenger.Default.Register<CapturingKeyMessage>(this, (r, m) =>
        {
            if (IsCapturingHotkey)
            {
                m.Handled = true;
                CaptureKey(m.KeyCode, m.Modifiers);
            }
        });
    }

    // 可用的应用界面语言列表
    public List<LanguageOption> AvailableUILanguages { get; } = new()
    {
        new LanguageOption("zh-CN", "简体中文"),
        new LanguageOption("en", "English"),
        new LanguageOption("ja", "日本語")
    };

    public List<TtsModelTypeOption> AvailableTtsModelTypes { get; } = new()
    {
        new TtsModelTypeOption(TtsModelType.Vits, "VITS / Piper"),
        new TtsModelTypeOption(TtsModelType.Kokoro, "Kokoro"),
        new TtsModelTypeOption(TtsModelType.Matcha, "Matcha")
    };

    public bool IsVitsTtsModel => TtsModelType == TtsModelType.Vits;

    public bool IsKokoroTtsModel => TtsModelType == TtsModelType.Kokoro;

    public bool IsMatchaTtsModel => TtsModelType == TtsModelType.Matcha;

    public async Task InitializeAsync()
    {
        await LoadSettingsAsync();

        // 自动获取所有启用Provider的模型列表
        await LoadModelsForAllProvidersAsync();
    }

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        var settings = _settingsService != null ? await _settingsService.LoadAsync() : new AppSettings();
        _originalSettings = settings;

        // 加载常规设置
        UiLanguage = settings.UILanguage;
        _hotkeyConfig = settings.Hotkey;
        _ocrHotkeyConfig = settings.OcrHotkey;
        IsStartupSupported = _startupService?.IsSupported ?? false;
        StartWithSystem = IsStartupSupported ? _startupService?.IsEnabled() ?? settings.StartWithSystem : settings.StartWithSystem;
        LoadTtsSettings(settings.Tts);
        UpdateHotkeyDisplay();
        UpdateOcrHotkeyDisplay();

        // 加载流式输出配置
        StreamingEnabled = settings.Streaming.Enabled;
        TypewriterDelayMs = settings.Streaming.TypewriterDelayMs;
        CharsPerUpdate = settings.Streaming.CharsPerUpdate;

        // 加载翻译源
        Providers.Clear();
        foreach (var provider in settings.Providers) Providers.Add(provider);
        SelectedProviderName = settings.SelectedProvider;
        SelectedProvider = Providers.FirstOrDefault(p => p.Name == settings.SelectedProvider);
        OcrProviders.Clear();
        foreach (var provider in settings.OcrProviders) OcrProviders.Add(CloneOcrProviderForEditing(provider));
        SelectedOcrProvider = OcrProviders.FirstOrDefault(p => p.Name == settings.SelectedOcrProvider) ??
                              OcrProviders.FirstOrDefault();

        // 加载代理设置
        ProxyEnabled = settings.Proxy.Enabled;
        ProxyUseSystemProxy = settings.Proxy.UseSystemProxy;
        ProxyAddress = settings.Proxy.Address;
        ProxyPort = settings.Proxy.Port;
        ProxyUseAuthentication = settings.Proxy.UseAuthentication;
        ProxyUsername = settings.Proxy.Username;
        ProxyPassword = settings.Proxy.Password;
    }

    /// <summary>
    ///     为所有Provider加载模型列表
    /// </summary>
    private async Task LoadModelsForAllProvidersAsync()
    {
        if (_modelProviderService == null || _encryptionService == null)
            return;

        var providersToLoad = Providers
            .Where(p => p.IsEnabled && !string.IsNullOrEmpty(p.ApiKey))
            .ToList();

        foreach (var provider in providersToLoad)
            try
            {
                await RefreshModelsAsync(provider);
            }
            catch (Exception ex)
            {
                _logger?.ZLogWarning(ex, $"为Provider {provider.Name} 加载模型失败: {ex.Message}");
                // 继续处理其他Provider
            }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        await SaveSettingsCoreAsync();
        WeakReferenceMessenger.Default.Send(new CloseWindowMessage());
    }

    [RelayCommand]
    private async Task ApplySettingsAsync()
    {
        await SaveSettingsCoreAsync();
        // 重新加载快捷键配置
        if (_hotkeyManagerService != null) await _hotkeyManagerService.ReloadConfigAsync();
    }

    private async Task SaveSettingsCoreAsync()
    {
        if (_settingsService == null) return;

        var settings = BuildSettingsFromViewModel();
        var savedSnapshot = CloneSettings(settings);
        if (_startupService?.IsSupported == true)
            _startupService.SetEnabled(settings.StartWithSystem);
        await _settingsService.SaveAsync(settings);
        _originalSettings = savedSnapshot;
    }

    [RelayCommand]
    private void CancelSettings()
    {
        if (_originalSettings != null)
        {
            // 恢复原始设置
            UiLanguage = _originalSettings.UILanguage;
            _hotkeyConfig = _originalSettings.Hotkey;
            _ocrHotkeyConfig = _originalSettings.OcrHotkey;
            StartWithSystem = _originalSettings.StartWithSystem;
            LoadTtsSettings(_originalSettings.Tts);
            UpdateHotkeyDisplay();
            UpdateOcrHotkeyDisplay();

            // 恢复流式输出配置
            StreamingEnabled = _originalSettings.Streaming.Enabled;
            TypewriterDelayMs = _originalSettings.Streaming.TypewriterDelayMs;
            CharsPerUpdate = _originalSettings.Streaming.CharsPerUpdate;

            Providers.Clear();
            foreach (var provider in _originalSettings.Providers) Providers.Add(provider);
            SelectedProviderName = _originalSettings.SelectedProvider;
            SelectedProvider = Providers.FirstOrDefault(p => p.Name == _originalSettings.SelectedProvider);
            OcrProviders.Clear();
            foreach (var provider in _originalSettings.OcrProviders) OcrProviders.Add(CloneOcrProviderForEditing(provider));
            SelectedOcrProvider = OcrProviders.FirstOrDefault(p => p.Name == _originalSettings.SelectedOcrProvider) ??
                                  OcrProviders.FirstOrDefault();

            ProxyEnabled = _originalSettings.Proxy.Enabled;
            ProxyUseSystemProxy = _originalSettings.Proxy.UseSystemProxy;
            ProxyAddress = _originalSettings.Proxy.Address;
            ProxyPort = _originalSettings.Proxy.Port;
            ProxyUseAuthentication = _originalSettings.Proxy.UseAuthentication;
            ProxyUsername = _originalSettings.Proxy.Username;
            ProxyPassword = _originalSettings.Proxy.Password;
        }

        WeakReferenceMessenger.Default.Send(new CloseWindowMessage());

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

    public void CaptureKey(KeyCode keyCode, EventMask modifiers)
    {
        if (!IsCapturingHotkey) return;

        if (keyCode == KeyCode.VcUndefined) return;

        var newConfig = new HotkeyConfig
        {
            Modifiers = modifiers,
            Key = keyCode
        };

        // 根据捕获类型更新相应的热键配置
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

    [RelayCommand]
    private void SelectProvider(ProviderConfig? provider)
    {
        if (provider != null)
            SelectedProvider = provider;
    }

    [RelayCommand]
    private void AddProvider()
    {
        var newProvider = new ProviderConfig
        {
            Name = $"新翻译源 {Providers.Count + 1}",
            Type = ProviderType.OpenAI,
            BaseUrl = "https://api.openai.com",
            Model = "gpt-4o-mini",
            RequestArguments = string.Empty,
            SystemPromptTemplate = string.Empty,
            UserPromptTemplate = string.Empty
        };
        Providers.Add(newProvider);
        SelectedProvider = newProvider;
    }

    [RelayCommand]
    private void DeleteProvider()
    {
        if (SelectedProvider != null && Providers.Count > 1)
        {
            var index = Providers.IndexOf(SelectedProvider);
            Providers.Remove(SelectedProvider);

            // 选择下一个项
            if (Providers.Count > 0) SelectedProvider = Providers[Math.Min(index, Providers.Count - 1)];
        }
    }

    [RelayCommand]
    private void ReorderProvider(SortableUpdateEventArgs? args)
    {
        if (args == null)
            return;

        var movedProvider = args.Item as ProviderConfig;
        if (args.ApplyUpdateMutation() && movedProvider != null)
            SelectedProvider = movedProvider;
    }

    [RelayCommand]
    private void SelectOcrProvider(ProviderConfig? provider)
    {
        if (provider != null)
            SelectedOcrProvider = provider;
    }

    [RelayCommand]
    private void AddOcrProvider()
    {
        var newProvider = new ProviderConfig
        {
            Name = $"新OCR源 {OcrProviders.Count + 1}",
            Type = ProviderType.OpenAI,
            BaseUrl = "https://api.openai.com",
            Model = "gpt-4o-mini",
            RequestArguments = string.Empty,
            UserPromptTemplate = string.Empty
        };
        OcrProviders.Add(newProvider);
        SelectedOcrProvider = newProvider;
    }

    [RelayCommand]
    private void DeleteOcrProvider()
    {
        if (SelectedOcrProvider != null && OcrProviders.Count > 1)
        {
            var index = OcrProviders.IndexOf(SelectedOcrProvider);
            OcrProviders.Remove(SelectedOcrProvider);

            if (OcrProviders.Count > 0)
                SelectedOcrProvider = OcrProviders[Math.Min(index, OcrProviders.Count - 1)];
        }
    }

    [RelayCommand]
    private void ReorderOcrProvider(SortableUpdateEventArgs? args)
    {
        if (args == null)
            return;

        var movedProvider = args.Item as ProviderConfig;
        if (args.ApplyUpdateMutation() && movedProvider != null)
            SelectedOcrProvider = movedProvider;
    }

    /// <summary>
    ///     刷新指定Provider的模型列表
    /// </summary>
    [RelayCommand]
    private async Task RefreshModelsAsync(ProviderConfig? provider)
    {
        if (provider == null ||
            string.IsNullOrEmpty(provider.ApiKey) ||
            _modelProviderService == null ||
            _encryptionService == null)
        {
            _logger?.ZLogWarning($"无法刷新模型：Provider或服务为null");
            return;
        }

        IsLoadingModels = true;
        HasModelLoadError = false;
        ModelLoadErrorMessage = string.Empty;

        try
        {
            _logger?.ZLogInformation($"开始刷新 {provider.Name} 的模型列表");

            // 解密API Key
            var decryptedKey = _encryptionService.Decrypt(provider.ApiKey);

            // 获取模型列表
            var models = await _modelProviderService.GetAvailableModelsAsync(
                decryptedKey,
                provider.BaseUrl,
                BuildProxyConfigFromViewModel(),
                CancellationToken.None);

            // 如果当前模型不在列表中，添加它（保持用户选择）
            if (!string.IsNullOrEmpty(provider.Model) &&
                models.All(m => m.Id != provider.Model))
            {
                models.Insert(0, new ModelInfo { Id = provider.Model, OwnedBy = "custom" });
                _logger?.ZLogInformation($"当前模型 {provider.Model} 不在列表中，已添加");
            }

            provider.AvailableModels ??= new ObservableCollection<ModelInfo>();
            provider.AvailableModels.Clear();
            foreach (var modelInfo in models)
                provider.AvailableModels.Add(modelInfo);

            _logger?.ZLogInformation($"成功获取 {models.Count} 个模型");

        }
        catch (ArgumentException ex)
        {
            HasModelLoadError = true;
            ModelLoadErrorMessage = $"参数错误: {ex.Message}";
            _logger?.ZLogError(ex, $"刷新模型列表失败: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            HasModelLoadError = true;
            ModelLoadErrorMessage = $"网络请求失败: {ex.Message}";
            _logger?.ZLogError(ex, $"刷新模型列表失败: {ex.Message}");
        }
        catch (TimeoutException ex)
        {
            HasModelLoadError = true;
            ModelLoadErrorMessage = $"请求超时: {ex.Message}";
            _logger?.ZLogError(ex, $"刷新模型列表失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            HasModelLoadError = true;
            ModelLoadErrorMessage = $"未知错误: {ex.Message}";
            _logger?.ZLogError(ex, $"刷新模型列表失败: {ex.Message}");
        }
        finally
        {
            IsLoadingModels = false;
        }
    }

    private ProxyConfig BuildProxyConfigFromViewModel()
    {
        return new ProxyConfig
        {
            Enabled = ProxyEnabled,
            UseSystemProxy = ProxyUseSystemProxy,
            Address = ProxyAddress,
            Port = ProxyPort,
            UseAuthentication = ProxyUseAuthentication,
            Username = ProxyUsername,
            Password = ProxyPassword
        };
    }

    private AppSettings BuildSettingsFromViewModel()
    {
        return new AppSettings
        {
            UILanguage = UiLanguage,
            LastTargetLanguage = _originalSettings?.LastTargetLanguage ?? "en",
            StartWithSystem = StartWithSystem,
            Hotkey = _hotkeyConfig,
            OcrHotkey = _ocrHotkeyConfig,
            Tts = BuildTtsSettingsFromViewModel(),
            SelectedProvider = SelectedProvider?.Name ?? Providers.FirstOrDefault()?.Name,
            Providers = Providers.Select(CloneProviderForPersistence).ToList(),
            SelectedOcrProvider = SelectedOcrProvider?.Name ?? OcrProviders.FirstOrDefault()?.Name,
            OcrProviders = OcrProviders.Select(CloneOcrProviderForPersistence).ToList(),
            Streaming = new StreamingConfig
            {
                Enabled = StreamingEnabled,
                TypewriterDelayMs = TypewriterDelayMs,
                CharsPerUpdate = CharsPerUpdate
            },
            Proxy = BuildProxyConfigFromViewModel()
        };
    }

    partial void OnUiLanguageChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
            _themeService?.ApplyLocale(value);
    }

    partial void OnTtsModelTypeChanged(TtsModelType value)
    {
        OnPropertyChanged(nameof(IsVitsTtsModel));
        OnPropertyChanged(nameof(IsKokoroTtsModel));
        OnPropertyChanged(nameof(IsMatchaTtsModel));
    }

    private void LoadTtsSettings(TtsConfig config)
    {
        TtsEnabled = config.Enabled;
        TtsModelType = config.ModelType;
        TtsModelPath = config.ModelPath;
        TtsTokensPath = config.TokensPath;
        TtsVoicesPath = config.VoicesPath;
        TtsDataDir = config.DataDir;
        TtsLexiconPath = config.LexiconPath;
        TtsDictDir = config.DictDir;
        TtsVocoderPath = config.VocoderPath;
        TtsRuleFsts = config.RuleFsts;
        TtsRuleFars = config.RuleFars;
        TtsProvider = config.Provider;
        TtsNumThreads = config.NumThreads;
        TtsSpeakerId = config.SpeakerId;
        TtsSpeed = config.Speed;
    }

    [RelayCommand]
    private async Task PickTtsModelPathAsync()
    {
        await PickSingleFilePathAsync("选择 TTS ONNX 模型", OnnxFilePatterns, path => TtsModelPath = path);
    }

    [RelayCommand]
    private async Task PickTtsTokensPathAsync()
    {
        await PickSingleFilePathAsync("选择 tokens.txt", TextFilePatterns, path => TtsTokensPath = path);
    }

    [RelayCommand]
    private async Task PickTtsVoicesPathAsync()
    {
        await PickSingleFilePathAsync("选择 voices.bin", BinFilePatterns, path => TtsVoicesPath = path);
    }

    [RelayCommand]
    private async Task PickTtsVocoderPathAsync()
    {
        await PickSingleFilePathAsync("选择 Vocoder ONNX 模型", OnnxFilePatterns, path => TtsVocoderPath = path);
    }

    [RelayCommand]
    private async Task PickTtsDataDirAsync()
    {
        await PickFolderPathAsync("选择 espeak-ng-data 目录", path => TtsDataDir = path);
    }

    [RelayCommand]
    private async Task PickTtsDictDirAsync()
    {
        await PickFolderPathAsync("选择词典目录", path => TtsDictDir = path);
    }

    [RelayCommand]
    private async Task PickTtsLexiconPathAsync()
    {
        await PickMultipleFilePathsAsync("选择 Lexicon 文件", TextFilePatterns, paths => TtsLexiconPath = paths);
    }

    [RelayCommand]
    private async Task PickTtsRuleFstsAsync()
    {
        await PickMultipleFilePathsAsync("选择 Rule FST 文件", FstFilePatterns, paths => TtsRuleFsts = paths);
    }

    [RelayCommand]
    private async Task PickTtsRuleFarsAsync()
    {
        await PickMultipleFilePathsAsync("选择 Rule FAR 文件", FarFilePatterns, paths => TtsRuleFars = paths);
    }

    private async Task PickSingleFilePathAsync(
        string title,
        IReadOnlyList<string> patterns,
        Action<string> apply)
    {
        if (_pathPickerService == null)
            return;

        var path = await _pathPickerService.PickFileAsync(title, patterns);
        if (!string.IsNullOrWhiteSpace(path))
            apply(path);
    }

    private async Task PickMultipleFilePathsAsync(
        string title,
        IReadOnlyList<string> patterns,
        Action<string> apply)
    {
        if (_pathPickerService == null)
            return;

        var paths = await _pathPickerService.PickFilesAsync(title, patterns);
        if (paths.Count > 0)
            apply(string.Join(",", paths));
    }

    private async Task PickFolderPathAsync(string title, Action<string> apply)
    {
        if (_pathPickerService == null)
            return;

        var path = await _pathPickerService.PickFolderAsync(title);
        if (!string.IsNullOrWhiteSpace(path))
            apply(path);
    }

    private TtsConfig BuildTtsSettingsFromViewModel()
    {
        return new TtsConfig
        {
            Enabled = TtsEnabled,
            ModelType = TtsModelType,
            ModelPath = TtsModelPath,
            TokensPath = TtsTokensPath,
            VoicesPath = TtsVoicesPath,
            DataDir = TtsDataDir,
            LexiconPath = TtsLexiconPath,
            DictDir = TtsDictDir,
            VocoderPath = TtsVocoderPath,
            RuleFsts = TtsRuleFsts,
            RuleFars = TtsRuleFars,
            Provider = string.IsNullOrWhiteSpace(TtsProvider) ? "cpu" : TtsProvider,
            NumThreads = Math.Max(1, TtsNumThreads),
            SpeakerId = Math.Max(0, TtsSpeakerId),
            Speed = Math.Clamp(TtsSpeed, 0.25, 4.0)
        };
    }

    private ProviderConfig CloneOcrProviderForEditing(ProviderConfig provider)
    {
        return new ProviderConfig
        {
            Name = provider.Name,
            Type = provider.Type,
            BaseUrl = provider.BaseUrl,
            ApiKey = string.IsNullOrEmpty(provider.ApiKey) || _encryptionService == null
                ? provider.ApiKey
                : _encryptionService.Decrypt(provider.ApiKey),
            Model = provider.Model,
            IsEnabled = provider.IsEnabled,
            RequestArguments = provider.RequestArguments,
            SystemPromptTemplate = provider.SystemPromptTemplate,
            UserPromptTemplate = provider.UserPromptTemplate,
            AllowManualModelInput = provider.AllowManualModelInput
        };
    }

    private static ProviderConfig CloneOcrProviderForPersistence(ProviderConfig provider)
    {
        return new ProviderConfig
        {
            Name = provider.Name,
            Type = provider.Type,
            BaseUrl = provider.BaseUrl,
            ApiKey = provider.ApiKey,
            Model = provider.Model,
            IsEnabled = provider.IsEnabled,
            RequestArguments = provider.RequestArguments,
            SystemPromptTemplate = provider.SystemPromptTemplate,
            UserPromptTemplate = provider.UserPromptTemplate,
            AllowManualModelInput = provider.AllowManualModelInput
        };
    }

    private static ProviderConfig CloneProviderForPersistence(ProviderConfig provider)
    {
        return new ProviderConfig
        {
            Name = provider.Name,
            Type = provider.Type,
            BaseUrl = provider.BaseUrl,
            ApiKey = provider.ApiKey,
            Model = provider.Model,
            IsEnabled = provider.IsEnabled,
            RequestArguments = provider.RequestArguments,
            SystemPromptTemplate = provider.SystemPromptTemplate,
            UserPromptTemplate = provider.UserPromptTemplate,
            AllowManualModelInput = provider.AllowManualModelInput
        };
    }

    private static AppSettings CloneSettings(AppSettings settings)
    {
        return new AppSettings
        {
            UILanguage = settings.UILanguage,
            LastTargetLanguage = settings.LastTargetLanguage,
            StartWithSystem = settings.StartWithSystem,
            Hotkey = settings.Hotkey,
            OcrHotkey = settings.OcrHotkey,
            Tts = CloneTtsConfig(settings.Tts),
            SelectedProvider = settings.SelectedProvider,
            Providers = settings.Providers.Select(CloneProviderForPersistence).ToList(),
            SelectedOcrProvider = settings.SelectedOcrProvider,
            OcrProviders = settings.OcrProviders.Select(CloneOcrProviderForPersistence).ToList(),
            Streaming = new StreamingConfig
            {
                Enabled = settings.Streaming.Enabled,
                TypewriterDelayMs = settings.Streaming.TypewriterDelayMs,
                CharsPerUpdate = settings.Streaming.CharsPerUpdate
            },
            Proxy = new ProxyConfig
            {
                Enabled = settings.Proxy.Enabled,
                UseSystemProxy = settings.Proxy.UseSystemProxy,
                Address = settings.Proxy.Address,
                Port = settings.Proxy.Port,
                UseAuthentication = settings.Proxy.UseAuthentication,
                Username = settings.Proxy.Username,
                Password = settings.Proxy.Password
            }
        };
    }

    private static TtsConfig CloneTtsConfig(TtsConfig config)
    {
        return new TtsConfig
        {
            Enabled = config.Enabled,
            ModelType = config.ModelType,
            ModelPath = config.ModelPath,
            TokensPath = config.TokensPath,
            VoicesPath = config.VoicesPath,
            DataDir = config.DataDir,
            LexiconPath = config.LexiconPath,
            DictDir = config.DictDir,
            VocoderPath = config.VocoderPath,
            RuleFsts = config.RuleFsts,
            RuleFars = config.RuleFars,
            Provider = config.Provider,
            NumThreads = config.NumThreads,
            SpeakerId = config.SpeakerId,
            Speed = config.Speed
        };
    }

    /// <summary>
    ///     当选择的模型信息变化时，同步到Provider配置
    /// </summary>
    partial void OnSelectedModelInfoChanged(ModelInfo? value)
    {
        if (value != null && SelectedProvider != null)
        {
            SelectedProvider.Model = value.Id;
            _logger?.ZLogInformation($"模型已更新为: {value.Id}");
        }
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

public class TtsModelTypeOption
{
    public TtsModelTypeOption(TtsModelType value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }

    public TtsModelType Value { get; set; }
    public string DisplayName { get; set; }
}
