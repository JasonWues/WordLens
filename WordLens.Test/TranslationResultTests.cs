using WordLens.Models;

namespace WordLens.Test;

public class TranslationResultTests
{
    [Fact]
    public void DurationText_FormatsMillisecondsUnderOneSecond()
    {
        var result = new TranslationResult { DurationMs = 320 };

        Assert.True(result.HasDuration);
        Assert.Equal("320ms", result.DurationText);
    }

    [Fact]
    public void DurationText_FormatsSecondsWithOneDecimal()
    {
        var result = new TranslationResult { DurationMs = 1250 };

        Assert.True(result.HasDuration);
        Assert.Equal("1.3s", result.DurationText);
    }

    [Fact]
    public void DurationText_IsEmpty_WhenDurationMissing()
    {
        var result = new TranslationResult();

        Assert.False(result.HasDuration);
        Assert.Equal(string.Empty, result.DurationText);
    }

    [Fact]
    public void IsError_IsTrue_WhenFinishedUnsuccessfully()
    {
        var result = new TranslationResult
        {
            IsLoading = false,
            IsSuccess = false,
            ErrorMessage = "boom"
        };

        Assert.True(result.IsError);
        Assert.False(result.HasVisibleResult);
    }
}
