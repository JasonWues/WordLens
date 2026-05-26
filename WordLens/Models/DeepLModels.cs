using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WordLens.Models;

public sealed class DeepLTranslationRequest
{
    [JsonPropertyName("text")]
    public IList<string> Text { get; set; } = new List<string>();

    [JsonPropertyName("target_lang")]
    public string TargetLanguage { get; set; } = string.Empty;

    [JsonPropertyName("source_lang")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceLanguage { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class DeepLTranslationResponse
{
    [JsonPropertyName("translations")]
    public List<DeepLTranslation> Translations { get; set; } = new();
}

public sealed class DeepLTranslation
{
    [JsonPropertyName("detected_source_language")]
    public string DetectedSourceLanguage { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
