using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using SharpHook.Data;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using WordLens.Models;
using ZLogger;

namespace WordLens.Services.Implementations;

[SupportedOSPlatform("windows6.0.6000")]
public sealed class WindowsRegisterHotkeyBackend : IHotkeyBackend
{
    private readonly ILogger<WindowsRegisterHotkeyBackend> _logger;
    private WindowsHotkeyMessageWindow? _messageWindow;

    public WindowsRegisterHotkeyBackend(ILogger<WindowsRegisterHotkeyBackend> logger)
    {
        _logger = logger;
    }

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public Task RegisterAsync(IReadOnlyCollection<HotkeyRegistration> registrations)
    {
        if (!OperatingSystem.IsWindows())
            return Task.CompletedTask;

        return Dispatcher.UIThread.InvokeAsync(() => RegisterCore(registrations)).GetTask();
    }

    public void UnregisterAll()
    {
        _messageWindow?.UnregisterAll();
    }

    public void Dispose()
    {
        if (_messageWindow == null)
            return;

        _messageWindow.HotkeyPressed -= OnMessageWindowHotkeyPressed;
        _messageWindow.Dispose();
        _messageWindow = null;
        _logger.ZLogInformation($"Windows系统热键后端已释放");
    }

    private void RegisterCore(IReadOnlyCollection<HotkeyRegistration> registrations)
    {
        _messageWindow ??= CreateMessageWindow();
        _messageWindow.UnregisterAll();

        foreach (var registration in registrations)
        {
            if (!_messageWindow.RegisterHotkey(registration.Id, registration.Config, registration.Name))
                _logger.ZLogWarning($"{registration.Name}热键注册失败");
        }
    }

    private WindowsHotkeyMessageWindow CreateMessageWindow()
    {
        var window = new WindowsHotkeyMessageWindow(_logger);
        window.HotkeyPressed += OnMessageWindowHotkeyPressed;
        return window;
    }

    private void OnMessageWindowHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        HotkeyPressed?.Invoke(this, e);
    }

    [SupportedOSPlatform("windows6.0.6000")]
    private sealed class WindowsHotkeyMessageWindow : IDisposable
    {
        private const int ErrorClassAlreadyExists = 1410;
        private const int HwndMessage = -3;
        private const string WindowClassName = "WordLensHotkeyMessageWindow";

        private static readonly ConcurrentDictionary<IntPtr, WindowsHotkeyMessageWindow> Windows = new();

        private readonly HINSTANCE _hInstance;
        private readonly HWND _hwnd;
        private readonly ILogger<WindowsRegisterHotkeyBackend> _logger;
        private readonly HashSet<int> _registeredIds = new();
        private bool _disposed;

        public WindowsHotkeyMessageWindow(ILogger<WindowsRegisterHotkeyBackend> logger)
        {
            _logger = logger;
            unsafe
            {
                _hInstance = PInvoke.GetModuleHandle((string?)null);
                _hwnd = CreateMessageWindow();
            }

            Windows[(IntPtr)_hwnd] = this;
        }

        public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

        public bool RegisterHotkey(int id, HotkeyConfig config, string name)
        {
            if (!TryMapHotkey(config, out var modifiers, out var virtualKey))
            {
                _logger.ZLogWarning($"{name}热键无效: Modifiers={config.Modifiers}, Key={config.Key}");
                return false;
            }

            if (!PInvoke.RegisterHotKey(_hwnd, id, modifiers, virtualKey))
            {
                var error = Marshal.GetLastPInvokeError();
                _logger.ZLogWarning($"{name}热键注册失败: Modifiers={config.Modifiers}, Key={config.Key}, Win32Error={error}");
                return false;
            }

            _registeredIds.Add(id);
            _logger.ZLogInformation($"{name}热键已注册到Windows: Modifiers={config.Modifiers}, Key={config.Key}");
            return true;
        }

        public void UnregisterAll()
        {
            foreach (var id in _registeredIds)
                PInvoke.UnregisterHotKey(_hwnd, id);

            _registeredIds.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            UnregisterAll();
            Windows.TryRemove((IntPtr)_hwnd, out _);
            PInvoke.DestroyWindow(_hwnd);
        }

        private unsafe HWND CreateMessageWindow()
        {
            RegisterWindowClass();

            var hwnd = PInvoke.CreateWindowEx(
                (WINDOW_EX_STYLE)0,
                WindowClassName,
                string.Empty,
                (WINDOW_STYLE)0,
                0,
                0,
                0,
                0,
                (HWND)new IntPtr(HwndMessage),
                default,
                _hInstance,
                null);

            if (hwnd.IsNull)
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "创建热键消息窗口失败");

            return hwnd;
        }

        private unsafe void RegisterWindowClass()
        {
            fixed (char* className = WindowClassName)
            {
                var wndClass = new WNDCLASSW
                {
                    lpfnWndProc = &WndProc,
                    hInstance = _hInstance,
                    lpszClassName = className
                };

                var atom = PInvoke.RegisterClass(in wndClass);
                if (atom != 0)
                    return;

                var error = Marshal.GetLastPInvokeError();
                if (error != ErrorClassAlreadyExists)
                    throw new Win32Exception(error, "注册热键消息窗口类失败");
            }
        }

        private static bool TryMapHotkey(
            HotkeyConfig config,
            out HOT_KEY_MODIFIERS modifiers,
            out uint virtualKey)
        {
            modifiers = HOT_KEY_MODIFIERS.MOD_NOREPEAT;
            virtualKey = MapVirtualKey(config.Key);

            if (virtualKey == 0)
                return false;

            if (config.Modifiers.HasFlag(EventMask.LeftCtrl) ||
                config.Modifiers.HasFlag(EventMask.RightCtrl))
                modifiers |= HOT_KEY_MODIFIERS.MOD_CONTROL;

            if (config.Modifiers.HasFlag(EventMask.LeftShift) ||
                config.Modifiers.HasFlag(EventMask.RightShift))
                modifiers |= HOT_KEY_MODIFIERS.MOD_SHIFT;

            if (config.Modifiers.HasFlag(EventMask.LeftAlt) ||
                config.Modifiers.HasFlag(EventMask.RightAlt))
                modifiers |= HOT_KEY_MODIFIERS.MOD_ALT;

            if (config.Modifiers.HasFlag(EventMask.LeftMeta) ||
                config.Modifiers.HasFlag(EventMask.RightMeta))
                modifiers |= HOT_KEY_MODIFIERS.MOD_WIN;

            return true;
        }

        private static uint MapVirtualKey(KeyCode key)
        {
            return key switch
            {
                KeyCode.VcA => 0x41,
                KeyCode.VcB => 0x42,
                KeyCode.VcC => 0x43,
                KeyCode.VcD => 0x44,
                KeyCode.VcE => 0x45,
                KeyCode.VcF => 0x46,
                KeyCode.VcG => 0x47,
                KeyCode.VcH => 0x48,
                KeyCode.VcI => 0x49,
                KeyCode.VcJ => 0x4A,
                KeyCode.VcK => 0x4B,
                KeyCode.VcL => 0x4C,
                KeyCode.VcM => 0x4D,
                KeyCode.VcN => 0x4E,
                KeyCode.VcO => 0x4F,
                KeyCode.VcP => 0x50,
                KeyCode.VcQ => 0x51,
                KeyCode.VcR => 0x52,
                KeyCode.VcS => 0x53,
                KeyCode.VcT => 0x54,
                KeyCode.VcU => 0x55,
                KeyCode.VcV => 0x56,
                KeyCode.VcW => 0x57,
                KeyCode.VcX => 0x58,
                KeyCode.VcY => 0x59,
                KeyCode.VcZ => 0x5A,
                KeyCode.Vc0 => 0x30,
                KeyCode.Vc1 => 0x31,
                KeyCode.Vc2 => 0x32,
                KeyCode.Vc3 => 0x33,
                KeyCode.Vc4 => 0x34,
                KeyCode.Vc5 => 0x35,
                KeyCode.Vc6 => 0x36,
                KeyCode.Vc7 => 0x37,
                KeyCode.Vc8 => 0x38,
                KeyCode.Vc9 => 0x39,
                KeyCode.VcF1 => 0x70,
                KeyCode.VcF2 => 0x71,
                KeyCode.VcF3 => 0x72,
                KeyCode.VcF4 => 0x73,
                KeyCode.VcF5 => 0x74,
                KeyCode.VcF6 => 0x75,
                KeyCode.VcF7 => 0x76,
                KeyCode.VcF8 => 0x77,
                KeyCode.VcF9 => 0x78,
                KeyCode.VcF10 => 0x79,
                KeyCode.VcF11 => 0x7A,
                KeyCode.VcF12 => 0x7B,
                KeyCode.VcSpace => 0x20,
                KeyCode.VcEnter => 0x0D,
                _ => 0
            };
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static LRESULT WndProc(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam)
        {
            try
            {
                if (message == PInvoke.WM_HOTKEY &&
                    Windows.TryGetValue((IntPtr)hwnd, out var window))
                {
                    window.HotkeyPressed?.Invoke(window, new HotkeyPressedEventArgs(checked((int)(nuint)wParam)));
                    return (LRESULT)0;
                }
            }
            catch
            {
                return (LRESULT)0;
            }

            return PInvoke.DefWindowProc(hwnd, message, wParam, lParam);
        }
    }
}
