using Microsoft.Extensions.DependencyInjection;
using WordLens.Abstractions.Services;
using WordLens.Linux.Services.Implementations;
using WordLens.Services;

namespace WordLens.Linux;

public static class WordLensLinuxServiceCollectionExtensions
{
    public static IServiceCollection AddWordLensLinux(this IServiceCollection services)
    {
        services.AddSingleton<ICursorPositionProvider, LinuxCursorPositionProvider>();
        services.AddSingleton<IStartupService, LinuxStartupService>();
        return services;
    }
}
