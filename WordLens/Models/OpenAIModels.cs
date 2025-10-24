using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WordLens.Models
{
    /// <summary>
    /// OpenAI模型信息
    /// </summary>
    public class ModelInfo
    {
        /// <summary>
        /// 模型ID（例如：gpt-4o, gpt-4o-mini）
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 对象类型（通常是"model"）
        /// </summary>
        [JsonPropertyName("object")]
        public string Object { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间（Unix时间戳）
        /// </summary>
        [JsonPropertyName("created")]
        public long Created { get; set; }

        /// <summary>
        /// 所有者（例如："openai", "system"）
        /// </summary>
        [JsonPropertyName("owned_by")]
        public string OwnedBy { get; set; } = string.Empty;

        /// <summary>
        /// 权限信息（可选）
        /// </summary>
        [JsonPropertyName("permission")]
        public List<object>? Permission { get; set; }

        /// <summary>
        /// 根模型（可选）
        /// </summary>
        [JsonPropertyName("root")]
        public string? Root { get; set; }

        /// <summary>
        /// 父模型（可选）
        /// </summary>
        [JsonPropertyName("parent")]
        public string? Parent { get; set; }

        public override string ToString()
        {
            return Id;
        }
    }

    /// <summary>
    /// OpenAI模型列表响应
    /// </summary>
    public class OpenAIModelResponse
    {
        /// <summary>
        /// 对象类型（通常是"list"）
        /// </summary>
        [JsonPropertyName("object")]
        public string Object { get; set; } = string.Empty;

        /// <summary>
        /// 模型列表
        /// </summary>
        [JsonPropertyName("data")]
        public List<ModelInfo> Data { get; set; } = new List<ModelInfo>();
    }
}