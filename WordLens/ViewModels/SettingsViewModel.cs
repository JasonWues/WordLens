using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Semi.Avalonia;
using SharpHook.Data;
using WordLens.Messages;
using WordLens.Models;
using WordLens.Services;
using WordLens.Util;
using ZLogger;

namespace WordLens.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService? _settingsService;
        private readonly IHotkeyManagerService? _hotkeyManagerService;
        private readonly IModelProviderService? _modelProviderService;
        private readonly IEncryptionService? _encryptionService;
        private readonly ILogger<SettingsViewModel>? _logger;
        private AppSettings? _originalSettings;

        [ObservableProperty]
        private string uiLanguage = "zh-CN";

        [ObservableProperty]
        private string hotkeyDisplay = "Ctrl+Shift+T";

        [ObservableProperty]
        private string ocrHotkeyDisplay = "Ctrl+Shift+W";

        [ObservableProperty]
        private bool isCapturingHotkey = false;

        [ObservableProperty]
        private ObservableCollection<ProviderConfig> providers = new();

        [ObservableProperty]
        private ProviderConfig? selectedProvider;

        [ObservableProperty]
        private string? selectedProviderName;

        // 代理设置
        [ObservableProperty]
        private bool proxyEnabled = false;

        [ObservableProperty]
        private bool proxyUseSystemProxy = false;

        [ObservableProperty]
        private string proxyAddress = "http://127.0.0.1";

        [ObservableProperty]
        private int proxyPort = 8080;

        [ObservableProperty]
        private bool proxyUseAuthentication = false;

        [ObservableProperty]
        private string? proxyUsername;

        [ObservableProperty]
        private string? proxyPassword;

        // 模型管理相关属性
        [ObservableProperty]
        private bool isLoadingModels = false;

        [ObservableProperty]
        private bool hasModelLoadError = false;

        [ObservableProperty]
        private string modelLoadErrorMessage = string.Empty;

        [ObservableProperty]
        private ModelInfo? selectedModelInfo;

        // 快捷键配置
        private HotkeyConfig _hotkeyConfig = HotkeyConfig.Default();
        private HotkeyConfig _ocrHotkeyConfig = HotkeyConfig.Default();
        
        // 当前正在捕获的热键类型
        private string _currentCapturingType = string.Empty;

        // 可用的应用界面语言列表
        public List<LanguageOption> AvailableUILanguages { get; } = new()
        {
            new LanguageOption("zh-CN", "简体中文"),
            new LanguageOption("en", "English"),
            new LanguageOption("ja", "日本語"),
        };
        

        public SettingsViewModel(
            ISettingsService settingsService,
            IHotkeyManagerService hotkeyManagerService,
            IModelProviderService modelProviderService,
            IEncryptionService encryptionService,
            ILogger<SettingsViewModel> logger)
        {
            _settingsService = settingsService;
            _hotkeyManagerService = hotkeyManagerService;
            _modelProviderService = modelProviderService;
            _encryptionService = encryptionService;
            _logger = logger;

            WeakReferenceMessenger.Default.Register<CapturingKeyMessage>(this, (r, m) =>
            {
                if (IsCapturingHotkey)
                {
                    CaptureKey(m.KeyEventArgs);
                    m.KeyEventArgs.Handled = true;
                }
            });
        }

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
            UpdateHotkeyDisplay();
            UpdateOcrHotkeyDisplay();

            // 加载翻译源
            Providers.Clear();
            foreach (var provider in settings.Providers)
            {
                Providers.Add(provider);
            }
            SelectedProviderName = settings.SelectedProvider;
            SelectedProvider = Providers.FirstOrDefault(p => p.Name == settings.SelectedProvider);

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
        /// 为所有Provider加载模型列表
        /// </summary>
        private async Task LoadModelsForAllProvidersAsync()
        {
            if (_modelProviderService == null || _encryptionService == null)
                return;
            
            var providersToLoad = Providers
                .Where(p => p.IsEnabled && !string.IsNullOrEmpty(p.ApiKey))
                .ToList();

            foreach (var provider in providersToLoad)
            {
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
        }

        [RelayCommand]
        private async Task SaveSettingsAsync()
        {
            var settings = BuildSettingsFromViewModel();
            await _settingsService.SaveAsync(settings);
            _originalSettings = settings;
        }

        [RelayCommand]
        private async Task ApplySettingsAsync()
        {
            await SaveSettingsAsync();
            // 重新加载快捷键配置
            if (_hotkeyManagerService != null)
            {
                await _hotkeyManagerService.ReloadConfigAsync();
            }
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
                UpdateHotkeyDisplay();
                UpdateOcrHotkeyDisplay();

                Providers.Clear();
                foreach (var provider in _originalSettings.Providers)
                {
                    Providers.Add(provider);
                }
                SelectedProviderName = _originalSettings.SelectedProvider;
                SelectedProvider = Providers.FirstOrDefault(p => p.Name == _originalSettings.SelectedProvider);

                ProxyEnabled = _originalSettings.Proxy.Enabled;
                ProxyUseSystemProxy = _originalSettings.Proxy.UseSystemProxy;
                ProxyAddress = _originalSettings.Proxy.Address;
                ProxyPort = _originalSettings.Proxy.Port;
                ProxyUseAuthentication = _originalSettings.Proxy.UseAuthentication;
                ProxyUsername = _originalSettings.Proxy.Username;
                ProxyPassword = _originalSettings.Proxy.Password;
            }
        }

        [RelayCommand]
        private void StartCaptureHotkey(string type)
        {
            IsCapturingHotkey = true;
            _currentCapturingType = type;
            
            if (type == "ocr")
            {
                OcrHotkeyDisplay = "请按下快捷键...";
            }
            else
            {
                HotkeyDisplay = "请按下快捷键...";
            }
        }

        public void CaptureKey(KeyEventArgs e)
        {
            if (!IsCapturingHotkey) return;

            // 将 Avalonia 的 Key 转换为 SharpHook 的 KeyCode
            var keyCode = KeyCodeUtil.ConvertToKeyCode(e.Key);
            if (keyCode == KeyCode.VcUndefined) return;

            // 构建修饰键
            EventMask modifiers = EventMask.None;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                modifiers |= EventMask.LeftCtrl;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                modifiers |= EventMask.LeftShift;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
                modifiers |= EventMask.LeftAlt;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Meta))
                modifiers |= EventMask.LeftMeta;

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
            var parts = new List<string>();

            if (_hotkeyConfig.Modifiers.HasFlag(EventMask.LeftCtrl) || _hotkeyConfig.Modifiers.HasFlag(EventMask.RightCtrl))
                parts.Add("Ctrl");
            if (_hotkeyConfig.Modifiers.HasFlag(EventMask.LeftShift) || _hotkeyConfig.Modifiers.HasFlag(EventMask.RightShift))
                parts.Add("Shift");
            if (_hotkeyConfig.Modifiers.HasFlag(EventMask.LeftAlt) || _hotkeyConfig.Modifiers.HasFlag(EventMask.RightAlt))
                parts.Add("Alt");
            if (_hotkeyConfig.Modifiers.HasFlag(EventMask.LeftMeta) || _hotkeyConfig.Modifiers.HasFlag(EventMask.RightMeta))
                parts.Add("Win");

            parts.Add(KeyCodeUtil.GetKeyName(_hotkeyConfig.Key));
            HotkeyDisplay = string.Join("+", parts);
        }

        private void UpdateOcrHotkeyDisplay()
        {
            var parts = new List<string>();

            if (_ocrHotkeyConfig.Modifiers.HasFlag(EventMask.LeftCtrl) || _ocrHotkeyConfig.Modifiers.HasFlag(EventMask.RightCtrl))
                parts.Add("Ctrl");
            if (_ocrHotkeyConfig.Modifiers.HasFlag(EventMask.LeftShift) || _ocrHotkeyConfig.Modifiers.HasFlag(EventMask.RightShift))
                parts.Add("Shift");
            if (_ocrHotkeyConfig.Modifiers.HasFlag(EventMask.LeftAlt) || _ocrHotkeyConfig.Modifiers.HasFlag(EventMask.RightAlt))
                parts.Add("Alt");
            if (_ocrHotkeyConfig.Modifiers.HasFlag(EventMask.LeftMeta) || _ocrHotkeyConfig.Modifiers.HasFlag(EventMask.RightMeta))
                parts.Add("Win");

            parts.Add(KeyCodeUtil.GetKeyName(_ocrHotkeyConfig.Key));
            OcrHotkeyDisplay = string.Join("+", parts);
        }

        [RelayCommand]
        private void AddProvider()
        {
            var newProvider = new ProviderConfig
            {
                Name = $"新翻译源 {Providers.Count + 1}",
                Type = ProviderType.OpenAI,
                BaseUrl = "https://api.openai.com",
                Model = "gpt-4o-mini"
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
                if (Providers.Count > 0)
                {
                    SelectedProvider = Providers[Math.Min(index, Providers.Count - 1)];
                }
            }
        }

        [RelayCommand]
        private void MoveProviderUp()
        {
            if (SelectedProvider != null)
            {
                var index = Providers.IndexOf(SelectedProvider);
                if (index > 0)
                {
                    Providers.Move(index, index - 1);
                }
            }
        }

        [RelayCommand]
        private void MoveProviderDown()
        {
            if (SelectedProvider != null)
            {
                var index = Providers.IndexOf(SelectedProvider);
                if (index < Providers.Count - 1)
                {
                    Providers.Move(index, index + 1);
                }
            }
        }

        /// <summary>
        /// 刷新指定Provider的模型列表
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
                    CancellationToken.None);

                // 如果当前模型不在列表中，添加它（保持用户选择）
                if (!string.IsNullOrEmpty(provider.Model) &&
                    models.All(m => m.Id != provider.Model))
                {
                    models.Insert(0, new ModelInfo { Id = provider.Model, OwnedBy = "custom" });
                    _logger?.ZLogInformation($"当前模型 {provider.Model} 不在列表中，已添加");
                }

                provider.AvailableModels = models;
                _logger?.ZLogInformation($"成功获取 {models.Count} 个模型");

                // 触发UI更新
                OnPropertyChanged(nameof(Providers));
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

        private AppSettings BuildSettingsFromViewModel()
        {
            return new AppSettings
            {
                UILanguage = UiLanguage,
                Hotkey = _hotkeyConfig,
                OcrHotkey = _ocrHotkeyConfig,
                SelectedProvider = SelectedProvider?.Name ?? Providers.FirstOrDefault()?.Name,
                Providers = Providers.ToList(),
                Proxy = new ProxyConfig
                {
                    Enabled = ProxyEnabled,
                    UseSystemProxy = ProxyUseSystemProxy,
                    Address = ProxyAddress,
                    Port = ProxyPort,
                    UseAuthentication = ProxyUseAuthentication,
                    Username = ProxyUsername,
                    Password = ProxyPassword
                }
            };
        }

        partial void OnUiLanguageChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                SemiTheme.OverrideLocaleResources(Application.Current, new CultureInfo(value));
            }
        }

        /// <summary>
        /// 当选择的模型信息变化时，同步到Provider配置
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
        public string Code { get; set; }
        public string DisplayName { get; set; }

        public LanguageOption(string code, string displayName)
        {
            Code = code;
            DisplayName = displayName;
        }
    }
}