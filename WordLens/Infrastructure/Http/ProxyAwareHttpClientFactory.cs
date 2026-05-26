using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using WordLens.Models;

namespace WordLens.Infrastructure.Http;

public sealed class ProxyAwareHttpClientFactory : IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConcurrentDictionary<ProxyCacheKey, HttpClientHandler> _proxyHandlers = new();

    public ProxyAwareHttpClientFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public HttpClient CreateClient(ProxyConfig proxyConfig)
    {
        if (!proxyConfig.Enabled)
        {
            return _httpClientFactory.CreateClient();
        }

        var key = ProxyCacheKey.From(proxyConfig);
        var handler = _proxyHandlers.GetOrAdd(key, static cacheKey => CreateProxyHandler(cacheKey));

        return new HttpClient(handler, disposeHandler: false);
    }

    public void Dispose()
    {
        foreach (var handler in _proxyHandlers.Values)
        {
            handler.Dispose();
        }

        _proxyHandlers.Clear();
    }

    private static HttpClientHandler CreateProxyHandler(ProxyCacheKey key)
    {
        var handler = new HttpClientHandler
        {
            UseProxy = true
        };

        if (key.UseSystemProxy)
        {
            handler.Proxy = null;
            handler.DefaultProxyCredentials = CredentialCache.DefaultCredentials;
            return handler;
        }

        var proxy = new WebProxy(key.Address, key.Port);
        if (key.UseAuthentication && !string.IsNullOrEmpty(key.Username))
        {
            proxy.Credentials = new NetworkCredential(key.Username, key.Password);
        }

        handler.Proxy = proxy;
        return handler;
    }

    private readonly record struct ProxyCacheKey(
        bool UseSystemProxy,
        string Address,
        int Port,
        bool UseAuthentication,
        string? Username,
        string? Password)
    {
        public static ProxyCacheKey From(ProxyConfig proxyConfig)
        {
            return new ProxyCacheKey(
                proxyConfig.UseSystemProxy,
                proxyConfig.Address,
                proxyConfig.Port,
                proxyConfig.UseAuthentication,
                proxyConfig.Username,
                proxyConfig.Password);
        }
    }
}
