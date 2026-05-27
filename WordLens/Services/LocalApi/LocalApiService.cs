using System;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WordLens.Models;
using ZLogger;

namespace WordLens.Services.LocalApi;

public sealed class LocalApiService : ILocalApiService
{
    private const int MinPort = 1024;
    private const int MaxPort = 65535;

    private readonly LocalApiBridge _bridge;
    private readonly ILogger<LocalApiService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private WebApplication? _app;
    private LocalApiConfig? _currentConfig;

    public LocalApiService(
        LocalApiBridge bridge,
        ILogger<LocalApiService> logger)
    {
        _bridge = bridge;
        _logger = logger;
    }

    public bool IsRunning => _app != null;

    public async Task ApplyConfigAsync(LocalApiConfig config)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (!config.Enabled)
            {
                await StopCoreAsync();
                _currentConfig = CloneConfig(config);
                return;
            }

            ValidateConfig(config);
            if (_app != null && IsSameRuntimeConfig(config, _currentConfig))
                return;

            await StopCoreAsync();
            await StartCoreAsync(config);
            _currentConfig = CloneConfig(config);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task StopAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            await StopCoreAsync();
            _currentConfig = null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _semaphore.Dispose();
    }

    private async Task StartCoreAsync(LocalApiConfig config)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(App).Assembly.FullName,
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = "Production"
        });

        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, config.Port, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1;
            });
            options.Limits.MaxRequestBodySize = 1_048_576;
        });

        builder.Services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, SourceGenerationContext.Default);
        });
        builder.Services.AddSingleton(_bridge);
        builder.Services.AddSingleton(CloneConfig(config));

        var app = builder.Build();
        MapEndpoints(app);

        _app = app;
        await app.StartAsync();
        _logger.ZLogInformation($"本地 API 已启动: http://127.0.0.1:{config.Port}");
    }

    private async Task StopCoreAsync()
    {
        if (_app == null)
            return;

        try
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _logger.ZLogInformation($"本地 API 已停止");
        }
        finally
        {
            _app = null;
        }
    }

    private static void MapEndpoints(WebApplication app)
    {
        app.Use(TokenAuthMiddleware);

        app.MapGet("/api/v1/health", () => Results.Ok(new LocalApiHealthResponse(
            true,
            "WordLens",
            typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown")));

        app.MapGet("/api/v1/settings/status", (LocalApiConfig config) => Results.Ok(new LocalApiStatusResponse(
            config.Enabled,
            config.Port,
            $"http://127.0.0.1:{config.Port}")));

        app.MapPost("/api/v1/translate", async (
            TranslateApiRequest request,
            LocalApiBridge bridge,
            CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await bridge.TranslateAsync(request, cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ApiErrorResponse(ex.Message));
            }
        });

        app.MapPost("/api/v1/window/translate", async (
            OpenTranslationWindowApiRequest request,
            LocalApiBridge bridge,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await bridge.OpenTranslationWindowAsync(request, cancellationToken);
                return Results.Accepted();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ApiErrorResponse(ex.Message));
            }
        });
    }

    private static async Task TokenAuthMiddleware(HttpContext context, Func<Task> next)
    {
        var config = context.RequestServices.GetRequiredService<LocalApiConfig>();
        var expected = $"Bearer {config.Token}";
        var actual = context.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(config.Token) ||
            !string.Equals(actual, expected, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new ApiErrorResponse("Unauthorized"), SourceGenerationContext.Default.ApiErrorResponse);
            return;
        }

        await next();
    }

    private static void ValidateConfig(LocalApiConfig config)
    {
        if (config.Port is < MinPort or > MaxPort)
            throw new InvalidOperationException($"本地 API 端口必须在 {MinPort}-{MaxPort} 之间。");

        if (string.IsNullOrWhiteSpace(config.Token))
            throw new InvalidOperationException("本地 API Token 不能为空。");
    }

    private static bool IsSameRuntimeConfig(LocalApiConfig left, LocalApiConfig? right)
    {
        return right != null &&
               left.Enabled == right.Enabled &&
               left.Port == right.Port &&
               string.Equals(left.Token, right.Token, StringComparison.Ordinal);
    }

    private static LocalApiConfig CloneConfig(LocalApiConfig config)
    {
        return new LocalApiConfig
        {
            Enabled = config.Enabled,
            Port = config.Port,
            Token = config.Token
        };
    }
}
