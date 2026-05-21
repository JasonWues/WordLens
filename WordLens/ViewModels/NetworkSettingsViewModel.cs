using CommunityToolkit.Mvvm.ComponentModel;
using WordLens.Models;

namespace WordLens.ViewModels;

public partial class NetworkSettingsViewModel : ViewModelBase
{
    [ObservableProperty] private string proxyAddress = "http://127.0.0.1";
    [ObservableProperty] private bool proxyEnabled;
    [ObservableProperty] private string? proxyPassword;
    [ObservableProperty] private int proxyPort = 8080;
    [ObservableProperty] private bool proxyUseAuthentication;
    [ObservableProperty] private bool proxyUseSystemProxy;
    [ObservableProperty] private string? proxyUsername;

    public void Load(ProxyConfig config)
    {
        ProxyEnabled = config.Enabled;
        ProxyUseSystemProxy = config.UseSystemProxy;
        ProxyAddress = config.Address;
        ProxyPort = config.Port;
        ProxyUseAuthentication = config.UseAuthentication;
        ProxyUsername = config.Username;
        ProxyPassword = config.Password;
    }

    public ProxyConfig BuildProxyConfig()
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

    public static ProxyConfig CloneProxyConfig(ProxyConfig config)
    {
        return new ProxyConfig
        {
            Enabled = config.Enabled,
            UseSystemProxy = config.UseSystemProxy,
            Address = config.Address,
            Port = config.Port,
            UseAuthentication = config.UseAuthentication,
            Username = config.Username,
            Password = config.Password
        };
    }
}
