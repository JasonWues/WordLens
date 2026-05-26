using Microsoft.Extensions.DependencyInjection;
using WordLens.Abstractions.Services;
using WordLens.Windows.Services.Implementations;

namespace WordLens.Windows;

public static class WordLensWindowsServiceCollectionExtensions
{
    public static IServiceCollection AddWordLensWindows(this IServiceCollection services)
    {
        services.AddSingleton<IHotkeyBackend, WindowsRegisterHotkeyBackend>();
        services.AddSingleton<IClipboardMonitorBackend, WindowsClipboardMonitorBackend>();
        services.AddSingleton<ICursorPositionProvider, WindowsCursorPositionProvider>();
        services.AddSingleton<ILocalTtsBackend, WindowsWinRTLocalTtsBackend>();
        services.AddSingleton<ILocalOcrBackend, WindowsOcrBackend>();
        services.AddSingleton<IStartupService, WindowsStartupService>();
        return services;
    }
}
