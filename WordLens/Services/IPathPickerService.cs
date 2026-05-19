using System.Collections.Generic;
using System.Threading.Tasks;

namespace WordLens.Services;

public interface IPathPickerService
{
    Task<string?> PickFileAsync(string title, IReadOnlyList<string> patterns);

    Task<IReadOnlyList<string>> PickFilesAsync(string title, IReadOnlyList<string> patterns);

    Task<string?> PickFolderAsync(string title);
}
