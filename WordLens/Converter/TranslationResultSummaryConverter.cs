using System.Linq;
using System.Text.Json;
using Avalonia.Data.Converters;

namespace WordLens.Converter;

public static class TranslationResultSummaryConverter
{
    public static FuncValueConverter<string?, string> Summary { get; } =
        new(json =>
        {
            if (string.IsNullOrWhiteSpace(json))
                return "无翻译结果";

            try
            {
                var results = JsonSerializer.Deserialize(
                    json,
                    SourceGenerationContext.Default.ListTranslationHistoryResult);
                var firstResult = results?.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Result))?.Result;
                if (string.IsNullOrWhiteSpace(firstResult))
                    return "无翻译结果";

                return firstResult.Length > 160
                    ? firstResult.Substring(0, 160) + "..."
                    : firstResult;
            }
            catch (JsonException)
            {
                return "解析结果失败";
            }
        });
}
