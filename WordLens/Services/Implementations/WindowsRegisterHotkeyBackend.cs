using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using SharpHook.Data;
using WordLens.Models;
using ZLogger;

namespace WordLens.Services.Implementations;

public sealed partial class WindowsRegisterHotkeyBackend : IHotkeyBackend
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

    private sealed partial class WindowsHotkeyMessageWindow : IDisposable
    {
        private const int ErrorClassAlreadyExists = 1410;
        private const int HwndMessage = -3;
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModShift = 0x0004;
        private const uint ModWin = 0x0008;
        private const uint ModNoRepeat = 0x4000;
        private const int WmHotkey = 0x0312;
        private const string WindowClassName = "WordLensHotkeyMessageWindow";

        private readonly IntPtr _hInstance;
        private readonly IntPtr _hwnd;
        private readonly ILogger<WindowsRegisterHotkeyBackend> _logger;
        private readonly HashSet<int> _registeredIds = new();
        private readonly WindowProc _wndProc;

        public WindowsHotkeyMessageWindow(ILogger<WindowsRegisterHotkeyBackend> logger)
        {
            _logger = logger;
            _wndProc = WndProc;
            _hInstance = GetModuleHandle(IntPtr.Zero);
            _hwnd = CreateMessageWindow();
        }

        public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

        public bool RegisterHotkey(int id, HotkeyConfig config, string name)
        {
            if (!TryMapHotkey(config, out var modifiers, out var virtualKey))
            {
                _logger.ZLogWarning($"{name}热键无效: Modifiers={config.Modifiers}, Key={config.Key}");
                return false;
            }

            if (!RegisterHotKey(_hwnd, id, modifiers, virtualKey))
            {
                var error = Marshal.GetLastWin32Error();
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
                UnregisterHotKey(_hwnd, id);

            _registeredIds.Clear();
        }

        public void Dispose()
        {
            UnregisterAll();

            if (_hwnd != IntPtr.Zero)
                DestroyWindow(_hwnd);
        }

        private IntPtr CreateMessageWindow()
        {
            RegisterWindowClass();

            var hwnd = CreateWindowEx(
                0,
                WindowClassName,
                string.Empty,
                0,
                0,
                0,
                0,
                0,
                new IntPtr(HwndMessage),
                IntPtr.Zero,
                _hInstance,
                IntPtr.Zero);

            if (hwnd == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "创建热键消息窗口失败");

            return hwnd;
        }

        private void RegisterWindowClass()
        {
            var className = Marshal.StringToHGlobalUni(WindowClassName);
            try
            {
                var wndClass = new WndClass
                {
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                    hInstance = _hInstance,
                    lpszClassName = className
                };

                var atom = RegisterClass(ref wndClass);
                if (atom != 0)
                    return;

                var error = Marshal.GetLastWin32Error();
                if (error != ErrorClassAlreadyExists)
                    throw new Win32Exception(error, "注册热键消息窗口类失败");
            }
            finally
            {
                Marshal.FreeHGlobal(className);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
        {
            if (message == WmHotkey)
            {
                HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(wParam.ToInt32()));
                return IntPtr.Zero;
            }

            return DefWindowProc(hwnd, message, wParam, lParam);
        }

        private static bool TryMapHotkey(HotkeyConfig config, out uint modifiers, out uint virtualKey)
        {
            modifiers = ModNoRepeat;
            virtualKey = MapVirtualKey(config.Key);

            if (virtualKey == 0)
                return false;

            if (config.Modifiers.HasFlag(EventMask.LeftCtrl) ||
                config.Modifiers.HasFlag(EventMask.RightCtrl))
                modifiers |= ModControl;

            if (config.Modifiers.HasFlag(EventMask.LeftShift) ||
                config.Modifiers.HasFlag(EventMask.RightShift))
                modifiers |= ModShift;

            if (config.Modifiers.HasFlag(EventMask.LeftAlt) ||
                config.Modifiers.HasFlag(EventMask.RightAlt))
                modifiers |= ModAlt;

            if (config.Modifiers.HasFlag(EventMask.LeftMeta) ||
                config.Modifiers.HasFlag(EventMask.RightMeta))
                modifiers |= ModWin;

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

        [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true)]
        private static partial IntPtr GetModuleHandle(IntPtr lpModuleName);

        [LibraryImport("user32.dll", EntryPoint = "RegisterClassW", SetLastError = true)]
        private static partial ushort RegisterClass(ref WndClass lpWndClass);

        [LibraryImport(
            "user32.dll",
            EntryPoint = "CreateWindowExW",
            SetLastError = true,
            StringMarshalling = StringMarshalling.Utf16)]
        private static partial IntPtr CreateWindowEx(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DestroyWindow(IntPtr hWnd);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool UnregisterHotKey(IntPtr hWnd, int id);

        [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
        private static partial IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WndClass
        {
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public IntPtr lpszMenuName;
            public IntPtr lpszClassName;
        }
    }
}
