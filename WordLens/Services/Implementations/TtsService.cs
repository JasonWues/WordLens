using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WordLens.Models;
using ZLogger;

namespace WordLens.Services.Implementations;

public sealed class TtsService : ITtsService, IDisposable
{
    private readonly IAudioPlayerService _audioPlayer;
    private readonly EncryptionService _encryptionService;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ProxyAwareHttpClientFactory _httpClientFactory;
    private readonly ILogger<TtsService> _logger;
    private readonly ISettingsService _settingsService;

    public TtsService(
        ISettingsService settingsService,
        ProxyAwareHttpClientFactory httpClientFactory,
        EncryptionService encryptionService,
        IAudioPlayerService audioPlayer,
        ILogger<TtsService> logger)
    {
        _settingsService = settingsService;
        _httpClientFactory = httpClientFactory;
        _encryptionService = encryptionService;
        _audioPlayer = audioPlayer;
        _logger = logger;
    }

    public async Task SpeakAsync(string? text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        await _gate.WaitAsync(cancellationToken);

        try
        {
            _audioPlayer.Stop();

            var settings = await _settingsService.LoadAsync();
            var provider = SelectProvider(settings.Tts);
            if (provider == null)
            {
                _logger.ZLogWarning($"未配置启用的 TTS 源");
                return;
            }

            switch (provider.Type)
            {
                case TtsProviderType.Llm:
                    var waveData = await GenerateLlmSpeechAsync(provider, settings.Proxy, text.Trim(), cancellationToken);
                    await _audioPlayer.PlayWaveAsync(waveData, cancellationToken);
                    break;

                case TtsProviderType.Local:
                    _logger.ZLogWarning($"本地 TTS 源尚未接入平台朗读后端: {provider.Name}");
                    break;

                default:
                    _logger.ZLogWarning($"不支持的 TTS 源类型: {provider.Type}");
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"TTS 朗读失败: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Stop()
    {
        _audioPlayer.Stop();
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    private async Task<byte[]> GenerateLlmSpeechAsync(
        TtsProviderConfig provider,
        ProxyConfig proxy,
        string text,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(provider.BaseUrl))
            throw new InvalidOperationException("TTS API URL不能为空");

        if (string.IsNullOrWhiteSpace(provider.Model))
            throw new InvalidOperationException("TTS模型不能为空");

        var apiKey = string.IsNullOrWhiteSpace(provider.ApiKey)
            ? string.Empty
            : _encryptionService.Decrypt(provider.ApiKey);

        using var httpClient = _httpClientFactory.CreateClient(proxy);
        if (!string.IsNullOrWhiteSpace(apiKey))
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var requestArguments = OpenAIRequestArguments.Parse(
            provider.RequestArguments,
            "model",
            "input",
            "voice",
            "response_format",
            "speed");
        var request = new TtsSpeechRequest
        {
            Model = provider.Model,
            Input = text,
            Voice = Normalize(provider.Voice, "alloy"),
            ResponseFormat = "wav",
            Speed = Math.Clamp(provider.Speed, 0.25, 4.0),
            ExtensionData = requestArguments
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildSpeechUri(provider.BaseUrl))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(request, SourceGenerationContext.Default.TtsSpeechRequest),
                Encoding.UTF8,
                "application/json")
        };

        using var response = await httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var audio = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (audio.Length == 0)
            throw new InvalidOperationException("TTS API返回了空音频。");

        return audio;
    }

    private static TtsProviderConfig? SelectProvider(TtsConfig config)
    {
        var providers = config.Providers;
        if (providers.Count == 0)
            return null;

        return providers.FirstOrDefault(p => p.IsEnabled && p.Name == config.SelectedProvider) ??
               providers.FirstOrDefault(p => p.IsEnabled);
    }

    private static Uri BuildSpeechUri(string configuredUrl)
    {
        var trimmed = configuredUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("TTS API URL必须是完整URL");

        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.EndsWith("/v1/audio/speech", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/audio/speech", StringComparison.OrdinalIgnoreCase))
            return uri;

        return new Uri($"{trimmed.TrimEnd('/')}/v1/audio/speech");
    }

    private static string Normalize(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
