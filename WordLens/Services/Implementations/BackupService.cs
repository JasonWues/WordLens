using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace WordLens.Services.Implementations;

public sealed class BackupService : IBackupService
{
    private const string AppName = "WordLens";
    private const string ManifestEntryName = "backup-manifest.json";
    private readonly string _appDataDirectory;
    private readonly ILogger<BackupService> _logger;

    public BackupService(ILogger<BackupService> logger)
        : this(logger, GetDefaultAppDataDirectory())
    {
    }

    public BackupService(ILogger<BackupService> logger, string appDataDirectory)
    {
        _logger = logger;
        _appDataDirectory = appDataDirectory;
    }

    public Task<BackupResult> CreateBackupAsync(
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => CreateBackup(destinationPath, cancellationToken), cancellationToken);
    }

    private BackupResult CreateBackup(string destinationPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("备份文件路径不能为空。", nameof(destinationPath));

        var fullDestinationPath = NormalizeDestinationPath(destinationPath);
        var destinationDirectory = Path.GetDirectoryName(fullDestinationPath);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
            throw new InvalidOperationException("备份文件目录无效。");

        Directory.CreateDirectory(destinationDirectory);

        var tempPath = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(fullDestinationPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            var manifest = new BackupManifest
            {
                CreatedAt = DateTimeOffset.UtcNow
            };

            using (var archive = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                AddFileIfExists(archive, "settings.json", manifest, cancellationToken);
                AddFileIfExists(archive, "translation_history.db", manifest, cancellationToken);
                AddFileIfExists(archive, "translation_history.db-wal", manifest, cancellationToken);
                AddFileIfExists(archive, "translation_history.db-shm", manifest, cancellationToken);
                AddDirectoryIfExists(archive, "Screenshots", manifest, cancellationToken);
                AddManifest(archive, manifest, cancellationToken);
            }

            File.Move(tempPath, fullDestinationPath, overwrite: true);
            var sizeBytes = new FileInfo(fullDestinationPath).Length;
            _logger.ZLogInformation($"备份创建成功: {fullDestinationPath}, 文件数: {manifest.Files.Count}");
            return new BackupResult(fullDestinationPath, manifest.Files.Count, sizeBytes);
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
    }

    private void AddFileIfExists(
        ZipArchive archive,
        string relativePath,
        BackupManifest manifest,
        CancellationToken cancellationToken)
    {
        var sourcePath = Path.Combine(_appDataDirectory, relativePath);
        if (!File.Exists(sourcePath))
            return;

        AddFile(archive, sourcePath, ToEntryName(relativePath), manifest, cancellationToken);
    }

    private void AddDirectoryIfExists(
        ZipArchive archive,
        string relativeDirectory,
        BackupManifest manifest,
        CancellationToken cancellationToken)
    {
        var sourceDirectory = Path.Combine(_appDataDirectory, relativeDirectory);
        if (!Directory.Exists(sourceDirectory))
            return;

        foreach (var sourcePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var childRelativePath = Path.GetRelativePath(sourceDirectory, sourcePath);
            var entryName = ToEntryName(Path.Combine(relativeDirectory, childRelativePath));
            AddFile(archive, sourcePath, entryName, manifest, cancellationToken);
        }
    }

    private static void AddFile(
        ZipArchive archive,
        string sourcePath,
        string entryName,
        BackupManifest manifest,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var destination = entry.Open();
        source.CopyTo(destination);

        manifest.Files.Add(new BackupManifestFile
        {
            Path = entryName,
            SizeBytes = source.Length
        });
    }

    private static void AddManifest(
        ZipArchive archive,
        BackupManifest manifest,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, manifest, SourceGenerationContext.Default.BackupManifest);
    }

    private static string NormalizeDestinationPath(string destinationPath)
    {
        var fullPath = Path.GetFullPath(destinationPath);
        return string.IsNullOrWhiteSpace(Path.GetExtension(fullPath))
            ? Path.ChangeExtension(fullPath, ".zip")
            : fullPath;
    }

    private static string ToEntryName(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string GetDefaultAppDataDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, AppName);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Cleanup failure should not hide the original backup error.
        }
    }
}

public sealed class BackupManifest
{
    public int Version { get; set; } = 1;

    public string AppName { get; set; } = "WordLens";

    public DateTimeOffset CreatedAt { get; set; }

    public List<BackupManifestFile> Files { get; set; } = new();
}

public sealed class BackupManifestFile
{
    public string Path { get; set; } = string.Empty;

    public long SizeBytes { get; set; }
}
