using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpHook;
using WordLens.Models;
using ZLogger;

namespace WordLens.Services
{
    public interface IHotkeyService : IAsyncDisposable
    {
        event EventHandler? HotkeyTriggered;
        Task StartAsync(CancellationToken ct = default);
        void Stop();
    }

    public class HotkeyService : IHotkeyService
    {
        private readonly ISettingsService _settingsService;
        private readonly ILogger<HotkeyService> _logger;
        private HotkeyConfig _config = HotkeyConfig.Default();
        private IGlobalHook? _hook;

        public HotkeyService(ISettingsService settingsService, ILogger<HotkeyService> logger,IGlobalHook hook)
        {
            _settingsService = settingsService;
            _logger = logger;
            _hook = hook;
        }

        public event EventHandler? HotkeyTriggered;

        public async Task StartAsync(CancellationToken ct = default)
        {
            var settings = await _settingsService.LoadAsync();
            _config = settings.Hotkey;

            _logger.ZLogInformation($"翻译热键服务启动，快捷键配置: Modifiers={_config.Modifiers}, Key={_config.Key}");

            _hook.KeyPressed += OnKeyPressed;
            await _hook.RunAsync();
        }

        public async Task ReloadHotkeyAsync()
        {
            var settings = await _settingsService.LoadAsync();
            _config = settings.Hotkey;
            _logger.ZLogInformation($"翻译热键配置已重新加载: Modifiers={_config.Modifiers}, Key={_config.Key}");
        }

        public void Stop()
        {
            if (_hook is { IsRunning: true })
            {
                _logger.ZLogInformation($"翻译热键服务停止");
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
                _logger.ZLogInformation($"翻译热键服务已释放");
            }
            await Task.CompletedTask;
        }

        private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
        {
            // On Windows/macOS we can suppress if needed: e.SuppressEvent = true;
            if ((e.RawEvent.Mask & _config.Modifiers) == _config.Modifiers && e.Data.KeyCode == _config.Key)
            {
                _logger.ZLogInformation($"翻译热键被触发");
                HotkeyTriggered?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}