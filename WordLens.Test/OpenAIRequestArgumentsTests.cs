using System;
using System.Text.Json;
using WordLens.Providers.OpenAI;

namespace WordLens.Test;

public class OpenAIRequestArgumentsTests
{
    [Fact]
    public void Parse_ReturnsNull_ForEmptyInput()
    {
        Assert.Null(OpenAIRequestArguments.Parse(null));
        Assert.Null(OpenAIRequestArguments.Parse(""));
        Assert.Null(OpenAIRequestArguments.Parse("   "));
    }

    [Fact]
    public void Parse_ReturnsClonedArguments_AndSkipsReservedProperties()
    {
        var arguments = OpenAIRequestArguments.Parse(
            """
            {
              "temperature": 0.2,
              "MODEL": "ignored",
              "messages": ["ignored"],
              "metadata": { "trace": true }
            }
            """,
            "model",
            "messages");

        Assert.NotNull(arguments);
        Assert.Equal(2, arguments.Count);
        Assert.Equal(0.2, arguments["temperature"].GetDouble());
        Assert.True(arguments["metadata"].GetProperty("trace").GetBoolean());
        Assert.False(arguments.ContainsKey("MODEL"));
        Assert.False(arguments.ContainsKey("messages"));
    }

    [Fact]
    public void Parse_ReturnsNull_WhenOnlyReservedPropertiesExist()
    {
        var arguments = OpenAIRequestArguments.Parse(
            """{ "model": "gpt-test", "messages": [] }""",
            "model",
            "messages");

        Assert.Null(arguments);
    }

    [Fact]
    public void Parse_ThrowsInvalidOperationException_ForMalformedJson()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => OpenAIRequestArguments.Parse("{"));

        Assert.Contains("有效 JSON 对象", ex.Message);
        Assert.IsAssignableFrom<JsonException>(ex.InnerException);
    }

    [Fact]
    public void Parse_ThrowsInvalidOperationException_ForNonObjectJson()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => OpenAIRequestArguments.Parse("[]"));

        Assert.Contains("JSON 对象", ex.Message);
    }
}
