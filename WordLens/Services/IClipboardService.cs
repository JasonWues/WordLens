using System.Threading.Tasks;

namespace WordLens.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text);
}
