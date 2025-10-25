using System.Text.Json.Serialization;
using WordLens.Models;

namespace WordLens;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(ChatCompletionRequest))]
[JsonSerializable(typeof(ModelInfo))]
[JsonSerializable(typeof(OpenAIModelResponse))]
public partial class SourceGenerationContext : JsonSerializerContext
{
}