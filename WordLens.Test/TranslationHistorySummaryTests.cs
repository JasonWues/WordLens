using System.Collections.Generic;
using System.Text.Json;
using WordLens;
using WordLens.Models;
using WordLens.ViewModels;

namespace WordLens.Test;

public class TranslationHistorySummaryTests
{
    [Fact]
    public void GetResultSummary_ReturnsEmptyMessage_ForMissingResults()
    {
        var history = new TranslationHistory();

        Assert.Equal("无翻译结果", TranslationHistoryViewModel.GetResultSummary(history));
    }

    [Fact]
    public void GetResultSummary_ReturnsFirstResult()
    {
        var history = CreateHistoryWithResults("第一条结果", "第二条结果");

        Assert.Equal("第一条结果", TranslationHistoryViewModel.GetResultSummary(history));
    }

    [Fact]
    public void GetResultSummary_TruncatesLongResult()
    {
        var result = new string('A', 101);
        var history = CreateHistoryWithResults(result);

        Assert.Equal(new string('A', 100) + "...", TranslationHistoryViewModel.GetResultSummary(history));
    }

    [Fact]
    public void GetResultSummary_ReturnsParseError_ForInvalidJson()
    {
        var history = new TranslationHistory { ResultsJson = "not-json" };

        Assert.Equal("解析结果失败", TranslationHistoryViewModel.GetResultSummary(history));
    }

    private static TranslationHistory CreateHistoryWithResults(params string[] results)
    {
        var items = new List<TranslationHistoryResult>();
        foreach (var result in results)
        {
            items.Add(new TranslationHistoryResult
            {
                ProviderName = "provider",
                Result = result
            });
        }

        return new TranslationHistory
        {
            ResultsJson = JsonSerializer.Serialize(items, SourceGenerationContext.Default.ListTranslationHistoryResult)
        };
    }
}
