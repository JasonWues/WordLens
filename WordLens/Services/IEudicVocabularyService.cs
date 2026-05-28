using System.Threading;
using System.Threading.Tasks;

namespace WordLens.Services;

public interface IEudicVocabularyService
{
    Task<EudicVocabularyAddResult> AddWordAsync(
        string word,
        string? contextLine,
        CancellationToken cancellationToken);
}

public sealed record EudicVocabularyAddResult(bool IsSuccess, string Message);
