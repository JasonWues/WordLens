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

        readonly ILogger<TranslationService> _logger;


        public TranslationService(ISettingsService settings, IHttpClientFactory httpClientFactory,ILogger<TranslationService> logger)
        {
            _settings = settings;
            _httpClientFactory = httpClientFactory;
            _logger = logger;

        }

        public async Task<string> TranslateAsync(string text, CancellationToken ct = default)
        {
            var cfg = await _settings.LoadAsync();
            var providerCfg = cfg.Providers.FirstOrDefault(p => p.Name == cfg.SelectedProvider) ?? cfg.Providers.First();

            ITranslationProvider provider = providerCfg.Type switch
            {
                ProviderType.OpenAI => new OpenAITranslationProvider(providerCfg),
                _ => throw new NotSupportedException("Provider not supported")
            };

            var httpClient = CreateHttpClientWithProxy(cfg.Proxy);
            _logger.ZLogInformation($"Translating text using provider {providerCfg.Name} to language {cfg.TargetLanguage}");
            return await provider.TranslateAsync(text, cfg.TargetLanguage, httpClient, ct);
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
                    new()
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