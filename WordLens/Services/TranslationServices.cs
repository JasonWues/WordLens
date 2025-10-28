using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using WordLens.Models;
using WordLens.Services.Implementations.Translation;
using ZLogger;

namespace WordLens.Services;

public interface ITranslationProvider
{
    /// <summary>
    ///     非流式翻译
    /// </summary>
    Task<string> TranslateAsync(string text, string targetLanguage, string sourceLanguage, HttpClient httpClient,
        CancellationToken ct = default);

    /// <summary>
    ///     流式翻译，通过回调实时返回翻译内容
    /// </summary>
    /// <param name="text">要翻译的文本</param>
    /// <param name="targetLanguage">目标语言</param>
    /// <param name="sourceLanguage">源语言</param>
    /// <param name="httpClient">HTTP客户端</param>
    /// <param name="onUpdate">每收到新内容时的回调</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>完整的翻译结果</returns>
    Task<string> TranslateStreamAsync(
        string text,
        string targetLanguage,
        string sourceLanguage,
        HttpClient httpClient,
        Action<string> onUpdate,
        CancellationToken ct = default);
}

public class TranslationService
{
    private readonly IEncryptionService _encryptionService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TranslationService> _logger;
    private readonly ISettingsService _settings;

    public TranslationService(
        ISettingsService settings,
        IHttpClientFactory httpClientFactory,
        IEncryptionService encryptionService,
        ILogger<TranslationService> logger)
    {
        _settings = settings;
        _httpClientFactory = httpClientFactory;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    /// <summary>
    ///     并行翻译文本，使用所有启用的翻译源
    /// </summary>
    /// <param name="text">要翻译的文本</param>
    /// <param name="targetLanguage">目标语言代码</param>
    /// <param name="sourceLanguage">源语言代码（默认为"auto"自动检测）</param>
    /// <param name="translationResults">翻译结果</param>
    /// <param name="ct">取消令牌</param>
    public async Task<List<TranslationResult>> TranslateAsync(
        string text,
        string targetLanguage,
        string sourceLanguage = "auto",
        ObservableCollection<TranslationResult>? translationResults = null,
        CancellationToken ct = default)
    {
        var cfg = await _settings.LoadAsync();
        var enabledProviders = cfg.Providers.Where(p => p.IsEnabled).ToList();

        _logger.ZLogInformation(
            $"开始翻译，文本长度: {text.Length}，源语言: {sourceLanguage}，目标语言: {targetLanguage}，启用的翻译源数量: {enabledProviders.Count}");

        if (enabledProviders.Count == 0)
        {
            _logger.ZLogWarning($"没有启用的翻译源");
            return new List<TranslationResult>();
        }

        // 根据配置选择流式或非流式
        if (cfg.Streaming.Enabled)
        {
            _logger.ZLogInformation($"使用流式翻译模式");
            return await TranslateStreamAsync(text, targetLanguage, sourceLanguage, cfg,translationResults, ct);
        }
        else
        {
            _logger.ZLogInformation($"使用非流式翻译模式");
            // 为每个翻译源创建任务
            var tasks = enabledProviders.Select(provider =>
                TranslateSingleProviderAsync(provider, text, targetLanguage, sourceLanguage, cfg, ct)
            ).ToList();

            // 并行执行所有翻译任务
            var results = await Task.WhenAll(tasks);

            var successCount = results.Count(r => r.IsSuccess);
            _logger.ZLogInformation($"翻译完成，成功: {successCount}/{results.Length}");

            return results.ToList();
        }
    }

    /// <summary>
    ///     流式翻译所有启用的提供商
    /// </summary>
    private Task<List<TranslationResult>> TranslateStreamAsync(
        string text,
        string targetLanguage,
        string sourceLanguage,
        AppSettings settings,
        ObservableCollection<TranslationResult>? translationResults = null,
        CancellationToken ct = default)
    {
        var enabledProviders = settings.Providers.Where(p => p.IsEnabled).ToList();

        // 为每个提供商创建结果对象
        foreach (var provider in enabledProviders)
        {
            var result = new TranslationResult
            {
                ProviderName = provider.Name,
                IsLoading = true,
                Result = "" // 初始为空，将通过流式更新
            };
            translationResults.Add(result);
        }

        // 在后台启动所有流式翻译任务（不等待完成）
        _ = Task.Run(async () =>
        {
            try
            {
                var tasks = enabledProviders.Select((provider, index) =>
                    TranslateSingleProviderStreamAsync(
                        provider,
                        text,
                        targetLanguage,
                        sourceLanguage,
                        settings,
                        translationResults[index],
                        ct)
                ).ToList();

                await Task.WhenAll(tasks);

                var successCount = translationResults.Count(r => r.IsSuccess);
                _logger.ZLogInformation($"流式翻译完成，成功: {successCount}/{translationResults.Count}");
            }
            catch (Exception ex)
            {
                _logger.ZLogError(ex, $"流式翻译后台任务异常: {ex.Message}");
            }
        }, ct);

        // 立即返回results，让ViewModel可以显示并接收实时更新
        return Task.FromResult(new List<TranslationResult>());
    }

    /// <summary>
    ///     单个提供商的流式翻译
    /// </summary>
    private async Task TranslateSingleProviderStreamAsync(
        ProviderConfig providerCfg,
        string text,
        string targetLanguage,
        string sourceLanguage,
        AppSettings settings,
        TranslationResult result,
        CancellationToken ct)
    {
        try
        {
            _logger.ZLogInformation($"开始使用 {providerCfg.Name} 进行流式翻译");

            // 解密API Key
            var decryptedApiKey = string.IsNullOrEmpty(providerCfg.ApiKey)
                ? string.Empty
                : _encryptionService.Decrypt(providerCfg.ApiKey);

            ITranslationProvider provider = providerCfg.Type switch
            {
                ProviderType.OpenAI => new OpenAITranslationProvider(providerCfg, decryptedApiKey),
                _ => throw new NotSupportedException($"不支持的翻译源类型: {providerCfg.Type}")
            };

            var httpClient = CreateHttpClientWithProxy(settings.Proxy);

            // 流式翻译，通过回调实时更新result
            var fullResult = await provider.TranslateStreamAsync(
                text,
                targetLanguage,
                sourceLanguage,
                httpClient,
                content =>
                {
                    // 必须在UI线程更新，避免跨线程问题
                    Dispatcher.UIThread.Post(() =>
                    {
                        result.Result += content;
                    });
                },
                ct
            );

            result.IsSuccess = true;
            _logger.ZLogInformation($"{providerCfg.Name} 流式翻译成功，最终长度: {fullResult?.Length ?? 0}");
        }
        catch (OperationCanceledException)
        {
            result.IsSuccess = false;
            result.ErrorMessage = "翻译已取消";
            _logger.ZLogInformation($"{providerCfg.Name} 流式翻译被取消");
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            _logger.ZLogError(ex, $"{providerCfg.Name} 流式翻译失败: {ex.Message}");
        }
        finally
        {
            result.IsLoading = false;
        }
    }

    /// <summary>
    ///     单个翻译源的翻译任务
    /// </summary>
    private async Task<TranslationResult> TranslateSingleProviderAsync(
        ProviderConfig providerCfg,
        string text,
        string targetLanguage,
        string sourceLanguage,
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

            // 解密API Key
            var decryptedApiKey = string.IsNullOrEmpty(providerCfg.ApiKey)
                ? string.Empty
                : _encryptionService.Decrypt(providerCfg.ApiKey);

            _logger.ZLogDebug($"API Key已解密，长度: {decryptedApiKey.Length}");

            ITranslationProvider provider = providerCfg.Type switch
            {
                ProviderType.OpenAI => new OpenAITranslationProvider(providerCfg, decryptedApiKey),
                _ => throw new NotSupportedException($"不支持的翻译源类型: {providerCfg.Type}")
            };

            var httpClient = CreateHttpClientWithProxy(settings.Proxy);
            result.Result = await provider.TranslateAsync(
                text,
                targetLanguage,
                sourceLanguage,
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
        if (!proxyConfig.Enabled) return _httpClientFactory.CreateClient();

        var handler = new HttpClientHandler();

        // 使用系统代理
        if (proxyConfig.UseSystemProxy)
        {
            handler.UseProxy = true;
            handler.Proxy = null;
            handler.DefaultProxyCredentials = CredentialCache.DefaultCredentials;

            _logger.ZLogInformation($"使用系统代理配置");
        }
        else
        {
            // 使用自定义代理
            var proxy = new WebProxy(proxyConfig.Address, proxyConfig.Port);

            if (proxyConfig.UseAuthentication &&
                !string.IsNullOrEmpty(proxyConfig.Username))
                proxy.Credentials = new NetworkCredential(
                    proxyConfig.Username,
                    proxyConfig.Password
                );

            handler.Proxy = proxy;
            handler.UseProxy = true;

            _logger.ZLogInformation($"使用自定义代理: {proxyConfig.Address}:{proxyConfig.Port}");
        }

        return new HttpClient(handler);
    }
}