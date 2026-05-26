using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using WordLens.Models;
using WordLens.Services.Implementations.Translation;

namespace WordLens.Test;

public class DeepLTranslationProviderTests
{
    [Fact]
    public async Task TranslateAsync_SendsDeepLRequestAndReturnsFirstTranslation()
    {
        var handler = new CapturingHandler(
            """{"translations":[{"detected_source_language":"EN","text":"Hallo"}]}""");
        using var httpClient = new HttpClient(handler);
        var provider = new DeepLTranslationProvider(
            new ProviderConfig
            {
                Type = ProviderType.DeepL,
                RequestArguments = """{"formality":"more"}"""
            },
            "secret-key");

        var result = await provider.TranslateAsync(
            "Hello",
            "de",
            "en",
            httpClient,
            TestContext.Current.CancellationToken);

        Assert.Equal("Hallo", result);
        Assert.Equal(new Uri("https://api-free.deepl.com/v2/translate"), handler.RequestUri);
        Assert.Equal("DeepL-Auth-Key", handler.AuthorizationScheme);
        Assert.Equal("secret-key", handler.AuthorizationParameter);

        using var body = JsonDocument.Parse(handler.RequestBody);
        var root = body.RootElement;
        Assert.Equal("Hello", root.GetProperty("text")[0].GetString());
        Assert.Equal("DE", root.GetProperty("target_lang").GetString());
        Assert.Equal("EN", root.GetProperty("source_lang").GetString());
        Assert.Equal("more", root.GetProperty("formality").GetString());
    }

    [Fact]
    public async Task TranslateAsync_OmitsSourceLanguage_WhenSourceIsAuto()
    {
        var handler = new CapturingHandler(
            """{"translations":[{"detected_source_language":"JA","text":"Hello"}]}""");
        using var httpClient = new HttpClient(handler);
        var provider = new DeepLTranslationProvider(
            new ProviderConfig { Type = ProviderType.DeepL },
            "secret-key");

        await provider.TranslateAsync(
            "こんにちは",
            "en",
            "auto",
            httpClient,
            TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(handler.RequestBody);
        Assert.False(body.RootElement.TryGetProperty("source_lang", out _));
        Assert.Equal("EN-US", body.RootElement.GetProperty("target_lang").GetString());
    }

    [Fact]
    public async Task TranslateStreamAsync_UsesNonStreamingRequestAndPublishesSingleUpdate()
    {
        var handler = new CapturingHandler(
            """{"translations":[{"detected_source_language":"EN","text":"Bonjour"}]}""");
        using var httpClient = new HttpClient(handler);
        var provider = new DeepLTranslationProvider(
            new ProviderConfig { Type = ProviderType.DeepL },
            "secret-key");
        var updates = new List<string>();

        var result = await provider.TranslateStreamAsync(
            "Hello",
            "fr",
            "en",
            httpClient,
            update =>
            {
                updates.Add(update);
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal("Bonjour", result);
        Assert.Equal(["Bonjour"], updates);
    }

    [Fact]
    public void NormalizeLanguage_MapsWordLensCodesToDeepLCodes()
    {
        Assert.Equal("EN-US", DeepLTranslationProvider.NormalizeTargetLanguage("en"));
        Assert.Equal("PT-PT", DeepLTranslationProvider.NormalizeTargetLanguage("pt"));
        Assert.Equal("ZH", DeepLTranslationProvider.NormalizeTargetLanguage("zh-CN"));
        Assert.Equal("JA", DeepLTranslationProvider.NormalizeSourceLanguage("ja"));
        Assert.Null(DeepLTranslationProvider.NormalizeSourceLanguage("auto"));
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public CapturingHandler(string responseBody)
        {
            _responseBody = responseBody;
        }

        public string? AuthorizationParameter { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        public string RequestBody { get; private set; } = string.Empty;

        public Uri? RequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            RequestBody = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
