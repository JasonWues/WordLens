using System;
using System.Threading.Tasks;

namespace WordLens.Services;

public sealed class ClipboardTextChangedEventArgs(string text) : EventArgs
{
    public string Text { get; } = text;
}

public interface IClipboardMonitorBackend : IDisposable
{
    event EventHandler<ClipboardTextChangedEventArgs>? TextChanged;

    bool IsRunning { get; }

    Task StartAsync();

    Task StopAsync();
}
