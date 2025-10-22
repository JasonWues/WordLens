using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Semi.Avalonia;
using SharpHook.Data;
using WordLens.Messages;
using WordLens.Models;
using WordLens.Services;
using WordLens.Util;

namespace WordLens.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {

        private readonly ISettingsService? _settingsService;
        private readonly IHotkeyManagerService? _hotkeyManagerService;
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

        public SettingsViewModel()
        {
            _settingsService = null;
            _hotkeyManagerService = null!;
        }

        public SettingsViewModel(ISettingsService settingsService, IHotkeyManagerService hotkeyManagerService)
        {
            _settingsService = settingsService;
            _hotkeyManagerService = hotkeyManagerService;
            
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

        void OnUILanguageChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                SemiTheme.OverrideLocaleResources(Application.Current, new CultureInfo(value));
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