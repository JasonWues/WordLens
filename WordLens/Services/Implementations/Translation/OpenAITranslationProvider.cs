using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        var systemPrompt = "You are a professional, authentic translation engine. You only return the translated text, without any explanations";

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
                new() { Role = "user", Content = $"Please translate into {targetLanguage} (avoid explaining the original text):{text}" }
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

    public async Task<string> TranslateStreamAsync(
        string text,
        string targetLanguage,
        string sourceLanguage,
        HttpClient httpClient,
        Action<string> onUpdate,
        CancellationToken ct = default)
    {
        // 配置请求头
        if (!string.IsNullOrWhiteSpace(_decryptedApiKey))
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _decryptedApiKey);
        if (!string.IsNullOrWhiteSpace(_config.BaseUrl))
            httpClient.BaseAddress = new Uri(_config.BaseUrl);

        // 构建系统提示
        var systemPrompt = sourceLanguage == "auto"
            ? $"You are a translation engine. Translate to {targetLanguage}. Only return the translation."
            : $"You are a translation engine. Translate from {sourceLanguage} to {targetLanguage}. Only return the translation.";

        // 构建流式请求
        var payload = new ChatCompletionRequest
        {
            Model = _config.Model,
            Stream = true, // 启用流式输出
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

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, SourceGenerationContext.Default.ChatCompletionRequest),
                Encoding.UTF8,
                "application/json")
        };

        // 发送请求，使用ResponseHeadersRead立即返回，不等待全部内容
        var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        response.EnsureSuccessStatusCode();

        // 处理SSE流
        var fullContent = new StringBuilder();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // SSE格式: "data: {...}"
            if (line.StartsWith("data: "))
            {
                var jsonData = line.Substring(6);

                // 结束标记
                if (jsonData == "[DONE]") break;

                try
                {
                    // 解析JSON
                    var chunk = JsonSerializer.Deserialize<StreamChunk>(jsonData);
                    var content = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;

                    if (!string.IsNullOrEmpty(content))
                    {
                        fullContent.Append(content);
                        onUpdate(content); // 回调通知UI更新
                    }
                }
                catch (JsonException)
                {
                    // 忽略JSON解析错误，继续处理下一行
                    // 某些SSE消息可能格式不同或不完整
                    continue;
                }
            }
        }

        return fullContent.ToString();
    }
}