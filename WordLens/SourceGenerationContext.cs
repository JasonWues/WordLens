using System.Text.Json.Serialization;
using WordLens.Models;

namespace WordLens
{
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(AppSettings))]
    [JsonSerializable(typeof(ChatCompletionRequest))]
    public partial class SourceGenerationContext : JsonSerializerContext
    {
        
    }
}