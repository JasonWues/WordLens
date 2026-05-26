using WordLens.Models;

namespace WordLens.Test;

public class LanguageInfoTests
{
    [Fact]
    public void ToString_ReturnsDisplayAndNativeNames()
    {
        var language = new LanguageInfo("ja", "日语", "日本語");

        Assert.Equal("日语 (日本語)", language.ToString());
    }

    [Fact]
    public void GetCommonLanguages_IncludesAutoDetect()
    {
        var languages = LanguageInfo.GetCommonLanguages();

        Assert.Contains(languages, language => language.Code == "auto");
        Assert.Contains(languages, language => language.Code == "zh-CN");
        Assert.Contains(languages, language => language.Code == "en");
    }

    [Fact]
    public void GetTargetLanguages_ExcludesAutoDetect()
    {
        var languages = LanguageInfo.GetTargetLanguages();

        Assert.DoesNotContain(languages, language => language.Code == "auto");
        Assert.Contains(languages, language => language.Code == "en");
    }
}
