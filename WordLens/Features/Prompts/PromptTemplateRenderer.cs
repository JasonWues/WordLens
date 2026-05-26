namespace WordLens.Features.Prompts;

internal static class PromptTemplateRenderer
{
    public static string RenderTranslation(
        string template,
        string text,
        string sourceLanguage,
        string targetLanguage)
    {
        return template
            .Replace("{text}", text)
            .Replace("{sourceLanguage}", sourceLanguage)
            .Replace("{targetLanguage}", targetLanguage);
    }

    public static string RenderOcr(string template, string languageCode)
    {
        return template.Replace("{languageCode}", languageCode);
    }
}
