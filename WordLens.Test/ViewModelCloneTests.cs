using WordLens.Models;
using WordLens.ViewModels;

namespace WordLens.Test;

public class ViewModelCloneTests
{
    [Fact]
    public void CloneProxyConfig_ReturnsIndependentCopy()
    {
        var source = new ProxyConfig
        {
            Enabled = true,
            UseSystemProxy = true,
            Address = "http://proxy.local",
            Port = 9090,
            UseAuthentication = true,
            Username = "user",
            Password = "password"
        };

        var clone = NetworkSettingsViewModel.CloneProxyConfig(source);
        source.Address = "http://changed.local";

        Assert.NotSame(source, clone);
        Assert.True(clone.Enabled);
        Assert.True(clone.UseSystemProxy);
        Assert.Equal("http://proxy.local", clone.Address);
        Assert.Equal(9090, clone.Port);
        Assert.True(clone.UseAuthentication);
        Assert.Equal("user", clone.Username);
        Assert.Equal("password", clone.Password);
    }

    [Fact]
    public void CloneTtsProviderForPersistence_ClampsSpeed()
    {
        var fastProvider = new TtsProviderConfig { Speed = 8.0 };
        var slowProvider = new TtsProviderConfig { Speed = 0.1 };

        Assert.Equal(4.0, TtsSettingsViewModel.CloneProviderForPersistence(fastProvider).Speed);
        Assert.Equal(0.25, TtsSettingsViewModel.CloneProviderForPersistence(slowProvider).Speed);
    }

    [Fact]
    public void CloneTtsConfig_ReturnsIndependentProviderList()
    {
        var config = new TtsConfig
        {
            SelectedProvider = "local",
            Providers =
            [
                new TtsProviderConfig
                {
                    Name = "local",
                    Type = TtsProviderType.Local,
                    Voice = "voice-a",
                    Speed = 1.2
                }
            ]
        };

        var clone = TtsSettingsViewModel.CloneTtsConfig(config);
        config.Providers[0].Voice = "voice-b";

        Assert.NotSame(config, clone);
        Assert.NotSame(config.Providers, clone.Providers);
        Assert.Equal("local", clone.SelectedProvider);
        Assert.Single(clone.Providers);
        Assert.Equal("voice-a", clone.Providers[0].Voice);
    }
}
