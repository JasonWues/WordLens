using System;
using System.Threading.Tasks;
using WordLens.Models;

namespace WordLens.Services.LocalApi;

public interface ILocalApiService : IAsyncDisposable
{
    bool IsRunning { get; }

    Task ApplyConfigAsync(LocalApiConfig config);

    Task StopAsync();
}
