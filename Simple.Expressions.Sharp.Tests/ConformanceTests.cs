#nullable enable

using System.Text.Json;
using System.Text.Json.Nodes;
using SE = Simple.Expressions.SimpleExpressions;
using StjObject = System.Text.Json.Nodes.JsonObject;

namespace Simple.Expressions.Tests;

public sealed class ConformanceTests
{
    private static readonly IReadOnlyDictionary<string, StjObject> Cases = LoadCases();

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void MatchesSpecificationV1(string id)
    {
        var item = Cases[id];
        string expression = (string)item["expression"]!;
        object? model = ParseModel(item["model"]);

        if (item["error"] is JsonNode error)
        {
            var exception = Assert.ThrowsAny<Exception>(() => SE.ParseExpression(expression)(model));
            Assert.Contains((string)error!, exception.Message);
        }
        else if (item["truthy"] is JsonNode truthy)
        {
            Assert.Equal((bool)truthy!, SE.ExecuteExpression(model, expression));
        }
        else
        {
            AssertValue(item["value"], SE.ParseExpression(expression)(model));
        }
    }

    public static TheoryData<string> CaseIds
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (string? id in Cases.Keys.OrderBy(value => value, StringComparer.Ordinal))
            {
                data.Add(id);
            }

            return data;
        }
    }

    private static object? ParseModel(JsonNode? model) => model is null ? new Dictionary<string, object?>() : ToClrValue(model);

    private static object? ToClrValue(JsonNode? node) => node switch
    {
        null => null,
        JsonObject obj => obj.ToDictionary(pair => pair.Key, pair => ToClrValue(pair.Value), StringComparer.Ordinal),
        JsonArray array => array.Select(ToClrValue).ToArray(),
        _ => node.GetValueKind() switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => node.GetValue<double>(),
            JsonValueKind.String => node.GetValue<string>(),
            _ => throw new InvalidOperationException("Unsupported corpus value kind"),
        },
    };

    private static void AssertValue(JsonNode? expected, object? actual)
    {
        if (expected is null || expected.GetValueKind() == JsonValueKind.Null)
        { Assert.Null(actual); return; }

        switch (expected.GetValueKind())
        {
            case JsonValueKind.True:
            case JsonValueKind.False:
                Assert.Equal(expected.GetValue<bool>(), Assert.IsType<bool>(actual));
                break;
            case JsonValueKind.Number:
                Assert.Equal(expected.GetValue<double>(), Assert.IsType<double>(actual));
                break;
            case JsonValueKind.String:
                Assert.Equal(expected.GetValue<string>(), Assert.IsType<string>(actual));
                break;
            case JsonValueKind.Array:
                object?[] values = Assert.IsAssignableFrom<object?[]>(actual);
                var expectedValues = expected.AsArray();
                Assert.Equal(expectedValues.Count, values.Length);
                for (int index = 0; index < values.Length; index++)
                {
                    AssertValue(expectedValues[index], values[index]);
                }

                break;
            case JsonValueKind.Object:
                var actualObject = Assert.IsAssignableFrom<IDictionary<string, object?>>(actual);
                var expectedObject = expected.AsObject();
                Assert.Equal(expectedObject.Count, actualObject.Count);
                foreach (var property in expectedObject)
                {
                    Assert.True(actualObject.ContainsKey(property.Key));
                    AssertValue(property.Value, actualObject[property.Key]);
                }

                break;
            default:
                Assert.Fail("Unsupported expectation kind");
                break;
        }
    }

    private static IReadOnlyDictionary<string, StjObject> LoadCases()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "conformance", "spec", "v1", "cases.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        return document["cases"]!.AsArray()
            .Select(node => node!.AsObject())
            .ToDictionary(item => (string)item["id"]!, StringComparer.Ordinal);
    }
}
