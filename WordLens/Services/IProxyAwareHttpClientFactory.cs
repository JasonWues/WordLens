using System.Net.Http;
using WordLens.Models;

namespace WordLens.Services;

public interface IProxyAwareHttpClientFactory
{
    HttpClient CreateClient(ProxyConfig proxyConfig);
}
