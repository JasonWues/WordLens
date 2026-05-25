namespace WordLens.Abstractions.Services;

public interface ILocalTtsBackend
{
    Task SpeakAsync(
        string text,
        string? voice,
        double speed,
        CancellationToken cancellationToken = default);

    void Stop();
}
