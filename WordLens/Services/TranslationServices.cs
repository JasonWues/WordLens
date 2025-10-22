using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WordLens.Models;
using ZLogger;

namespace WordLens.Services
{
    public interface ITranslationProvider
    {
        Task<string> TranslateAsync(string text, string targetLanguage, HttpClient httpClient, CancellationToken ct = default);
    }

    public class TranslationService
    {
        private readonly ISettingsService _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TranslationService> _logger;

        public TranslationService(ISettingsService settings, IHttpClientFactory httpClientFactory, ILogger<TranslationService> logger)
        {
            _settings = settings;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// 并行翻译文本，使用所有启用的翻译源
        /// </summary>
        public async Task<List<TranslationResult>> TranslateAsync(string text, CancellationToken ct = default)
        {
            var cfg = await _settings.LoadAsync();
            var enabledProviders = cfg.Providers.Where(p => p.IsEnabled).ToList();

            _logger.ZLogInformation($"开始翻译，文本长度: {text.Length}，启用的翻译源数量: {enabledProviders.Count}");

            if (enabledProviders.Count == 0)
            {
                _logger.ZLogWarning($"没有启用的翻译源");
                return new List<TranslationResult>();
            }

            // 为每个翻译源创建任务
            var tasks = enabledProviders.Select(provider =>
                TranslateSingleProviderAsync(provider, text, cfg, ct)
            ).ToList();

            // 并行执行所有翻译任务
            var results = await Task.WhenAll(tasks);

            var successCount = results.Count(r => r.IsSuccess);
            _logger.ZLogInformation($"翻译完成，成功: {successCount}/{results.Length}");

            return results.ToList();
        }

        /// <summary>
        /// 单个翻译源的翻译任务
        /// </summary>
        private async Task<TranslationResult> TranslateSingleProviderAsync(
            ProviderConfig providerCfg,
            string text,
            AppSettings settings,
            CancellationToken ct)
        {
            var result = new TranslationResult
            {
                ProviderName = providerCfg.Name,
                IsLoading = true
            };

            try
            {
                _logger.ZLogInformation($"开始使用 {providerCfg.Name} 翻译");

                ITranslationProvider provider = providerCfg.Type switch
                {
                    ProviderType.OpenAI => new OpenAITranslationProvider(providerCfg),
                    _ => throw new NotSupportedException($"不支持的翻译源类型: {providerCfg.Type}")
                };

                var httpClient = CreateHttpClientWithProxy(settings.Proxy);
                result.Result = await provider.TranslateAsync(
                    text,
                    settings.TargetLanguage,
                    httpClient,
                    ct
                );
                result.IsSuccess = true;

                _logger.ZLogInformation($"{providerCfg.Name} 翻译成功，结果长度: {result.Result?.Length ?? 0}");
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;

                _logger.ZLogError(ex, $"{providerCfg.Name} 翻译失败: {ex.Message}");
            }
            finally
            {
                result.IsLoading = false;
            }

            return result;
        }

        private HttpClient CreateHttpClientWithProxy(ProxyConfig proxyConfig)
        {
            if (!proxyConfig.Enabled)
            {
                return _httpClientFactory.CreateClient();
            }

            var handler = new HttpClientHandler();
            
            var proxy = new WebProxy(proxyConfig.Address, proxyConfig.Port);
            
            if (proxyConfig.UseAuthentication &&
                !string.IsNullOrEmpty(proxyConfig.Username))
            {
                proxy.Credentials = new NetworkCredential(
                    proxyConfig.Username,
                    proxyConfig.Password
                );
            }

            handler.Proxy = proxy;
            handler.UseProxy = true;

            return new HttpClient(handler);
        }
    }

    public class OpenAITranslationProvider : ITranslationProvider
    {
        private readonly ProviderConfig _config;

        public OpenAITranslationProvider(ProviderConfig config)
        {
            _config = config;
        }

        public async Task<string> TranslateAsync(string text, string targetLanguage, HttpClient httpClient, CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(_config.ApiKey))
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
            }
            if (!string.IsNullOrWhiteSpace(_config.BaseUrl))
            {
                httpClient.BaseAddress = new Uri(_config.BaseUrl);
            }
            
            var payload = new ChatCompletionRequest
            {
                Model = _config.Model,
                Messages = new List<ChatMessage>
                {
                    new ChatMessage
                    {
                        Role = "system",
                        Content = $"You are a translation engine. Translate to {targetLanguage}. Only return the translation."
                    },
                    new ChatMessage { Role = "user", Content = text }
                }
            };

            var req = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions");
            req.Content = new StringContent(
                JsonSerializer.Serialize(payload, SourceGenerationContext.Default.ChatCompletionRequest),
                Encoding.UTF8,
                "application/json"
            );

            var resp = await httpClient.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            
            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;
            var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return content ?? string.Empty;
        }
    }
}