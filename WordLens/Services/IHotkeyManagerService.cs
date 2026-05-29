using System;
using System.Threading.Tasks;
using WordLens.Models;

namespace WordLens.Services;

public interface IHotkeyManagerService : IDisposable, IAsyncDisposable
{
    Task StartAsync();
    Task StartAsync(AppSettings settings);
    Task ReloadConfigAsync();
    Task ReloadConfigAsync(AppSettings settings);
}
