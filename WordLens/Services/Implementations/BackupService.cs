using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

    public Task<RestoreBackupResult> RestoreBackupAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => RestoreBackup(sourcePath, cancellationToken), cancellationToken);
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

    private RestoreBackupResult RestoreBackup(string sourcePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("备份文件路径不能为空。", nameof(sourcePath));

        var fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath))
            throw new FileNotFoundException("备份文件不存在。", fullSourcePath);

        var tempDirectory = Path.Combine(Path.GetTempPath(), $"WordLensRestore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var restoredEntries = ExtractBackup(fullSourcePath, tempDirectory, cancellationToken);
            if (restoredEntries.Count == 0)
                throw new InvalidDataException("备份中没有可恢复的数据。");

            var preRestoreBackupPath = CreatePreRestoreBackup(cancellationToken).DestinationPath;

            Directory.CreateDirectory(_appDataDirectory);
            RestoreFileIfExists(tempDirectory, "settings.json", cancellationToken);
            RestoreFileIfExists(tempDirectory, "translation_history.db", cancellationToken);
            RestoreFileOrDeleteIfMissing(tempDirectory, "translation_history.db-wal", cancellationToken);
            RestoreFileOrDeleteIfMissing(tempDirectory, "translation_history.db-shm", cancellationToken);
            RestoreScreenshots(tempDirectory, cancellationToken);

            _logger.ZLogInformation($"备份恢复成功: {fullSourcePath}, 文件数: {restoredEntries.Count}");
            return new RestoreBackupResult(fullSourcePath, restoredEntries.Count, preRestoreBackupPath);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private HashSet<string> ExtractBackup(
        string sourcePath,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(sourcePath);
        var manifestEntry = archive.GetEntry(ManifestEntryName) ??
                            throw new InvalidDataException("不是有效的 WordLens 备份：缺少备份清单。");

        using var manifestStream = manifestEntry.Open();
        var manifest = JsonSerializer.Deserialize(manifestStream, SourceGenerationContext.Default.BackupManifest) ??
                       throw new InvalidDataException("不是有效的 WordLens 备份：备份清单无法读取。");

        if (!string.Equals(manifest.AppName, AppName, StringComparison.Ordinal))
            throw new InvalidDataException("不是有效的 WordLens 备份：应用名称不匹配。");

        if (manifest.Version != 1)
            throw new InvalidDataException($"不支持的备份版本：{manifest.Version}。");

        var restorableEntries = manifest.Files
            .Select(static file => NormalizeEntryName(file.Path))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var entryName in restorableEntries)
        {
            if (!IsRestorableEntry(entryName))
                throw new InvalidDataException($"备份包含不允许恢复的路径：{entryName}");
        }

        var extractedEntries = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.Equals(entry.FullName, ManifestEntryName, StringComparison.Ordinal))
                continue;

            var entryName = NormalizeEntryName(entry.FullName);
            if (!restorableEntries.Contains(entryName))
                continue;

            ExtractEntry(entry, destinationDirectory, entryName, cancellationToken);
            extractedEntries.Add(entryName);
        }

        if (!extractedEntries.SetEquals(restorableEntries))
            throw new InvalidDataException("备份文件不完整：清单和文件内容不一致。");

        return extractedEntries;
    }

    private BackupResult CreatePreRestoreBackup(CancellationToken cancellationToken)
    {
        var backupDirectory = Path.Combine(_appDataDirectory, "Backups");
        var backupPath = Path.Combine(backupDirectory, $"WordLens-before-restore-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        return CreateBackup(backupPath, cancellationToken);
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

    private static void ExtractEntry(
        ZipArchiveEntry entry,
        string destinationDirectory,
        string entryName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var destinationPath = GetSafeDestinationPath(destinationDirectory, entryName);
        var destinationParent = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationParent))
            Directory.CreateDirectory(destinationParent);

        using var source = entry.Open();
        using var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        source.CopyTo(destination);
    }

    private void RestoreFileIfExists(
        string sourceDirectory,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var sourcePath = Path.Combine(sourceDirectory, relativePath);
        if (!File.Exists(sourcePath))
            return;

        RestoreFile(sourcePath, Path.Combine(_appDataDirectory, relativePath), cancellationToken);
    }

    private void RestoreFileOrDeleteIfMissing(
        string sourceDirectory,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var sourcePath = Path.Combine(sourceDirectory, relativePath);
        var targetPath = Path.Combine(_appDataDirectory, relativePath);

        if (File.Exists(sourcePath))
        {
            RestoreFile(sourcePath, targetPath, cancellationToken);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        TryDeleteFile(targetPath);
    }

    private void RestoreScreenshots(string sourceDirectory, CancellationToken cancellationToken)
    {
        var sourcePath = Path.Combine(sourceDirectory, "Screenshots");
        var targetPath = Path.Combine(_appDataDirectory, "Screenshots");
        var oldPath = Path.Combine(_appDataDirectory, $".Screenshots.restore-{Guid.NewGuid():N}.bak");

        cancellationToken.ThrowIfCancellationRequested();

        if (Directory.Exists(targetPath))
            Directory.Move(targetPath, oldPath);

        try
        {
            if (Directory.Exists(sourcePath))
                CopyDirectory(sourcePath, targetPath, cancellationToken);
            else
                Directory.CreateDirectory(targetPath);

            TryDeleteDirectory(oldPath);
        }
        catch
        {
            TryDeleteDirectory(targetPath);
            if (Directory.Exists(oldPath))
                Directory.Move(oldPath, targetPath);
            throw;
        }
    }

    private static void RestoreFile(string sourcePath, string targetPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var targetDirectory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
            Directory.CreateDirectory(targetDirectory);

        File.Copy(sourcePath, targetPath, overwrite: true);
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

    private static string NormalizeEntryName(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private static bool IsRestorableEntry(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName) ||
            entryName.EndsWith('/') ||
            entryName.Contains(':') ||
            entryName.Split('/').Any(static part => part is "." or ".."))
            return false;

        return entryName is
                   "settings.json" or
                   "translation_history.db" or
                   "translation_history.db-wal" or
                   "translation_history.db-shm" ||
               entryName.StartsWith("Screenshots/", StringComparison.Ordinal);
    }

    private static string GetSafeDestinationPath(string destinationDirectory, string entryName)
    {
        var destinationPath = Path.GetFullPath(Path.Combine(
            destinationDirectory,
            entryName.Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.GetFullPath(destinationDirectory);
        if (!destinationPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"备份包含不安全的路径：{entryName}");

        return destinationPath;
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var sourcePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(sourceDirectory, sourcePath);
            var targetPath = Path.Combine(targetDirectory, relativePath);
            var targetParent = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetParent))
                Directory.CreateDirectory(targetParent);

            File.Copy(sourcePath, targetPath, overwrite: true);
        }
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

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Cleanup failure should not hide the original backup or restore error.
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
