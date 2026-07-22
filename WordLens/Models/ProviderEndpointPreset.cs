using System;
using System.Collections.Generic;

namespace WordLens.Models;

/// <summary>
///     常用翻译源端点预设，用于快速填充 Base URL / 模型 / 类型。
/// </summary>
public sealed class ProviderEndpointPreset
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public string Description { get; init; } = string.Empty;

    public required ProviderType Type { get; init; }

    /// <summary>
    ///     新建翻译源时的默认名称。
    /// </summary>
    public required string DefaultName { get; init; }

    public required string BaseUrl { get; init; }

    /// <summary>
    ///     默认模型。DeepL 等非 OpenAI 兼容源可为空。
    /// </summary>
    public string Model { get; init; } = string.Empty;

    public string RequestArguments { get; init; } = string.Empty;

    public bool AllowManualModelInput { get; init; } = true;

    public ProviderConfig CreateProvider(string name)
    {
        return new ProviderConfig
        {
            Name = name,
            Type = Type,
            BaseUrl = BaseUrl,
            Model = Model,
            ApiKey = null,
            IsEnabled = true,
            RequestArguments = RequestArguments,
            SystemPromptTemplate = string.Empty,
            UserPromptTemplate = string.Empty,
            AllowManualModelInput = AllowManualModelInput
        };
    }

    public void ApplyTo(ProviderConfig provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        provider.Type = Type;
        provider.BaseUrl = BaseUrl;
        provider.Model = Model;
        provider.RequestArguments = RequestArguments;
        provider.AllowManualModelInput = AllowManualModelInput;

        // 名称仍为空或仍是默认“新翻译源”时才覆盖，避免冲掉用户自定义命名
        if (string.IsNullOrWhiteSpace(provider.Name) ||
            provider.Name.StartsWith("新翻译源", StringComparison.Ordinal))
            provider.Name = DefaultName;
    }
}

public static class ProviderEndpointPresets
{
    public static IReadOnlyList<ProviderEndpointPreset> All { get; } =
    [
        new()
        {
            Id = "openai",
            DisplayName = "OpenAI",
            Description = "官方 OpenAI API",
            Type = ProviderType.OpenAI,
            DefaultName = "OpenAI",
            BaseUrl = "https://api.openai.com",
            Model = "gpt-4o-mini"
        },
        new()
        {
            Id = "deepseek",
            DisplayName = "DeepSeek",
            Description = "DeepSeek 官方 OpenAI 兼容接口",
            Type = ProviderType.OpenAI,
            DefaultName = "DeepSeek",
            BaseUrl = "https://api.deepseek.com",
            Model = "deepseek-chat"
        },
        new()
        {
            Id = "siliconflow",
            DisplayName = "硅基流动 SiliconFlow",
            Description = "国内聚合 OpenAI 兼容接口",
            Type = ProviderType.OpenAI,
            DefaultName = "SiliconFlow",
            BaseUrl = "https://api.siliconflow.cn",
            Model = "deepseek-ai/DeepSeek-V3"
        },
        new()
        {
            Id = "openrouter",
            DisplayName = "OpenRouter",
            Description = "多模型聚合，Base URL 为 openrouter.ai/api",
            Type = ProviderType.OpenAI,
            DefaultName = "OpenRouter",
            BaseUrl = "https://openrouter.ai/api",
            Model = "openai/gpt-4o-mini"
        },
        new()
        {
            Id = "groq",
            DisplayName = "Groq",
            Description = "高速推理，OpenAI 兼容路径",
            Type = ProviderType.OpenAI,
            DefaultName = "Groq",
            BaseUrl = "https://api.groq.com/openai",
            Model = "llama-3.3-70b-versatile"
        },
        new()
        {
            Id = "moonshot",
            DisplayName = "月之暗面 Kimi",
            Description = "Moonshot OpenAI 兼容接口",
            Type = ProviderType.OpenAI,
            DefaultName = "Kimi",
            BaseUrl = "https://api.moonshot.cn",
            Model = "moonshot-v1-8k"
        },
        new()
        {
            Id = "dashscope",
            DisplayName = "通义千问 DashScope",
            Description = "阿里云兼容模式",
            Type = ProviderType.OpenAI,
            DefaultName = "DashScope",
            BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode",
            Model = "qwen-plus"
        },
        new()
        {
            Id = "mistral",
            DisplayName = "Mistral",
            Description = "Mistral 官方 API",
            Type = ProviderType.OpenAI,
            DefaultName = "Mistral",
            BaseUrl = "https://api.mistral.ai",
            Model = "mistral-small-latest"
        },
        new()
        {
            Id = "together",
            DisplayName = "Together AI",
            Description = "Together OpenAI 兼容接口",
            Type = ProviderType.OpenAI,
            DefaultName = "Together",
            BaseUrl = "https://api.together.xyz",
            Model = "meta-llama/Meta-Llama-3.1-8B-Instruct-Turbo"
        },
        new()
        {
            Id = "ollama",
            DisplayName = "Ollama (本地)",
            Description = "本机 Ollama，默认 11434；需先拉取模型",
            Type = ProviderType.OpenAI,
            DefaultName = "Ollama",
            BaseUrl = "http://127.0.0.1:11434",
            Model = "llama3.2"
        },
        new()
        {
            Id = "lmstudio",
            DisplayName = "LM Studio (本地)",
            Description = "本机 LM Studio 本地服务器，默认 1234",
            Type = ProviderType.OpenAI,
            DefaultName = "LM Studio",
            BaseUrl = "http://127.0.0.1:1234",
            Model = string.Empty
        },
        new()
        {
            Id = "deepl-free",
            DisplayName = "DeepL Free",
            Description = "DeepL 免费 API（api-free）",
            Type = ProviderType.DeepL,
            DefaultName = "DeepL Free",
            BaseUrl = "https://api-free.deepl.com",
            Model = string.Empty,
            AllowManualModelInput = false
        },
        new()
        {
            Id = "deepl-pro",
            DisplayName = "DeepL Pro",
            Description = "DeepL 付费 API",
            Type = ProviderType.DeepL,
            DefaultName = "DeepL Pro",
            BaseUrl = "https://api.deepl.com",
            Model = string.Empty,
            AllowManualModelInput = false
        }
    ];
}
