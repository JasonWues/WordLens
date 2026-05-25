using System;
using System.Threading.Tasks;
using WordLens.Abstractions.Services;

namespace WordLens.Services;

public interface IClipboardMonitorService : IDisposable
{
    event EventHandler<ClipboardTextChangedEventArgs>? TextChanged;

    bool IsRunning { get; }

    Task StartAsync();

    Task StopAsync();

    void IgnoreNextTextChange(string text);
}
