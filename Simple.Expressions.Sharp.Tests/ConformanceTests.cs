#nullable enable

using Simple;
using System.Text.Json;
using System.Text.Json.Nodes;
using SE = Simple.Expressions.SimpleExpressions;
using StjObject = System.Text.Json.Nodes.JsonObject;

namespace Simple.Expressions.Sharp.Tests;

public sealed class ConformanceTests
{
    private static readonly string[] s_expectationNames = ["value", "truthy", "error"];
    private static readonly HashSet<string> s_allowedDocumentProperties = new(StringComparer.Ordinal)
    {
        "$comment", "cases",
    };
    private static readonly HashSet<string> s_allowedCaseProperties = new(StringComparer.Ordinal)
    {
        "$comment", "id", "expression", "model", "value", "truthy", "error",
    };
    private static readonly IReadOnlyDictionary<string, StjObject> s_cases = LoadCases();

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void MatchesSpecificationV1(string id)
    {
        StjObject item = s_cases[id];
        string expression = (string)item["expression"]!;
        object? model = ParseModel(item);

        if (item.ContainsKey("error"))
        {
            JsonNode error = item["error"]!;
            Exception exception = Assert.ThrowsAny<Exception>(() => SE.ParseExpression(expression)(model));
            Assert.Contains((string)error!, exception.Message);
        }
        else if (item.ContainsKey("truthy"))
        {
            JsonNode truthy = item["truthy"]!;
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
            foreach (string? id in s_cases.Keys.OrderBy(value => value, StringComparer.Ordinal))
            {
                data.Add(id);
            }

            return data;
        }
    }

    private static object? ParseModel(StjObject item)
        => item.ContainsKey("model") ? ToClrValue(item["model"]) : new Dictionary<string, object?>();

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
        {
            Assert.Null(actual);
            return;
        }

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
                object?[] values = Assert.IsType<object?[]>(actual, exactMatch: false);
                JsonArray expectedValues = expected.AsArray();
                Assert.Equal(expectedValues.Count, values.Length);
                for (int index = 0; index < values.Length; index++)
                {
                    AssertValue(expectedValues[index], values[index]);
                }

                break;
            case JsonValueKind.Object:
                IDictionary<string, object?> actualObject = Assert.IsType<IDictionary<string, object?>>(actual, exactMatch: false);
                StjObject expectedObject = expected.AsObject();
                Assert.Equal(expectedObject.Count, actualObject.Count);
                foreach (KeyValuePair<string, JsonNode?> property in expectedObject)
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

    [Fact]
    public void CasesRequireExactlyOneExpectation()
    {
        ValidateCase(new StjObject { ["id"] = "value-null", ["expression"] = "#value", ["value"] = null });
        Assert.Throws<InvalidDataException>(() => ValidateCase(new StjObject { ["id"] = "none", ["expression"] = "true" }));
        Assert.Throws<InvalidDataException>(() => ValidateCase(new StjObject { ["id"] = "multiple", ["expression"] = "true", ["value"] = 1, ["truthy"] = true }));
        Assert.Throws<InvalidDataException>(() => ValidateCase(new StjObject { ["id"] = "bad-truthy", ["expression"] = "true", ["truthy"] = "yes" }));
        Assert.Throws<InvalidDataException>(() => ValidateCase(new StjObject { ["id"] = "bad-error", ["expression"] = "true", ["error"] = null }));
        Assert.Throws<InvalidDataException>(() => ValidateCase(new StjObject { ["id"] = "blank-error", ["expression"] = "true", ["error"] = " " }));
    }

    [Fact]
    public void DocumentRequiresValidShape()
    {
        ValidateDocument(new StjObject { ["$comment"] = new JsonArray("comment"), ["cases"] = new JsonArray() });
        Assert.Throws<InvalidDataException>(() => ValidateDocument(new JsonArray()));
        Assert.Throws<InvalidDataException>(() => ValidateDocument(new StjObject()));
        Assert.Throws<InvalidDataException>(() => ValidateDocument(new StjObject { ["cases"] = new StjObject() }));
        Assert.Throws<InvalidDataException>(() => ValidateDocument(new StjObject { ["cases"] = new JsonArray(), ["extra"] = true }));
        Assert.Throws<InvalidDataException>(() => ValidateDocument(new StjObject { ["$comment"] = "comment", ["cases"] = new JsonArray() }));
        Assert.Throws<InvalidDataException>(() => ValidateDocument(new StjObject { ["$comment"] = new JsonArray("comment", 1), ["cases"] = new JsonArray() }));
    }

    [Theory]
#pragma warning disable xUnit1044 // Avoid using TheoryData type arguments that are not serializable
    [MemberData(nameof(InvalidCaseShapes))]
#pragma warning restore xUnit1044 // Avoid using TheoryData type arguments that are not serializable
    public void CasesRequireValidShape(StjObject item)
        => Assert.Throws<InvalidDataException>(() => ValidateCase(item));

    public static TheoryData<StjObject> InvalidCaseShapes =>
    [
        new StjObject { ["id"] = null, ["expression"] = "true", ["truthy"] = true },
        new StjObject { ["id"] = "", ["expression"] = "true", ["truthy"] = true },
        new StjObject { ["id"] = "case", ["expression"] = null, ["truthy"] = true },
        new StjObject { ["id"] = "case", ["expression"] = 1, ["truthy"] = true },
        new StjObject { ["id"] = "case", ["expression"] = "true", ["truthy"] = true, ["extra"] = 1 },
    ];

    [Fact]
    public void CaseIdsMustBeUnique()
    {
        var item = new StjObject { ["id"] = "duplicate", ["expression"] = "true", ["truthy"] = true };
        Assert.Throws<InvalidDataException>(() => ValidateCases([item, (StjObject)item.DeepClone()]));
    }

    [Fact]
    public void AdapterDistinguishesMissingAndExplicitNullModels()
    {
        Assert.IsType<Dictionary<string, object?>>(ParseModel([]));
        Assert.Null(ParseModel(new StjObject { ["model"] = null }));
    }

    private static Dictionary<string, StjObject> LoadCases()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "conformance", "spec", "v1", "cases.json");
        var root = JsonNode.Parse(File.ReadAllText(path));
        StjObject document = ValidateDocument(root);
        var caseNodes = (JsonArray)document["cases"]!;

        var cases = new List<StjObject>(caseNodes.Count);
        foreach (JsonNode? node in caseNodes)
        {
            if (node is not StjObject item)
            {
                throw new InvalidDataException("Each conformance case must be an object.");
            }

            cases.Add(item);
        }

        ValidateCases(cases);
        return cases.ToDictionary(item => (string)item["id"]!, StringComparer.Ordinal);
    }

    private static StjObject ValidateDocument(JsonNode? root)
    {
        if (root is not StjObject document || document["cases"] is not JsonArray)
        {
            throw new InvalidDataException("Conformance corpus must be an object containing a cases array.");
        }

        if (document.Any(property => !s_allowedDocumentProperties.Contains(property.Key)))
        {
            throw new InvalidDataException("Unknown property in conformance corpus.");
        }

        if (document.ContainsKey("$comment"))
        {
            if (document["$comment"] is not JsonArray comments ||
                 comments.Any(comment => comment?.GetValueKind() != JsonValueKind.String))
            {
                throw new InvalidDataException("Corpus comment must be an array of strings.");
            }
        }

        return document;
    }

    private static void ValidateCases(IEnumerable<StjObject> cases)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (StjObject item in cases)
        {
            ValidateCase(item);
            string id = item["id"]!.GetValue<string>();
            if (!ids.Add(id))
            {
                throw new InvalidDataException("Duplicate conformance case id: " + id);
            }
        }
    }

    private static void ValidateCase(StjObject item)
    {
        if (item.Any(property => !s_allowedCaseProperties.Contains(property.Key)))
        {
            throw new InvalidDataException("Unknown property in conformance case: " + (string?)item["id"]);
        }

        if (item["id"]?.GetValueKind() != JsonValueKind.String || string.IsNullOrWhiteSpace(item["id"]!.GetValue<string>()))
        {
            throw new InvalidDataException("Case id must be a non-empty string.");
        }

        string id = item["id"]!.GetValue<string>();
        if (item["expression"]?.GetValueKind() != JsonValueKind.String)
        {
            throw new InvalidDataException("Case expression must be a string: " + id);
        }

        if (item.ContainsKey("$comment") && item["$comment"]?.GetValueKind() != JsonValueKind.String)
        {
            throw new InvalidDataException("Case comment must be a string: " + id);
        }

        int expectations = s_expectationNames.Count(item.ContainsKey);
        if (expectations != 1)
        {
            throw new InvalidDataException("Each case must have one expectation: " + id);
        }

        if (item.ContainsKey("truthy") && item["truthy"]?.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException("Truthy expectation must be boolean: " + id);
        }

        if (item.ContainsKey("error") &&
            (item["error"]?.GetValueKind() != JsonValueKind.String ||
             string.IsNullOrWhiteSpace(item["error"]!.GetValue<string>())))
        {
            throw new InvalidDataException("Error expectation must be a non-empty string: " + id);
        }
    }
}