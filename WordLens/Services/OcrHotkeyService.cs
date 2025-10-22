using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpHook;
using WordLens.Models;
using ZLogger;

namespace WordLens.Services
{
    public interface IOcrHotkeyService : IAsyncDisposable
    {
        event EventHandler? OcrHotkeyTriggered;
        Task StartAsync(CancellationToken ct = default);
        void Stop();
    }

    public class OcrHotkeyService : IOcrHotkeyService
    {
        private readonly ISettingsService _settingsService;
        private readonly ILogger<OcrHotkeyService> _logger;
        private HotkeyConfig _config = HotkeyConfig.Default();
        private IGlobalHook? _hook;

        public OcrHotkeyService(ISettingsService settingsService, ILogger<OcrHotkeyService> logger,IGlobalHook hook)
        {
            _settingsService = settingsService;
            _logger = logger;
            _hook = hook;
        }

        public event EventHandler? OcrHotkeyTriggered;

        public async Task StartAsync(CancellationToken ct = default)
        {
            var settings = await _settingsService.LoadAsync();
            _config = settings.OcrHotkey;

            _logger.ZLogInformation($"OCR热键服务启动，快捷键配置: Modifiers={_config.Modifiers}, Key={_config.Key}");
            _hook.KeyPressed += OnKeyPressed;
            await _hook.RunAsync();
        }

        public async Task ReloadHotkeyAsync()
        {
            var settings = await _settingsService.LoadAsync();
            _config = settings.OcrHotkey;
            _logger.ZLogInformation($"OCR热键配置已重新加载: Modifiers={_config.Modifiers}, Key={_config.Key}");
        }

        public void Stop()
        {
            if (_hook is { IsRunning: true })
            {
                _logger.ZLogInformation($"OCR热键服务停止");
                _hook.Stop();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_hook != null)
            {
                _hook.KeyPressed -= OnKeyPressed;
                if (_hook.IsRunning)
                {
                    _hook.Stop();
                }
                _hook.Dispose();
                _logger.ZLogInformation($"OCR热键服务已释放");
            }
            await Task.CompletedTask;
        }

        private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
        {
            if ((e.RawEvent.Mask & _config.Modifiers) == _config.Modifiers && e.Data.KeyCode == _config.Key)
            {
                _logger.ZLogInformation($"OCR热键被触发（功能预留）");
                OcrHotkeyTriggered?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}