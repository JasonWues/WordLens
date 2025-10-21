using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpHook.Data;
using WordLens.Models;
using WordLens.Services;

namespace WordLens.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IHotkeyService _hotkeyService;
        private AppSettings? _originalSettings;

        [ObservableProperty]
        private string targetLanguage = "zh-CN";

        [ObservableProperty]
        private string hotkeyDisplay = "Ctrl+Shift+T";

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

        // 可用的目标语言列表
        public List<LanguageOption> AvailableLanguages { get; } = new()
        {
            new LanguageOption("zh-CN", "简体中文"),
            new LanguageOption("zh-TW", "繁體中文"),
            new LanguageOption("en", "English"),
            new LanguageOption("ja", "日本語"),
            new LanguageOption("ko", "한국어"),
            new LanguageOption("fr", "Français"),
            new LanguageOption("de", "Deutsch"),
            new LanguageOption("es", "Español"),
            new LanguageOption("ru", "Русский"),
        };

        public SettingsViewModel()
        {
            // 设计时构造函数
            _settingsService = null!;
            _hotkeyService = null!;
        }

        public SettingsViewModel(ISettingsService settingsService, IHotkeyService hotkeyService)
        {
            _settingsService = settingsService;
            _hotkeyService = hotkeyService;
        }

        public async Task InitializeAsync()
        {
            await LoadSettingsAsync();
        }

        [RelayCommand]
        private async Task LoadSettingsAsync()
        {
            var settings = await _settingsService.LoadAsync();
            _originalSettings = settings;

            // 加载常规设置
            TargetLanguage = settings.TargetLanguage;
            _hotkeyConfig = settings.Hotkey;
            UpdateHotkeyDisplay();

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
            // 重新加载快捷键
            if (_hotkeyService is HotkeyService service)
            {
                await service.ReloadHotkeyAsync();
            }
        }

        [RelayCommand]
        private void CancelSettings()
        {
            if (_originalSettings != null)
            {
                // 恢复原始设置
                TargetLanguage = _originalSettings.TargetLanguage;
                _hotkeyConfig = _originalSettings.Hotkey;
                UpdateHotkeyDisplay();

                Providers.Clear();
                foreach (var provider in _originalSettings.Providers)
                {
                    Providers.Add(provider);
                }
                SelectedProviderName = _originalSettings.SelectedProvider;
                SelectedProvider = Providers.FirstOrDefault(p => p.Name == _originalSettings.SelectedProvider);

                ProxyEnabled = _originalSettings.Proxy.Enabled;
                ProxyAddress = _originalSettings.Proxy.Address;
                ProxyPort = _originalSettings.Proxy.Port;
                ProxyUseAuthentication = _originalSettings.Proxy.UseAuthentication;
                ProxyUsername = _originalSettings.Proxy.Username;
                ProxyPassword = _originalSettings.Proxy.Password;
            }
        }

        [RelayCommand]
        private void StartCaptureHotkey()
        {
            IsCapturingHotkey = true;
            HotkeyDisplay = "请按下快捷键...";
        }

        public void CaptureKey(KeyEventArgs e)
        {
            if (!IsCapturingHotkey) return;

            // 将 Avalonia 的 Key 转换为 SharpHook 的 KeyCode
            var keyCode = ConvertToKeyCode(e.Key);
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

            _hotkeyConfig = new HotkeyConfig
            {
                Modifiers = modifiers,
                Key = keyCode
            };

            IsCapturingHotkey = false;
            UpdateHotkeyDisplay();
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

            parts.Add(GetKeyName(_hotkeyConfig.Key));
            HotkeyDisplay = string.Join("+", parts);
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
                TargetLanguage = TargetLanguage,
                Hotkey = _hotkeyConfig,
                SelectedProvider = SelectedProvider?.Name ?? Providers.FirstOrDefault()?.Name,
                Providers = Providers.ToList(),
                Proxy = new ProxyConfig
                {
                    Enabled = ProxyEnabled,
                    Address = ProxyAddress,
                    Port = ProxyPort,
                    UseAuthentication = ProxyUseAuthentication,
                    Username = ProxyUsername,
                    Password = ProxyPassword
                }
            };
        }

        private static KeyCode ConvertToKeyCode(Key key)
        {
            return key switch
            {
                Key.A => KeyCode.VcA,
                Key.B => KeyCode.VcB,
                Key.C => KeyCode.VcC,
                Key.D => KeyCode.VcD,
                Key.E => KeyCode.VcE,
                Key.F => KeyCode.VcF,
                Key.G => KeyCode.VcG,
                Key.H => KeyCode.VcH,
                Key.I => KeyCode.VcI,
                Key.J => KeyCode.VcJ,
                Key.K => KeyCode.VcK,
                Key.L => KeyCode.VcL,
                Key.M => KeyCode.VcM,
                Key.N => KeyCode.VcN,
                Key.O => KeyCode.VcO,
                Key.P => KeyCode.VcP,
                Key.Q => KeyCode.VcQ,
                Key.R => KeyCode.VcR,
                Key.S => KeyCode.VcS,
                Key.T => KeyCode.VcT,
                Key.U => KeyCode.VcU,
                Key.V => KeyCode.VcV,
                Key.W => KeyCode.VcW,
                Key.X => KeyCode.VcX,
                Key.Y => KeyCode.VcY,
                Key.Z => KeyCode.VcZ,
                Key.D0 => KeyCode.Vc0,
                Key.D1 => KeyCode.Vc1,
                Key.D2 => KeyCode.Vc2,
                Key.D3 => KeyCode.Vc3,
                Key.D4 => KeyCode.Vc4,
                Key.D5 => KeyCode.Vc5,
                Key.D6 => KeyCode.Vc6,
                Key.D7 => KeyCode.Vc7,
                Key.D8 => KeyCode.Vc8,
                Key.D9 => KeyCode.Vc9,
                Key.F1 => KeyCode.VcF1,
                Key.F2 => KeyCode.VcF2,
                Key.F3 => KeyCode.VcF3,
                Key.F4 => KeyCode.VcF4,
                Key.F5 => KeyCode.VcF5,
                Key.F6 => KeyCode.VcF6,
                Key.F7 => KeyCode.VcF7,
                Key.F8 => KeyCode.VcF8,
                Key.F9 => KeyCode.VcF9,
                Key.F10 => KeyCode.VcF10,
                Key.F11 => KeyCode.VcF11,
                Key.F12 => KeyCode.VcF12,
                Key.Space => KeyCode.VcSpace,
                Key.Enter => KeyCode.VcEnter,
                _ => KeyCode.VcUndefined
            };
        }

        private static string GetKeyName(KeyCode keyCode)
        {
            return keyCode switch
            {
                KeyCode.VcA => "A",
                KeyCode.VcB => "B",
                KeyCode.VcC => "C",
                KeyCode.VcD => "D",
                KeyCode.VcE => "E",
                KeyCode.VcF => "F",
                KeyCode.VcG => "G",
                KeyCode.VcH => "H",
                KeyCode.VcI => "I",
                KeyCode.VcJ => "J",
                KeyCode.VcK => "K",
                KeyCode.VcL => "L",
                KeyCode.VcM => "M",
                KeyCode.VcN => "N",
                KeyCode.VcO => "O",
                KeyCode.VcP => "P",
                KeyCode.VcQ => "Q",
                KeyCode.VcR => "R",
                KeyCode.VcS => "S",
                KeyCode.VcT => "T",
                KeyCode.VcU => "U",
                KeyCode.VcV => "V",
                KeyCode.VcW => "W",
                KeyCode.VcX => "X",
                KeyCode.VcY => "Y",
                KeyCode.VcZ => "Z",
                KeyCode.Vc0 => "0",
                KeyCode.Vc1 => "1",
                KeyCode.Vc2 => "2",
                KeyCode.Vc3 => "3",
                KeyCode.Vc4 => "4",
                KeyCode.Vc5 => "5",
                KeyCode.Vc6 => "6",
                KeyCode.Vc7 => "7",
                KeyCode.Vc8 => "8",
                KeyCode.Vc9 => "9",
                KeyCode.VcF1 => "F1",
                KeyCode.VcF2 => "F2",
                KeyCode.VcF3 => "F3",
                KeyCode.VcF4 => "F4",
                KeyCode.VcF5 => "F5",
                KeyCode.VcF6 => "F6",
                KeyCode.VcF7 => "F7",
                KeyCode.VcF8 => "F8",
                KeyCode.VcF9 => "F9",
                KeyCode.VcF10 => "F10",
                KeyCode.VcF11 => "F11",
                KeyCode.VcF12 => "F12",
                KeyCode.VcSpace => "Space",
                KeyCode.VcEnter => "Enter",
                _ => keyCode.ToString()
            };
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