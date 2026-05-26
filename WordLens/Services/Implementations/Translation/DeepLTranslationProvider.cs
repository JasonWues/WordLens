using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WordLens.Models;
using WordLens.Providers.OpenAI;

namespace WordLens.Services.Implementations.Translation;

public class DeepLTranslationProvider : ITranslationProvider
{
    public const string DefaultBaseUrl = "https://api-free.deepl.com";

    private static readonly Dictionary<string, string> SourceLanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zh-CN"] = "ZH",
        ["zh-TW"] = "ZH",
        ["en"] = "EN",
        ["ja"] = "JA",
        ["ko"] = "KO",
        ["fr"] = "FR",
        ["de"] = "DE",
        ["es"] = "ES",
        ["ru"] = "RU",
        ["ar"] = "AR",
        ["pt"] = "PT",
        ["it"] = "IT"
    };

    private static readonly Dictionary<string, string> TargetLanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zh-CN"] = "ZH",
        ["zh-TW"] = "ZH",
        ["en"] = "EN-US",
        ["ja"] = "JA",
        ["ko"] = "KO",
        ["fr"] = "FR",
        ["de"] = "DE",
        ["es"] = "ES",
        ["ru"] = "RU",
        ["ar"] = "AR",
        ["pt"] = "PT-PT",
        ["it"] = "IT"
    };

    private readonly ProviderConfig _config;
    private readonly string _decryptedApiKey;

    public DeepLTranslationProvider(ProviderConfig config, string decryptedApiKey)
    {
        _config = config;
        _decryptedApiKey = decryptedApiKey;
    }

    public async Task<string> TranslateAsync(
        string text,
        string targetLanguage,
        string sourceLanguage,
        HttpClient httpClient,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_decryptedApiKey))
            throw new InvalidOperationException("DeepL API Key 不能为空。");

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("DeepL-Auth-Key", _decryptedApiKey);
        httpClient.BaseAddress = new Uri(
            string.IsNullOrWhiteSpace(_config.BaseUrl) ? DefaultBaseUrl : _config.BaseUrl.Trim());

        var payload = new DeepLTranslationRequest
        {
            Text = [text],
            TargetLanguage = NormalizeTargetLanguage(targetLanguage),
            SourceLanguage = NormalizeSourceLanguage(sourceLanguage),
            ExtensionData = OpenAIRequestArguments.Parse(
                _config.RequestArguments,
                "text",
                "target_lang",
                "source_lang")
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v2/translate")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, SourceGenerationContext.Default.DeepLTranslationRequest),
                Encoding.UTF8,
                "application/json")
        };

        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"DeepL 翻译请求失败: {(int)response.StatusCode} {response.ReasonPhrase} {errorBody}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var result = await JsonSerializer.DeserializeAsync(
            stream,
            SourceGenerationContext.Default.DeepLTranslationResponse,
            ct);

        return result?.Translations.Count > 0
            ? result.Translations[0].Text
            : string.Empty;
    }

    public async Task<string> TranslateStreamAsync(
        string text,
        string targetLanguage,
        string sourceLanguage,
        HttpClient httpClient,
        Func<string, Task> onUpdate,
        CancellationToken ct = default)
    {
        var result = await TranslateAsync(text, targetLanguage, sourceLanguage, httpClient, ct);
        if (!string.IsNullOrEmpty(result))
            await onUpdate(result);

        return result;
    }

    public static string NormalizeTargetLanguage(string languageCode)
    {
        return TargetLanguageMap.TryGetValue(languageCode, out var mapped)
            ? mapped
            : languageCode.ToUpperInvariant();
    }

    public static string? NormalizeSourceLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode) ||
            string.Equals(languageCode, "auto", StringComparison.OrdinalIgnoreCase))
            return null;

        return SourceLanguageMap.TryGetValue(languageCode, out var mapped)
            ? mapped
            : languageCode.ToUpperInvariant();
    }
}
