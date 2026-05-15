using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace WordLens.Services.Implementations;

public class AvaloniaClipboardService : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var clipboard = GetClipboard();
        if (clipboard != null)
            await clipboard.SetTextAsync(text);
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
