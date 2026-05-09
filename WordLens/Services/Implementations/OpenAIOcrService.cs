using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using WordLens.Models;
using ZLogger;

namespace WordLens.Services.Implementations;

public class OpenAIOcrService : IOcrService
{
    private readonly IEncryptionService _encryptionService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenAIOcrService> _logger;
    private readonly ISettingsService _settingsService;

    public OpenAIOcrService(
        ISettingsService settingsService,
        IHttpClientFactory httpClientFactory,
        IEncryptionService encryptionService,
        ILogger<OpenAIOcrService> logger)
    {
        _settingsService = settingsService;
        _httpClientFactory = httpClientFactory;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<string?> RecognizeTextAsync(WriteableBitmap bitmap, string languageCode = "auto")
    {
        var settings = await _settingsService.LoadAsync();
        var provider = settings.OcrProvider;

        if (!provider.IsEnabled)
        {
            _logger.ZLogWarning($"OCR源未启用");
            return null;
        }

        if (string.IsNullOrWhiteSpace(provider.BaseUrl))
            throw new InvalidOperationException("OCR API URL不能为空");

        if (string.IsNullOrWhiteSpace(provider.Model))
            throw new InvalidOperationException("OCR模型不能为空");

        var apiKey = string.IsNullOrWhiteSpace(provider.ApiKey)
            ? string.Empty
            : _encryptionService.Decrypt(provider.ApiKey);

        using var httpClient = CreateHttpClientWithProxy(settings.Proxy);
        if (!string.IsNullOrWhiteSpace(apiKey))
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var imageDataUrl = CreatePngDataUrl(bitmap);
        var request = new OcrChatCompletionRequest
        {
            Model = provider.Model,
            Messages = new List<OcrChatMessage>
            {
                new()
                {
                    Role = "user",
                    Content = new List<OcrContentPart>
                    {
                        new()
                        {
                            Type = "text",
                            Text = BuildOcrPrompt(languageCode)
                        },
                        new()
                        {
                            Type = "image_url",
                            ImageUrl = new OcrImageUrl
                            {
                                Url = imageDataUrl,
                                Detail = "high"
                            }
                        }
                    }
                }
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri(provider.BaseUrl))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(request, SourceGenerationContext.Default.OcrChatCompletionRequest),
                Encoding.UTF8,
                "application/json")
        };

        using var response = await httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(responseStream);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        var text = content?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.ZLogWarning($"OCR返回空文本");
            return null;
        }

        _logger.ZLogInformation($"OCR识别成功，文本长度: {text.Length}");
        return text;
    }

    public async Task<bool> IsAvailableAsync()
    {
        var settings = await _settingsService.LoadAsync();
        return settings.OcrProvider.IsEnabled &&
               !string.IsNullOrWhiteSpace(settings.OcrProvider.BaseUrl) &&
               !string.IsNullOrWhiteSpace(settings.OcrProvider.Model);
    }

    public Task<string[]> GetSupportedLanguagesAsync()
    {
        return Task.FromResult(new[] { "auto", "zh-CN", "en-US", "ja-JP", "ko-KR" });
    }

    private HttpClient CreateHttpClientWithProxy(ProxyConfig proxyConfig)
    {
        if (!proxyConfig.Enabled) return _httpClientFactory.CreateClient();

        var handler = new HttpClientHandler();
        if (proxyConfig.UseSystemProxy)
        {
            handler.UseProxy = true;
            handler.Proxy = null;
            handler.DefaultProxyCredentials = CredentialCache.DefaultCredentials;
        }
        else
        {
            var proxy = new WebProxy(proxyConfig.Address, proxyConfig.Port);
            if (proxyConfig.UseAuthentication &&
                !string.IsNullOrEmpty(proxyConfig.Username))
                proxy.Credentials = new NetworkCredential(
                    proxyConfig.Username,
                    proxyConfig.Password);

            handler.Proxy = proxy;
            handler.UseProxy = true;
        }

        return new HttpClient(handler);
    }

    private static string CreatePngDataUrl(WriteableBitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        var base64 = Convert.ToBase64String(stream.ToArray());
        return $"data:image/png;base64,{base64}";
    }

    private static Uri BuildChatCompletionsUri(string configuredUrl)
    {
        var trimmed = configuredUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("OCR API URL必须是完整URL");

        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.EndsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            return uri;

        return new Uri($"{trimmed.TrimEnd('/')}/v1/chat/completions");
    }

    private static string BuildOcrPrompt(string languageCode)
    {
        var languageHint = string.IsNullOrWhiteSpace(languageCode) || languageCode == "auto"
            ? "Detect the text language automatically."
            : $"The expected text language is {languageCode}.";

        return $"{languageHint} Extract all readable text from the image. Preserve line breaks as much as possible. Return only the extracted text. If there is no readable text, return an empty string.";
    }
}
