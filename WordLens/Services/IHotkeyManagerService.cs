using System;
using System.Threading.Tasks;

namespace WordLens.Services;

public interface IHotkeyManagerService : IDisposable, IAsyncDisposable
{
    Task StartAsync();
    Task ReloadConfigAsync();
}