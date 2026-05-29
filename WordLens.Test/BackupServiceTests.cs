using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using WordLens;
using WordLens.Services.Implementations;

namespace WordLens.Test;

public class BackupServiceTests
{
    [Fact]
    public async Task CreateBackupAsync_WritesExpectedUserDataEntries()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"WordLensBackupTests-{Guid.NewGuid():N}");
        var appDataDirectory = Path.Combine(tempRoot, "appdata");
        var screenshotDirectory = Path.Combine(appDataDirectory, "Screenshots");
        var logsDirectory = Path.Combine(appDataDirectory, "logs");
        var cancellationToken = TestContext.Current.CancellationToken;

        Directory.CreateDirectory(screenshotDirectory);
        Directory.CreateDirectory(logsDirectory);
        await File.WriteAllTextAsync(Path.Combine(appDataDirectory, "settings.json"), "{}", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(appDataDirectory, "translation_history.db"), "sqlite", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(screenshotDirectory, "capture.txt"), "image", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(logsDirectory, "app.log"), "log", cancellationToken);

        try
        {
            var service = new BackupService(NullLogger<BackupService>.Instance, appDataDirectory);
            var result = await service.CreateBackupAsync(Path.Combine(tempRoot, "backup"), cancellationToken);

            Assert.EndsWith(".zip", result.DestinationPath);
            Assert.Equal(3, result.FileCount);
            Assert.True(result.SizeBytes > 0);

            using var archive = ZipFile.OpenRead(result.DestinationPath);
            var entries = archive.Entries.Select(static entry => entry.FullName).ToHashSet();

            Assert.Contains("backup-manifest.json", entries);
            Assert.Contains("settings.json", entries);
            Assert.Contains("translation_history.db", entries);
            Assert.Contains("Screenshots/capture.txt", entries);
            Assert.DoesNotContain("logs/app.log", entries);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RestoreBackupAsync_ReplacesUserDataAndCreatesPreRestoreBackup()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"WordLensBackupTests-{Guid.NewGuid():N}");
        var sourceDirectory = Path.Combine(tempRoot, "source");
        var sourceScreenshots = Path.Combine(sourceDirectory, "Screenshots");
        var appDataDirectory = Path.Combine(tempRoot, "appdata");
        var appScreenshots = Path.Combine(appDataDirectory, "Screenshots");
        var cancellationToken = TestContext.Current.CancellationToken;

        Directory.CreateDirectory(sourceScreenshots);
        Directory.CreateDirectory(appScreenshots);
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "settings.json"), "new-settings", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "translation_history.db"), "new-history", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(sourceScreenshots, "new.txt"), "new-screenshot", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(appDataDirectory, "settings.json"), "old-settings", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(appDataDirectory, "translation_history.db"), "old-history", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(appScreenshots, "old.txt"), "old-screenshot", cancellationToken);

        try
        {
            var backupCreator = new BackupService(NullLogger<BackupService>.Instance, sourceDirectory);
            var backup = await backupCreator.CreateBackupAsync(Path.Combine(tempRoot, "backup.zip"), cancellationToken);

            var restoreService = new BackupService(NullLogger<BackupService>.Instance, appDataDirectory);
            var result = await restoreService.RestoreBackupAsync(backup.DestinationPath, cancellationToken);

            Assert.Equal(3, result.FileCount);
            Assert.True(File.Exists(result.PreRestoreBackupPath));
            Assert.Equal("new-settings", await File.ReadAllTextAsync(Path.Combine(appDataDirectory, "settings.json"), cancellationToken));
            Assert.Equal("new-history", await File.ReadAllTextAsync(Path.Combine(appDataDirectory, "translation_history.db"), cancellationToken));
            Assert.True(File.Exists(Path.Combine(appScreenshots, "new.txt")));
            Assert.False(File.Exists(Path.Combine(appScreenshots, "old.txt")));

            using var preRestore = ZipFile.OpenRead(result.PreRestoreBackupPath);
            var preRestoreEntries = preRestore.Entries.Select(static entry => entry.FullName).ToHashSet();
            Assert.Contains("settings.json", preRestoreEntries);
            Assert.Contains("translation_history.db", preRestoreEntries);
            Assert.Contains("Screenshots/old.txt", preRestoreEntries);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RestoreBackupAsync_RejectsUnsafeManifestPaths()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"WordLensBackupTests-{Guid.NewGuid():N}");
        var appDataDirectory = Path.Combine(tempRoot, "appdata");
        var backupPath = Path.Combine(tempRoot, "malicious.zip");
        var cancellationToken = TestContext.Current.CancellationToken;

        Directory.CreateDirectory(appDataDirectory);

        try
        {
            using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
            {
                var manifestEntry = archive.CreateEntry("backup-manifest.json");
                await using (var stream = manifestEntry.Open())
                {
                    var manifest = new BackupManifest
                    {
                        CreatedAt = DateTimeOffset.UtcNow,
                        Files =
                        [
                            new BackupManifestFile
                            {
                                Path = "../evil.txt",
                                SizeBytes = 4
                            }
                        ]
                    };
                    await JsonSerializer.SerializeAsync(
                        stream,
                        manifest,
                        SourceGenerationContext.Default.BackupManifest,
                        cancellationToken);
                }

                var payloadEntry = archive.CreateEntry("../evil.txt");
                await using var payload = payloadEntry.Open();
                await payload.WriteAsync("evil"u8.ToArray(), cancellationToken);
            }

            var service = new BackupService(NullLogger<BackupService>.Instance, appDataDirectory);

            await Assert.ThrowsAsync<InvalidDataException>(
                async () => await service.RestoreBackupAsync(backupPath, cancellationToken));
            Assert.False(File.Exists(Path.Combine(tempRoot, "evil.txt")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }
}
