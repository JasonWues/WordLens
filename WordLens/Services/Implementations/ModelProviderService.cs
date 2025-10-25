using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WordLens.Models;
using ZLogger;

namespace WordLens.Services.Implementations;

/// <summary>
///     模型提供服务接口
/// </summary>
public interface IModelProviderService
{
    /// <summary>
    ///     获取可用的模型列表
    /// </summary>
    /// <param name="apiKey">API密钥</param>
    /// <param name="baseUrl">API基础URL</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>模型信息列表</returns>
    Task<List<ModelInfo>> GetAvailableModelsAsync(
        string apiKey,
        string baseUrl,
        CancellationToken ct = default);
}

/// <summary>
///     OpenAI模型提供服务实现
/// </summary>
public class OpenAIModelProviderService : IModelProviderService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenAIModelProviderService> _logger;

    public OpenAIModelProviderService(
        IHttpClientFactory httpClientFactory,
        ILogger<OpenAIModelProviderService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<ModelInfo>> GetAvailableModelsAsync(
        string apiKey,
        string baseUrl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.ZLogWarning($"API Key为空，无法获取模型列表");
            throw new ArgumentException("API Key不能为空", nameof(apiKey));
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.ZLogWarning($"BaseUrl为空，无法获取模型列表");
            throw new ArgumentException("BaseUrl不能为空", nameof(baseUrl));
        }

        try
        {
            _logger.ZLogInformation($"开始获取模型列表，BaseUrl: {baseUrl}");

            using var httpClient = _httpClientFactory.CreateClient();

            // 配置HTTP客户端
            httpClient.BaseAddress = new Uri(baseUrl);
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            // 发送请求
            var response = await httpClient.GetAsync("/v1/models", ct);

            // 检查响应状态
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.ZLogError($"获取模型列表失败，状态码: {response.StatusCode}，错误: {errorContent}");
                throw new HttpRequestException(
                    $"API请求失败 (状态码: {response.StatusCode}): {errorContent}");
            }

            // 解析响应
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var modelResponse = await JsonSerializer.DeserializeAsync<OpenAIModelResponse>(
                stream,
                SourceGenerationContext.Default.OpenAIModelResponse,
                ct);

            if (modelResponse?.Data == null || modelResponse.Data.Count == 0)
            {
                _logger.ZLogWarning($"API返回的模型列表为空");
                return new List<ModelInfo>();
            }

            return modelResponse.Data
                .OrderByDescending(m => m.Created)
                .ThenBy(m => m.Id)
                .ToList();
        }
        catch (TaskCanceledException ex)
        {
            _logger.ZLogError(ex, $"获取模型列表超时");
            throw new TimeoutException("请求超时，请检查网络连接", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.ZLogError(ex, $"HTTP请求失败: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"获取模型列表时发生未知错误: {ex.Message}");
            throw;
        }
    }
}