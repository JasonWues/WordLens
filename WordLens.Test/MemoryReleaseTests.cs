using WordLens.Models;
using WordLens.ViewModels;

namespace WordLens.Test;

public class MemoryReleaseTests
{
    [Fact]
    public void ReleaseLoadedData_ClearsHistoryPageState()
    {
        var history = new TranslationHistory { Id = 1, SourceText = "retained text" };
        var viewModel = new TranslationHistoryViewModel
        {
            SelectedHistory = history,
            TotalCount = 1,
            HasMoreHistories = true
        };
        viewModel.Histories.Add(history);

        viewModel.ReleaseLoadedData();

        Assert.Empty(viewModel.Histories);
        Assert.Null(viewModel.SelectedHistory);
        Assert.Equal(0, viewModel.TotalCount);
        Assert.False(viewModel.HasMoreHistories);
        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.IsLoadingMore);
    }
}
