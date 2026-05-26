using Microsoft.Extensions.DependencyInjection;
using WordLens.Abstractions.Services;
using WordLens.Macos.Services.Implementations;

namespace WordLens.Macos;

public static class WordLensMacosServiceCollectionExtensions
{
    public static IServiceCollection AddWordLensMacos(this IServiceCollection services)
    {
        services.AddSingleton<ICursorPositionProvider, MacosCursorPositionProvider>();
        services.AddSingleton<ILocalTtsBackend, MacosSayLocalTtsBackend>();
        services.AddSingleton<IStartupService, MacosStartupService>();
        return services;
    }
}
