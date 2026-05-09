using System.Net;
using System.Net.Http;
using WordLens.Models;

namespace WordLens.Services;

internal static class HttpClientFactoryExtensions
{
    public static HttpClient CreateClient(this IHttpClientFactory httpClientFactory, ProxyConfig proxyConfig)
    {
        if (!proxyConfig.Enabled)
            return httpClientFactory.CreateClient();

        var handler = new HttpClientHandler
        {
            UseProxy = true
        };

        if (proxyConfig.UseSystemProxy)
        {
            handler.Proxy = null;
            handler.DefaultProxyCredentials = CredentialCache.DefaultCredentials;
        }
        else
        {
            var proxy = new WebProxy(proxyConfig.Address, proxyConfig.Port);
            if (proxyConfig.UseAuthentication && !string.IsNullOrEmpty(proxyConfig.Username))
                proxy.Credentials = new NetworkCredential(proxyConfig.Username, proxyConfig.Password);

            handler.Proxy = proxy;
        }

        return new HttpClient(handler, disposeHandler: true);
    }
}
