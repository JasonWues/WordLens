using System.Text.Json.Serialization;
using System.Collections.Generic;
using WordLens.Models;
using WordLens.Services.Implementations;
using WordLens.Services.LocalApi;

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
[JsonSerializable(typeof(LocalApiHealthResponse))]
[JsonSerializable(typeof(LocalApiStatusResponse))]
[JsonSerializable(typeof(TranslateApiRequest))]
[JsonSerializable(typeof(TranslateApiResponse))]
[JsonSerializable(typeof(TranslateApiResult))]
[JsonSerializable(typeof(OpenTranslationWindowApiRequest))]
[JsonSerializable(typeof(ApiErrorResponse))]
[JsonSerializable(typeof(EudicAddWordRequest))]
[JsonSerializable(typeof(EudicApiMessageResponse))]
public partial class SourceGenerationContext : JsonSerializerContext
{
}
