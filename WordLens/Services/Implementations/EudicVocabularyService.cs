using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WordLens.Infrastructure.Http;
using WordLens.Infrastructure.Security;
using WordLens.Models;
using ZLogger;

namespace WordLens.Services.Implementations;

public sealed class EudicVocabularyService : IEudicVocabularyService
{
    private const string AddWordUrl = "https://api.frdic.com/api/open/v1/studylist/word";

    private readonly EncryptionService _encryptionService;
    private readonly ProxyAwareHttpClientFactory _httpClientFactory;
    private readonly ILogger<EudicVocabularyService> _logger;
    private readonly ISettingsService _settingsService;

    public EudicVocabularyService(
        ISettingsService settingsService,
        ProxyAwareHttpClientFactory httpClientFactory,
        EncryptionService encryptionService,
        ILogger<EudicVocabularyService> logger)
    {
        _settingsService = settingsService;
        _httpClientFactory = httpClientFactory;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<EudicVocabularyAddResult> AddWordAsync(
        string word,
        string? contextLine,
        CancellationToken cancellationToken)
    {
        var trimmedWord = word.Trim();
        if (string.IsNullOrWhiteSpace(trimmedWord))
            return new EudicVocabularyAddResult(false, "原文为空，无法加入生词本");

        var settings = await _settingsService.LoadAsync();
        var config = settings.EudicVocabulary;

        if (!config.Enabled)
            return new EudicVocabularyAddResult(false, "未启用欧路生词本同步");

        if (string.IsNullOrWhiteSpace(config.Token))
            return new EudicVocabularyAddResult(false, "未配置欧路 OpenAPI Token");

        try
        {
            using var httpClient = _httpClientFactory.CreateClient(settings.Proxy);
            using var request = CreateRequest(config, trimmedWord, contextLine);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var message = TryReadMessage(content);

            if (response.IsSuccessStatusCode)
            {
                _logger.ZLogInformation($"欧路生词本添加成功，单词: {trimmedWord}");
                return new EudicVocabularyAddResult(true, string.IsNullOrWhiteSpace(message) ? "已加入欧路生词本" : message);
            }

            var failure = string.IsNullOrWhiteSpace(message)
                ? $"欧路接口返回 {(int)response.StatusCode}"
                : message;
            _logger.ZLogWarning($"欧路生词本添加失败，状态码: {(int)response.StatusCode}, 消息: {failure}");
            return new EudicVocabularyAddResult(false, failure);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"欧路生词本添加异常: {ex.Message}");
            return new EudicVocabularyAddResult(false, "加入欧路生词本失败");
        }
    }

    private HttpRequestMessage CreateRequest(EudicVocabularyConfig config, string word, string? contextLine)
    {
        var token = _encryptionService.IsEncrypted(config.Token)
            ? _encryptionService.Decrypt(config.Token)
            : config.Token;
        var payload = new EudicAddWordRequest
        {
            Language = NormalizeLanguage(config.Language),
            Word = word,
            Star = Math.Clamp(config.Star, 0, 5),
            ContextLine = string.IsNullOrWhiteSpace(contextLine) ? null : contextLine.Trim(),
            CategoryIds = ParseCategoryIds(config.CategoryId)
        };

        var json = JsonSerializer.Serialize(payload, SourceGenerationContext.Default.EudicAddWordRequest);
        var request = new HttpRequestMessage(HttpMethod.Post, AddWordUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("WordLens", "1.0"));
        request.Headers.TryAddWithoutValidation("Authorization", NormalizeAuthorizationHeader(token));
        return request;
    }

    private static string NormalizeLanguage(string? language)
    {
        var normalized = language?.Trim().ToLowerInvariant();
        return normalized is "en" or "fr" or "de" or "es" ? normalized : "en";
    }

    private static string NormalizeAuthorizationHeader(string token)
    {
        var trimmed = token.Trim();
        if (trimmed.StartsWith("NIS ", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        return trimmed.StartsWith("NIS", StringComparison.OrdinalIgnoreCase)
            ? $"NIS {trimmed[3..].TrimStart()}"
            : $"NIS {trimmed}";
    }

    private static string[]? ParseCategoryIds(string? categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
            return null;

        var ids = categoryId
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .ToArray();

        return ids.Length == 0 ? null : ids;
    }

    private static string TryReadMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        try
        {
            var response = JsonSerializer.Deserialize(
                content,
                SourceGenerationContext.Default.EudicApiMessageResponse);
            return response?.Message ?? string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
}

public sealed class EudicAddWordRequest
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("word")]
    public string Word { get; set; } = string.Empty;

    [JsonPropertyName("star")]
    public int Star { get; set; } = 1;

    [JsonPropertyName("context_line")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContextLine { get; set; }

    [JsonPropertyName("category_ids")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? CategoryIds { get; set; }
}

public sealed class EudicApiMessageResponse
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
