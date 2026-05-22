using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace WordLens.Services.Implementations;

public sealed class UnsupportedClipboardMonitorBackend : IClipboardMonitorBackend
{
    private readonly ILogger<UnsupportedClipboardMonitorBackend> _logger;

    public UnsupportedClipboardMonitorBackend(ILogger<UnsupportedClipboardMonitorBackend> logger)
    {
        _logger = logger;
    }

    public event EventHandler<ClipboardTextChangedEventArgs>? TextChanged
    {
        add { }
        remove { }
    }

    public bool IsRunning => false;

    public Task StartAsync()
    {
        _logger.ZLogWarning($"当前平台暂未实现剪贴板监听");
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}
