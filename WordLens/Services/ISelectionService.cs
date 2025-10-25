using System.Threading.Tasks;
using WordLens.Native;

namespace WordLens.Services;

public interface ISelectionService
{
    string GetSelectedTex();

    Task<string?> GetSelectedTextAsync();
}
