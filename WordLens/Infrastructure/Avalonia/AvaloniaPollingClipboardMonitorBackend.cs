using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using WordLens.Abstractions.Services;
using ZLogger;

namespace WordLens.Infrastructure.Avalonia;

public sealed class AvaloniaPollingClipboardMonitorBackend : IClipboardMonitorBackend
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(750);

    private readonly ILogger<AvaloniaPollingClipboardMonitorBackend> _logger;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _pollingTask;

    public AvaloniaPollingClipboardMonitorBackend(ILogger<AvaloniaPollingClipboardMonitorBackend> logger)
    {
        _logger = logger;
    }

    public event EventHandler<ClipboardTextChangedEventArgs>? TextChanged;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _cts is { IsCancellationRequested: false };
            }
        }
    }

    public Task StartAsync()
    {
        lock (_gate)
        {
            if (_cts != null)
                return Task.CompletedTask;

            _cts = new CancellationTokenSource();
            _pollingTask = PollClipboardAsync(_cts.Token);
        }

        _logger.ZLogInformation($"Avalonia轮询剪贴板监听后端已启动");
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        Task? pollingTask;

        lock (_gate)
        {
            if (_cts == null)
                return;

            cts = _cts;
            pollingTask = _pollingTask;
            _cts = null;
            _pollingTask = null;
        }

        await StopCoreAsync(cts, pollingTask).ConfigureAwait(false);
        _logger.ZLogInformation($"Avalonia轮询剪贴板监听后端已停止");
    }

    public void Dispose()
    {
        CancellationTokenSource? cts;

        lock (_gate)
        {
            cts = _cts;
            _cts = null;
            _pollingTask = null;
        }

        if (cts == null)
            return;

        cts.Cancel();
        cts.Dispose();
    }

    private static async Task StopCoreAsync(CancellationTokenSource cts, Task? pollingTask)
    {
        try
        {
            cts.Cancel();

            if (pollingTask != null)
                await pollingTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cts.Dispose();
        }
    }

    private async Task PollClipboardAsync(CancellationToken cancellationToken)
    {
        string? lastText = null;

        try
        {
            lastText = await TryReadClipboardTextAsync(cancellationToken).ConfigureAwait(false);

            using var timer = new PeriodicTimer(PollInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                var text = await TryReadClipboardTextAsync(cancellationToken).ConfigureAwait(false);
                if (string.Equals(lastText, text, StringComparison.Ordinal))
                    continue;

                lastText = text;

                if (string.IsNullOrWhiteSpace(text))
                    continue;

                TextChanged?.Invoke(this, new ClipboardTextChangedEventArgs(text));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"轮询剪贴板失败: {ex.Message}");
        }
    }

    private static async Task<string?> TryReadClipboardTextAsync(CancellationToken cancellationToken)
    {
        var clipboard = await GetClipboardAsync(cancellationToken).ConfigureAwait(false);
        if (clipboard == null)
            return null;

        cancellationToken.ThrowIfCancellationRequested();
        return await clipboard.TryGetTextAsync().ConfigureAwait(false);
    }

    private static Task<IClipboard?> GetClipboardAsync(CancellationToken cancellationToken)
    {
        if (Dispatcher.UIThread.CheckAccess())
            return Task.FromResult(GetClipboard());

        return Dispatcher.UIThread.InvokeAsync(GetClipboard, default, cancellationToken).GetTask();
    }

    private static IClipboard? GetClipboard()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        var window = desktop.Windows.FirstOrDefault(w => w.IsActive) ??
                     desktop.Windows.FirstOrDefault(w => w.IsVisible) ??
                     desktop.Windows.FirstOrDefault();

        return window?.Clipboard;
    }
}
