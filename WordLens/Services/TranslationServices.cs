using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WordLens.Models;

namespace WordLens.Services
{
    public interface ITranslationProvider
    {
        Task<string> TranslateAsync(string text, string targetLanguage,HttpClient httpClient, CancellationToken ct = default);
    }

    public class TranslationService
    {
        readonly private ISettingsService _settings;
        readonly private IHttpClientFactory _httpClientFactory;

        public TranslationService(ISettingsService settings,IHttpClientFactory httpClientFactory)
        {
            _settings = settings;
            _httpClientFactory = httpClientFactory;
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

            return await provider.TranslateAsync(text, cfg.TargetLanguage,_httpClientFactory.CreateClient(), ct);
        }
    }

    public class OpenAITranslationProvider : ITranslationProvider
    {
        readonly private ProviderConfig _config;

        public OpenAITranslationProvider(ProviderConfig config)
        {
            _config = config;

        }

        public async Task<string> TranslateAsync(string text, string targetLanguage,HttpClient httpClient, CancellationToken ct = default)
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

            using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions");
            req.Content = new StringContent(JsonSerializer.Serialize(payload,SourceGenerationContext.Default.ChatCompletionRequest), Encoding.UTF8, "application/json");

            using var resp = await httpClient.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;
            var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return content ?? string.Empty;
        }
    }
}