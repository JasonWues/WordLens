using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Ole;
using Windows.Win32.UI.WindowsAndMessaging;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using WordLens.Abstractions.Services;
using WordLens.Services;
using ZLogger;

namespace WordLens.Windows.Services.Implementations;

[SupportedOSPlatform("windows6.0.6000")]
public sealed class WindowsClipboardMonitorBackend : IClipboardMonitorBackend
{
    private readonly ILogger<WindowsClipboardMonitorBackend> _logger;
    private WindowsClipboardMessageWindow? _messageWindow;

    public WindowsClipboardMonitorBackend(ILogger<WindowsClipboardMonitorBackend> logger)
    {
        _logger = logger;
    }

    public event EventHandler<ClipboardTextChangedEventArgs>? TextChanged;

    public bool IsRunning => _messageWindow != null;

    public Task StartAsync()
    {
        if (!OperatingSystem.IsWindows())
            return Task.CompletedTask;

        return Dispatcher.UIThread.InvokeAsync(StartCore).GetTask();
    }

    public Task StopAsync()
    {
        if (!OperatingSystem.IsWindows())
            return Task.CompletedTask;

        return Dispatcher.UIThread.InvokeAsync(StopCore).GetTask();
    }

    public void Dispose()
    {
        if (Dispatcher.UIThread.CheckAccess())
            StopCore();
        else
            Dispatcher.UIThread.Invoke(StopCore);
    }

    private void StartCore()
    {
        if (_messageWindow != null)
            return;

        _messageWindow = new WindowsClipboardMessageWindow(_logger);
        _messageWindow.TextChanged += OnTextChanged;
        _logger.ZLogInformation($"Windows剪贴板监听后端已启动");
    }

    private void StopCore()
    {
        if (_messageWindow == null)
            return;

        _messageWindow.TextChanged -= OnTextChanged;
        _messageWindow.Dispose();
        _messageWindow = null;
        _logger.ZLogInformation($"Windows剪贴板监听后端已停止");
    }

    private void OnTextChanged(object? sender, ClipboardTextChangedEventArgs e)
    {
        TextChanged?.Invoke(this, e);
    }

    [SupportedOSPlatform("windows6.0.6000")]
    private sealed class WindowsClipboardMessageWindow : IDisposable
    {
        private const int ErrorClassAlreadyExists = 1410;
        private const int HwndMessage = -3;
        private const int MaxClipboardReadCharacters = 100000;
        private const string WindowClassName = "WordLensClipboardMessageWindow";

        private static readonly ConcurrentDictionary<IntPtr, WindowsClipboardMessageWindow> Windows = new();

        private readonly HWND _hwnd;
        private readonly HINSTANCE _hInstance;
        private readonly ILogger<WindowsClipboardMonitorBackend> _logger;
        private volatile bool _disposed;
        private int _readVersion;

        public WindowsClipboardMessageWindow(ILogger<WindowsClipboardMonitorBackend> logger)
        {
            _logger = logger;
            unsafe
            {
                _hInstance = PInvoke.GetModuleHandle((string?)null);
                _hwnd = CreateMessageWindow();
            }

            Windows[(IntPtr)_hwnd] = this;

            if (!PInvoke.AddClipboardFormatListener(_hwnd))
            {
                var error = Marshal.GetLastPInvokeError();
                Windows.TryRemove((IntPtr)_hwnd, out _);
                PInvoke.DestroyWindow(_hwnd);
                throw new Win32Exception(error, "注册剪贴板监听窗口失败");
            }
        }

        public event EventHandler<ClipboardTextChangedEventArgs>? TextChanged;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            PInvoke.RemoveClipboardFormatListener(_hwnd);
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
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "创建剪贴板监听消息窗口失败");

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
                    throw new Win32Exception(error, "注册剪贴板监听消息窗口类失败");
            }
        }

        private void QueueClipboardRead()
        {
            var version = Interlocked.Increment(ref _readVersion);
            _ = ReadClipboardTextWithRetryAsync(version);
        }

        private async Task ReadClipboardTextWithRetryAsync(int version)
        {
            await Task.Delay(80).ConfigureAwait(false);

            if (_disposed || version != Volatile.Read(ref _readVersion))
                return;

            for (var attempt = 0; attempt < 3 && !_disposed; attempt++)
            {
                var text = TryReadClipboardText();
                if (text != null)
                {
                    TextChanged?.Invoke(this, new ClipboardTextChangedEventArgs(text));
                    return;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        private string? TryReadClipboardText()
        {
            var format = (uint)CLIPBOARD_FORMAT.CF_UNICODETEXT;
            if (!PInvoke.IsClipboardFormatAvailable(format))
                return null;

            if (!PInvoke.OpenClipboard(_hwnd))
            {
                _logger.ZLogDebug($"打开剪贴板失败: Win32Error={Marshal.GetLastPInvokeError()}");
                return null;
            }

            try
            {
                var handle = PInvoke.GetClipboardData(format);
                if (handle.IsNull)
                    return null;

                unsafe
                {
                    var global = (HGLOBAL)(IntPtr)handle;
                    var data = (char*)PInvoke.GlobalLock(global);
                    if (data == null)
                        return null;

                    try
                    {
                        var byteCount = PInvoke.GlobalSize(global);
                        var maxCharacters = byteCount == 0
                            ? MaxClipboardReadCharacters
                            : (int)Math.Min(byteCount / 2, (nuint)MaxClipboardReadCharacters);

                        var length = 0;
                        while (length < maxCharacters && data[length] != '\0')
                            length++;

                        return length == 0 ? null : new string(data, 0, length);
                    }
                    finally
                    {
                        PInvoke.GlobalUnlock(global);
                    }
                }
            }
            finally
            {
                PInvoke.CloseClipboard();
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static LRESULT WndProc(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam)
        {
            try
            {
                if (message == PInvoke.WM_CLIPBOARDUPDATE &&
                    Windows.TryGetValue((IntPtr)hwnd, out var window))
                {
                    window.QueueClipboardRead();
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
