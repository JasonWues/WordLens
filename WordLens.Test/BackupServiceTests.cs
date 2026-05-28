using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
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
}
