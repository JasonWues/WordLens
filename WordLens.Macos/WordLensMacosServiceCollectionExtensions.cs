using Microsoft.Extensions.DependencyInjection;
using WordLens.Macos.Services.Implementations;
using WordLens.Services;

namespace WordLens.Macos;

public static class WordLensMacosServiceCollectionExtensions
{
    public static IServiceCollection AddWordLensMacos(this IServiceCollection services)
    {
        services.AddSingleton<ICursorPositionProvider, MacosCursorPositionProvider>();
        services.AddSingleton<IStartupService, MacosStartupService>();
        return services;
    }
}
