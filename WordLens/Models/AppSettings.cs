using System.Collections.Generic;
using SharpHook.Data;

namespace WordLens.Models
{
    public class AppSettings
    {
        public HotkeyConfig Hotkey { get; set; } = HotkeyConfig.Default();
        public string TargetLanguage { get; set; } = "zh-CN";
        public string? SelectedProvider { get; set; } = "OpenAI";
        public ProxyConfig Proxy { get; set; } = new ProxyConfig();

        public List<ProviderConfig> Providers { get; set; } = new List<ProviderConfig>
        {
            new ProviderConfig
            {
                Name = "OpenAI",
                BaseUrl = "https://api.openai.com",
                ApiKey = null,
                Model = "gpt-4o-mini",
                Type = ProviderType.OpenAI
            }
        };
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
    }

    public enum ProviderType
    {
        OpenAI
    }

    public class ProviderConfig
    {
        public string Name { get; set; } = string.Empty;
        public ProviderType Type { get; set; } = ProviderType.OpenAI;
        public string BaseUrl { get; set; } = string.Empty; // e.g. https://api.openai.com or compatible
        public string? ApiKey { get; set; }
        public string Model { get; set; } = string.Empty;
    }

    public class ProxyConfig
    {
        public bool Enabled { get; set; } = false;
        public string Address { get; set; } = "http://127.0.0.1";
        public int Port { get; set; } = 8080;
        public bool UseAuthentication { get; set; } = false;
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}