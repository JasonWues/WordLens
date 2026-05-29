using WordLens.Services;

namespace WordLens.Messages;

public sealed class BackupRestoredMessage(RestoreBackupResult result)
{
    public RestoreBackupResult Result { get; } = result;
}
