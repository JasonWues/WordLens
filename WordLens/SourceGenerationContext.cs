using System.Text.Json.Serialization;
using System.Collections.Generic;
using WordLens.Models;

namespace WordLens;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(ChatCompletionRequest))]
[JsonSerializable(typeof(OcrChatCompletionRequest))]
[JsonSerializable(typeof(TtsSpeechRequest))]
[JsonSerializable(typeof(ModelInfo))]
[JsonSerializable(typeof(OpenAIModelResponse))]
[JsonSerializable(typeof(DeepLTranslationRequest))]
[JsonSerializable(typeof(DeepLTranslationResponse))]
[JsonSerializable(typeof(GitHubReleaseResponse))]
[JsonSerializable(typeof(StreamChunk))]
[JsonSerializable(typeof(List<TranslationHistoryResult>))]
public partial class SourceGenerationContext : JsonSerializerContext
{
}
