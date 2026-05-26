using System;
using System.Collections.Generic;
using System.Text.Json;

namespace WordLens.Providers.OpenAI;

public static class OpenAIRequestArguments
{
    public static Dictionary<string, JsonElement>? Parse(string? value, params string[] reservedPropertyNames)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(value);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Request Arguments 必须是有效 JSON 对象。", ex);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Request Arguments 必须是 JSON 对象。");
            }

            var reserved = new HashSet<string>(reservedPropertyNames, StringComparer.OrdinalIgnoreCase);
            var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (reserved.Contains(property.Name))
                {
                    continue;
                }

                arguments[property.Name] = property.Value.Clone();
            }

            return arguments.Count == 0 ? null : arguments;
        }
    }
}
