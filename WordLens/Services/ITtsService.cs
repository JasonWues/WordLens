using System.Threading;
using System.Threading.Tasks;

namespace WordLens.Services;

public interface ITtsService
{
    Task SpeakAsync(string? text, CancellationToken cancellationToken = default);

    void Stop();
}
