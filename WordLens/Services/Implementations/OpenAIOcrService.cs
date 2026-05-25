using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WordLens.Models;
using ZLogger;

namespace WordLens.Services.Implementations;

public class OpenAIOcrService : IOcrService
{
    private readonly EncryptionService _encryptionService;
    private readonly ILogger<OpenAIOcrService> _logger;
    private readonly ProxyAwareHttpClientFactory _httpClientFactory;
    private readonly ISettingsService _settingsService;

    public OpenAIOcrService(
        ISettingsService settingsService,
        ProxyAwareHttpClientFactory httpClientFactory,
        EncryptionService encryptionService,
        ILogger<OpenAIOcrService> logger)
    {
        _settingsService = settingsService;
        _httpClientFactory = httpClientFactory;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<string?> RecognizeTextAsync(
        WriteableBitmap bitmap,
        string languageCode = "auto",
        string? providerName = null)
    {
        var settings = await _settingsService.LoadAsync();
        var provider = SelectOcrProvider(settings, providerName);

        if (provider == null)
            throw new InvalidOperationException("未配置OCR源");

        if (!provider.IsEnabled)
        {
            _logger.ZLogWarning($"OCR源未启用: {provider.Name}");
            return null;
        }

        if (string.IsNullOrWhiteSpace(provider.BaseUrl))
            throw new InvalidOperationException("OCR API URL不能为空");

        if (string.IsNullOrWhiteSpace(provider.Model))
            throw new InvalidOperationException("OCR模型不能为空");

        var apiKey = string.IsNullOrWhiteSpace(provider.ApiKey)
            ? string.Empty
            : _encryptionService.Decrypt(provider.ApiKey);

        using var httpClient = _httpClientFactory.CreateClient(settings.Proxy);
        if (!string.IsNullOrWhiteSpace(apiKey))
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var imageDataUrl = CreateOcrPngDataUrl(bitmap);
        var requestArguments = OpenAIRequestArguments.Parse(provider.RequestArguments, "model", "messages");
        var request = new OcrChatCompletionRequest
        {
            Model = provider.Model,
            MaxTokens = requestArguments?.ContainsKey("max_tokens") == true ? null : 2000,
            ExtensionData = requestArguments,
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
                            Text = BuildOcrPrompt(provider, languageCode)
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
        var provider = SelectOcrProvider(settings);
        return provider != null &&
               provider.IsEnabled &&
               !string.IsNullOrWhiteSpace(provider.BaseUrl) &&
               !string.IsNullOrWhiteSpace(provider.Model);
    }

    public Task<string[]> GetSupportedLanguagesAsync()
    {
        return Task.FromResult(new[] { "auto", "zh-CN", "en-US", "ja-JP", "ko-KR" });
    }

    private string CreateOcrPngDataUrl(WriteableBitmap bitmap)
    {
        try
        {
            using var framebuffer = bitmap.Lock();
            var png = OcrImageProcessor.PreprocessBgraToPng(
                framebuffer.Address,
                bitmap.PixelSize.Width,
                bitmap.PixelSize.Height,
                framebuffer.RowBytes);

            _logger.ZLogDebug($"OCR 图片预处理完成，PNG大小: {png.Length} bytes");
            return $"data:image/png;base64,{Convert.ToBase64String(png)}";
        }
        catch (Exception ex)
        {
            _logger.ZLogWarning(ex, $"OCR 图片预处理失败，回退到原始截图编码: {ex.Message}");
            return CreatePngDataUrl(bitmap);
        }
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

    private static string BuildOcrPrompt(ProviderConfig provider, string languageCode)
    {
        var languageHint = string.IsNullOrWhiteSpace(languageCode) || languageCode == "auto"
            ? "Detect the text language automatically."
            : $"The expected text language is {languageCode}.";

        var defaultPrompt =
            $"{languageHint} Extract all readable text from the image. Preserve line breaks as much as possible. Return only the extracted text. If there is no readable text, return an empty string.";
        var template = string.IsNullOrWhiteSpace(provider.UserPromptTemplate)
            ? defaultPrompt
            : provider.UserPromptTemplate;

        return PromptTemplateRenderer.RenderOcr(template, languageCode);
    }

    private static ProviderConfig? SelectOcrProvider(AppSettings settings, string? providerName = null)
    {
        var providers = settings.OcrProviders ?? new List<ProviderConfig>();
        if (providers.Count == 0)
            return null;

        var selectedName = string.IsNullOrWhiteSpace(providerName)
            ? settings.SelectedOcrProvider
            : providerName;

        return providers.FirstOrDefault(p => p.IsEnabled && p.Name == selectedName) ??
               providers.FirstOrDefault(p => p.IsEnabled) ??
               providers.FirstOrDefault(p => p.Name == selectedName) ??
               providers[0];
    }
}
