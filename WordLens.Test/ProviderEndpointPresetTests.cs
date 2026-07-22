using WordLens.Models;

namespace WordLens.Test;

public class ProviderEndpointPresetTests
{
    [Fact]
    public void All_ContainsExpectedCorePresets()
    {
        var ids = ProviderEndpointPresets.All.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("openai", ids);
        Assert.Contains("deepseek", ids);
        Assert.Contains("ollama", ids);
        Assert.Contains("deepl-free", ids);
        Assert.Contains("deepl-pro", ids);
    }

    [Fact]
    public void CreateProvider_CopiesPresetFields()
    {
        var preset = ProviderEndpointPresets.All.First(p => p.Id == "deepseek");

        var provider = preset.CreateProvider("DeepSeek");

        Assert.Equal("DeepSeek", provider.Name);
        Assert.Equal(ProviderType.OpenAI, provider.Type);
        Assert.Equal("https://api.deepseek.com", provider.BaseUrl);
        Assert.Equal("deepseek-chat", provider.Model);
        Assert.True(provider.IsEnabled);
        Assert.Null(provider.ApiKey);
    }

    [Fact]
    public void ApplyTo_PreservesApiKeyAndCustomName()
    {
        var preset = ProviderEndpointPresets.All.First(p => p.Id == "ollama");
        var provider = new ProviderConfig
        {
            Name = "My Local LLM",
            Type = ProviderType.DeepL,
            BaseUrl = "https://api-free.deepl.com",
            Model = "ignored",
            ApiKey = "secret-key",
            SystemPromptTemplate = "custom system",
            UserPromptTemplate = "custom user"
        };

        preset.ApplyTo(provider);

        Assert.Equal("My Local LLM", provider.Name);
        Assert.Equal(ProviderType.OpenAI, provider.Type);
        Assert.Equal("http://127.0.0.1:11434", provider.BaseUrl);
        Assert.Equal("llama3.2", provider.Model);
        Assert.Equal("secret-key", provider.ApiKey);
        Assert.Equal("custom system", provider.SystemPromptTemplate);
        Assert.Equal("custom user", provider.UserPromptTemplate);
    }

    [Fact]
    public void ApplyTo_RenamesDefaultNewProviderName()
    {
        var preset = ProviderEndpointPresets.All.First(p => p.Id == "openai");
        var provider = new ProviderConfig
        {
            Name = "新翻译源 2",
            Type = ProviderType.OpenAI,
            BaseUrl = "https://example.com"
        };

        preset.ApplyTo(provider);

        Assert.Equal("OpenAI", provider.Name);
        Assert.Equal("https://api.openai.com", provider.BaseUrl);
        Assert.Equal("gpt-4o-mini", provider.Model);
    }

    [Fact]
    public void DeepLPresets_DisableManualModelInput()
    {
        var free = ProviderEndpointPresets.All.First(p => p.Id == "deepl-free");
        var pro = ProviderEndpointPresets.All.First(p => p.Id == "deepl-pro");

        Assert.Equal(ProviderType.DeepL, free.Type);
        Assert.False(free.AllowManualModelInput);
        Assert.Equal("https://api.deepl.com", pro.BaseUrl);
        Assert.False(pro.AllowManualModelInput);
    }
}
