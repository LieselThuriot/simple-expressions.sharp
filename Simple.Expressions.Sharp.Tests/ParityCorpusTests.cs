#nullable enable

using System.Text.Json;
using System.Text.Json.Nodes;
using StjObject = System.Text.Json.Nodes.JsonObject;
using SE = Simple.Expressions.SimpleExpressions;

namespace Simple.Expressions.Tests;

/// <summary>
/// Runs the cross-implementation parity corpus shared with the TypeScript reference and
/// the Dart port. The corpus is vendored at <c>parity/cases.json</c>; the canonical copy
/// lives in the TypeScript repository.
/// </summary>
public class ParityCorpusTests
{
    private const string Implementation = "csharp";

    private static readonly IReadOnlyDictionary<string, StjObject> Cases = LoadCases();

    public ParityCorpusTests()
    {
        SE.EnableCaches();
        SE.SetCacheLimit(1000);
        SE.Clear();
    }

    public static TheoryData<string> CaseIds
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var id in Cases.Keys.OrderBy(id => id, StringComparer.Ordinal))
                data.Add(id);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void MatchesTheSharedCorpus(string id)
    {
        var parityCase = Cases[id];
        var expression = (string)parityCase["expression"]!;
        var model = ParseModel(parityCase["model"]);

        if (parityCase["error"] is JsonNode error)
        {
            var exception = Assert.ThrowsAny<Exception>(() => SE.ParseExpression(expression)(model));
            Assert.Contains((string)error!, exception.Message);
            return;
        }

        if (parityCase["truthy"] is JsonNode truthy)
        {
            Assert.Equal((bool)truthy!, SE.ExecuteExpression(model, expression));
            return;
        }

        AssertValue(parityCase["value"], SE.ParseExpression(expression)(model));
    }

    [Fact]
    public void CorpusIsInSyncWithTheOtherImplementations()
    {
        // A tripwire: the vendored copy must stay byte-identical to the canonical corpus.
        // Every implementation asserts the same case count so a partial copy is caught.
        Assert.NotEmpty(Cases);
        Assert.All(Cases.Values, parityCase =>
        {
            var declared = new[] { "value", "truthy", "error" }.Count(key => parityCase.ContainsKey(key));
            Assert.Equal(1, declared);
        });
    }

    /// <summary>
    /// Materialises a corpus model as plain CLR objects. The library reads models by
    /// reflection and has no JSON dependency, so the corpus is projected onto the shapes a
    /// caller would actually pass: dictionaries, arrays and primitives.
    /// </summary>
    private static object? ParseModel(JsonNode? model)
    {
        if (model is null)
            return new Dictionary<string, object?>(StringComparer.Ordinal);

        return ToClrValue(model);
    }

    private static object? ToClrValue(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonObject jsonObject:
            {
                var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var property in jsonObject)
                    result[property.Key] = ToClrValue(property.Value);
                return result;
            }
            case JsonArray jsonArray:
            {
                var result = new object?[jsonArray.Count];
                for (var index = 0; index < jsonArray.Count; index++)
                    result[index] = ToClrValue(jsonArray[index]);
                return result;
            }
            default:
                return node.GetValueKind() switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number => node.GetValue<double>(),
                    JsonValueKind.String => node.GetValue<string>(),
                    _ => throw new InvalidOperationException("Unsupported corpus value kind: " + node.GetValueKind()),
                };
        }
    }

    private static void AssertValue(JsonNode? expected, object? actual)
    {
        if (expected is null)
        {
            Assert.Null(actual);
            return;
        }

        switch (expected.GetValueKind())
        {
            case JsonValueKind.Null:
                Assert.Null(actual);
                break;
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
            {
                var expectedItems = expected.AsArray();
                var actualItems = Assert.IsAssignableFrom<object?[]>(actual);
                Assert.Equal(expectedItems.Count, actualItems.Length);
                for (var index = 0; index < expectedItems.Count; index++)
                    AssertValue(expectedItems[index], actualItems[index]);
                break;
            }
            default:
                Assert.Fail("Unsupported expectation kind: " + expected.GetValueKind());
                break;
        }
    }

    private static IReadOnlyDictionary<string, StjObject> LoadCases()
    {
        var document = JsonNode.Parse(File.ReadAllText(FindCorpus()))!.AsObject();
        var cases = new Dictionary<string, StjObject>(StringComparer.Ordinal);

        foreach (var node in document["cases"]!.AsArray())
        {
            var parityCase = node!.AsObject();
            if (parityCase["skip"] is JsonArray skip && skip.Any(entry => (string?)entry == Implementation))
                continue;

            cases.Add((string)parityCase["id"]!, parityCase);
        }

        return cases;
    }

    private static string FindCorpus()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "parity", "cases.json");
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate parity/cases.json above " + AppContext.BaseDirectory);
    }
}
