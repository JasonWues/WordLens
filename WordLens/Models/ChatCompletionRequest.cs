using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WordLens.Models;

public class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("messages")]
    public IList<ChatMessage> Messages { get; set; }

    /// <summary>
    ///     是否启用流式输出
    /// </summary>
    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;
}

public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }
}