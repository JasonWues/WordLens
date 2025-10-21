using System;
using System.Threading;
using System.Threading.Tasks;
using SharpHook;
using WordLens.Models;

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
        readonly private ISettingsService _settingsService;
        private HotkeyConfig _config = HotkeyConfig.Default();
        private EventLoopGlobalHook? _hook;

        public HotkeyService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public event EventHandler? HotkeyTriggered;

        public async Task StartAsync(CancellationToken ct = default)
        {


            var settings = await _settingsService.LoadAsync();
            _config = settings.Hotkey;

            _hook = new EventLoopGlobalHook();
            _hook.KeyPressed += OnKeyPressed;
            await _hook.RunAsync();
        }

        public void Stop()
        {
            if (_hook is { IsRunning: true })
            {
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
            }
            await Task.CompletedTask;
        }

        private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
        {
            // On Windows/macOS we can suppress if needed: e.SuppressEvent = true;
            if ((e.RawEvent.Mask & _config.Modifiers) == _config.Modifiers && e.Data.KeyCode == _config.Key)
            {
                HotkeyTriggered?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}