using WordLens.Models;

namespace WordLens.Test;

public class AppSettingsTests
{
    [Fact]
    public void Constructor_CreatesDefaultTranslationAndOcrProviders()
    {
        var settings = new AppSettings();

        Assert.Equal("zh-CN", settings.UILanguage);
        Assert.Equal("en", settings.LastTargetLanguage);
        Assert.Equal("OpenAI", settings.SelectedProvider);
        Assert.Single(settings.Providers);
        Assert.Equal("OpenAI", settings.Providers[0].Name);
        Assert.Equal("gpt-4o-mini", settings.Providers[0].Model);
        Assert.Single(settings.OcrProviders);
        Assert.Equal("OpenAI OCR", settings.OcrProviders[0].Name);
        Assert.True(settings.OcrProviders[0].IsEnabled);
    }

    [Fact]
    public void CreateDefaultTtsProvider_ReturnsDisabledLlmProvider()
    {
        var provider = AppSettings.CreateDefaultTtsProvider();

        Assert.Equal("OpenAI TTS", provider.Name);
        Assert.Equal(TtsProviderType.Llm, provider.Type);
        Assert.Equal("https://api.openai.com", provider.BaseUrl);
        Assert.Equal("gpt-4o-mini-tts", provider.Model);
        Assert.Equal("alloy", provider.Voice);
        Assert.False(provider.IsEnabled);
        Assert.Equal(1.0, provider.Speed);
    }

    [Fact]
    public void CreateDefaultLocalOcrProvider_ReturnsWindowsLocalProvider()
    {
        var provider = AppSettings.CreateDefaultLocalOcrProvider();

        Assert.Equal("Windows OCR", provider.Name);
        Assert.Equal(ProviderType.Local, provider.Type);
        Assert.True(provider.IsEnabled);
        Assert.False(provider.AllowManualModelInput);
        Assert.Equal(string.Empty, provider.BaseUrl);
        Assert.Equal(string.Empty, provider.Model);
        Assert.Equal("本地", provider.TypeDisplayName);
        Assert.Equal("Windows 系统 OCR", provider.Summary);
    }

    [Fact]
    public void TtsProviderConfig_Summary_UsesModelForLlmAndVoiceForLocal()
    {
        var provider = new TtsProviderConfig
        {
            Type = TtsProviderType.Llm,
            Model = "tts-model",
            Voice = "local-voice"
        };

        Assert.Equal("LLM", provider.TypeDisplayName);
        Assert.Equal("tts-model", provider.Summary);

        provider.Type = TtsProviderType.Local;

        Assert.Equal("本地", provider.TypeDisplayName);
        Assert.Equal("local-voice", provider.Summary);
    }
}
