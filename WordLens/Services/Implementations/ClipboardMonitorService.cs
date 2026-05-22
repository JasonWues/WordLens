using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace WordLens.Services.Implementations;

public sealed class ClipboardMonitorService : IClipboardMonitorService
{
    private const int MaxAutoTranslateTextLength = 10000;

    private readonly IClipboardMonitorBackend _backend;
    private readonly ILogger<ClipboardMonitorService> _logger;
    private readonly object _gate = new();
    private string? _ignoredText;
    private string? _lastText;

    public ClipboardMonitorService(
        IClipboardMonitorBackend backend,
        ILogger<ClipboardMonitorService> logger)
    {
        _backend = backend;
        _logger = logger;
    }

    public event EventHandler<ClipboardTextChangedEventArgs>? TextChanged;

    public bool IsRunning => _backend.IsRunning;

    public async Task StartAsync()
    {
        if (_backend.IsRunning)
            return;

        _backend.TextChanged += OnBackendTextChanged;
        try
        {
            await _backend.StartAsync();
        }
        catch
        {
            _backend.TextChanged -= OnBackendTextChanged;
            throw;
        }

        if (!_backend.IsRunning)
        {
            _backend.TextChanged -= OnBackendTextChanged;
            _logger.ZLogWarning($"剪贴板监听服务未启动");
            return;
        }

        _logger.ZLogInformation($"剪贴板监听服务已启动");
    }

    public async Task StopAsync()
    {
        if (!_backend.IsRunning)
            return;

        _backend.TextChanged -= OnBackendTextChanged;
        await _backend.StopAsync();
        _logger.ZLogInformation($"剪贴板监听服务已停止");
    }

    public void Dispose()
    {
        _backend.TextChanged -= OnBackendTextChanged;
        _backend.Dispose();
    }

    public void IgnoreNextTextChange(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        lock (_gate)
        {
            _ignoredText = text;
        }
    }

    private void OnBackendTextChanged(object? sender, ClipboardTextChangedEventArgs e)
    {
        var text = e.Text;
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (text.Length > MaxAutoTranslateTextLength)
        {
            _logger.ZLogWarning($"剪贴板文本过长，已忽略: Length={text.Length}");
            return;
        }

        lock (_gate)
        {
            if (string.Equals(_ignoredText, text, StringComparison.Ordinal))
            {
                _ignoredText = null;
                _lastText = text;
                _logger.ZLogDebug($"忽略应用自身写入的剪贴板文本: Length={text.Length}");
                return;
            }

            if (string.Equals(_lastText, text, StringComparison.Ordinal))
                return;

            _lastText = text;
        }

        _logger.ZLogInformation($"检测到新的剪贴板文本: Length={text.Length}");
        TextChanged?.Invoke(this, new ClipboardTextChangedEventArgs(text));
    }
}
