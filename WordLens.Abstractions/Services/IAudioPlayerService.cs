namespace WordLens.Services;

public interface IAudioPlayerService
{
    Task PlayWaveAsync(byte[] waveData, CancellationToken cancellationToken);

    void Stop();
}
