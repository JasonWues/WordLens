using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using WordLens.Services;

namespace WordLens.Infrastructure.Avalonia;

public sealed class AvaloniaPathPickerService : IPathPickerService
{
    public async Task<string?> PickFileAsync(string title, IReadOnlyList<string> patterns)
    {
        var paths = await PickFilesCoreAsync(title, patterns, allowMultiple: false);
        return paths.FirstOrDefault();
    }

    public Task<IReadOnlyList<string>> PickFilesAsync(string title, IReadOnlyList<string> patterns)
    {
        return PickFilesCoreAsync(title, patterns, allowMultiple: true);
    }

    private static async Task<IReadOnlyList<string>> PickFilesCoreAsync(
        string title,
        IReadOnlyList<string> patterns,
        bool allowMultiple)
    {
        var storageProvider = GetStorageProvider();
        if (storageProvider == null)
            return Array.Empty<string>();

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = allowMultiple,
            FileTypeFilter = CreateFileTypeFilter(patterns)
        });

        return files
            .Select(static file => file.TryGetLocalPath())
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => path!)
            .ToList();
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var storageProvider = GetStorageProvider();
        if (storageProvider == null)
            return null;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickSaveFileAsync(
        string title,
        string suggestedFileName,
        IReadOnlyList<string> patterns)
    {
        var storageProvider = GetStorageProvider();
        if (storageProvider == null)
            return null;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = CreateFileTypeFilter(patterns),
            DefaultExtension = GetDefaultExtension(patterns),
            ShowOverwritePrompt = true
        });

        return file?.TryGetLocalPath();
    }

    private static IStorageProvider? GetStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        var window = desktop.Windows.FirstOrDefault(static w => w.IsActive) ??
                     desktop.Windows.FirstOrDefault(static w => w.IsVisible);

        return window?.StorageProvider;
    }

    private static IReadOnlyList<FilePickerFileType> CreateFileTypeFilter(IReadOnlyList<string> patterns)
    {
        return new[]
        {
            new FilePickerFileType("支持的文件")
            {
                Patterns = patterns
            },
            FilePickerFileTypes.All
        };
    }

    private static string? GetDefaultExtension(IReadOnlyList<string> patterns)
    {
        var pattern = patterns.FirstOrDefault(static p => p.StartsWith("*."));
        return pattern?.Length > 2 ? pattern[2..] : null;
    }
}
