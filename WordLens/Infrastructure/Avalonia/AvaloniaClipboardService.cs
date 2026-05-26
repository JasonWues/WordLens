using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using WordLens.Services;

namespace WordLens.Infrastructure.Avalonia;

public class AvaloniaClipboardService : IClipboardService
{
    private readonly IClipboardMonitorService _clipboardMonitor;

    public AvaloniaClipboardService(IClipboardMonitorService clipboardMonitor)
    {
        _clipboardMonitor = clipboardMonitor;
    }

    public async Task SetTextAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var clipboard = GetClipboard();
        if (clipboard != null)
        {
            _clipboardMonitor.IgnoreNextTextChange(text);
            await clipboard.SetTextAsync(text);
        }
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
