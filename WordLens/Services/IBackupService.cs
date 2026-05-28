using System.Threading;
using System.Threading.Tasks;

namespace WordLens.Services;

public interface IBackupService
{
    Task<BackupResult> CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default);
}

public sealed record BackupResult(string DestinationPath, int FileCount, long SizeBytes);
