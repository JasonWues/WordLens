using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpHook;
using WordLens.Abstractions.Models;
using WordLens.Models;
using ZLogger;

namespace WordLens.Services.Implementations;

public sealed class SharpHookHotkeyBackend : IHotkeyBackend
{
    private readonly IGlobalHook _globalHook;
    private readonly ILogger<SharpHookHotkeyBackend> _logger;
    private IReadOnlyCollection<HotkeyRegistration> _registrations = Array.Empty<HotkeyRegistration>();
    private bool _started;

    public SharpHookHotkeyBackend(
        IGlobalHook globalHook,
        ILogger<SharpHookHotkeyBackend> logger)
    {
        _globalHook = globalHook;
        _logger = logger;
    }

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public async Task RegisterAsync(IReadOnlyCollection<HotkeyRegistration> registrations)
    {
        _registrations = registrations.ToArray();

        if (_started)
            return;

        _started = true;
        _globalHook.KeyPressed += OnGlobalKeyPressed;

        await _globalHook.RunAsync();
        _logger.ZLogInformation($"SharpHook热键后端启动完成");
    }

    public void UnregisterAll()
    {
        _registrations = Array.Empty<HotkeyRegistration>();
    }

    public void Dispose()
    {
        if (!_started)
            return;

        _globalHook.KeyPressed -= OnGlobalKeyPressed;
        if (_globalHook.IsRunning)
            _globalHook.Stop();

        _globalHook.Dispose();
        _started = false;
        _logger.ZLogInformation($"SharpHook热键后端已释放");
    }

    private void OnGlobalKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        foreach (var registration in _registrations)
        {
            if (!IsHotkeyMatch(e, registration.Config))
                continue;

            e.SuppressEvent = true;
            HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(registration.Id));
            return;
        }
    }

    private static bool IsHotkeyMatch(KeyboardHookEventArgs e, HotkeyConfig config)
    {
        return (e.RawEvent.Mask & config.Modifiers) == config.Modifiers &&
               e.Data.KeyCode == config.Key;
    }
}
