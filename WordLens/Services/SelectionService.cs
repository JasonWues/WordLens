using System.Threading.Tasks;
using WordLens.Native;

namespace WordLens.Services
{
    public interface ISelectionService
    {
        string GetSelectedTex();

        Task<string?> GetSelectedTextAsync();
    }

    public class SelectionService : ISelectionService
    {
        public string GetSelectedTex()
        {
            return SelectionNative.GetSelectionText();
        }

        public Task<string?> GetSelectedTextAsync()
        {
            var text = SelectionNative.GetSelectionText();
            return Task.FromResult(string.IsNullOrWhiteSpace(text) ? null : text);
        }
    }
}