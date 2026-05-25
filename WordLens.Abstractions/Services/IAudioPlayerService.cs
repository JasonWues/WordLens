namespace WordLens.Abstractions.Services;

public interface IAudioPlayerService
{
    Task PlayWaveAsync(byte[] waveData, CancellationToken cancellationToken);

    void Stop();
}
