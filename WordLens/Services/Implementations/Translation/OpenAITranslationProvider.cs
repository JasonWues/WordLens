using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WordLens.Models;

namespace WordLens.Services.Implementations.Translation;

public class OpenAITranslationProvider : ITranslationProvider
{
    private readonly ProviderConfig _config;
    private readonly string _decryptedApiKey;

    public OpenAITranslationProvider(ProviderConfig config, string decryptedApiKey)
    {
        _config = config;
        _decryptedApiKey = decryptedApiKey;
    }

    public async Task<string> TranslateAsync(string text, string targetLanguage, string sourceLanguage,
        HttpClient httpClient, CancellationToken ct = default)
    {
        // 使用解密后的API Key
        if (!string.IsNullOrWhiteSpace(_decryptedApiKey))
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _decryptedApiKey);
        if (!string.IsNullOrWhiteSpace(_config.BaseUrl)) httpClient.BaseAddress = new Uri(_config.BaseUrl);

        // 构建系统提示，包含源语言和目标语言信息
        var systemPrompt = sourceLanguage == "auto"
            ? $"You are a translation engine. Translate to {targetLanguage}. Only return the translation."
            : $"You are a translation engine. Translate from {sourceLanguage} to {targetLanguage}. Only return the translation.";

        var payload = new ChatCompletionRequest
        {
            Model = _config.Model,
            Messages = new List<ChatMessage>
            {
                new()
                {
                    Role = "system",
                    Content = systemPrompt
                },
                new() { Role = "user", Content = text }
            }
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions");
        req.Content = new StringContent(
            JsonSerializer.Serialize(payload, SourceGenerationContext.Default.ChatCompletionRequest),
            Encoding.UTF8,
            "application/json"
        );

        using var resp = await httpClient.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;
        var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        return content ?? string.Empty;
    }
}