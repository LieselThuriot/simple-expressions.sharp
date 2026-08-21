using BenchmarkDotNet.Attributes;
using Simple.Expressions;

namespace Simple.Expressions.Benchmarks;

/// <summary>
/// Measures the per-evaluation cost of the model access path. Expression parsing is cached
/// globally, so after the first iteration every benchmark here measures model access only.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class EvaluationBenchmarks
{
    private const string SimpleRule = "and(eq(#User.Name, \"Ada\"), gt(#Score, 5))";
    private const string DottedRule = "eq(#User.Address.City.Name, \"Brussels\")";
    private const string MembershipRule = "in(#User.Role, #AllowedRoles)";

    /// <summary>
    /// Twenty distinct rules, as a rules-engine caller would have. Each one currently pays for
    /// a full re-serialization of the same unchanged model.
    /// </summary>
    private static readonly string[] RuleSet =
    [
        "eq(#Score, 8)", "gt(#Score, 5)", "lt(#Score, 10)", "defined(#Score)",
        "eq(#User.Name, \"Ada\")", "startswith(#User.Name, \"A\")", "endswith(#User.Name, \"a\")",
        "contains(#User.Role, \"engineer\")", "not(empty(#User.Role))", "defined(#User.Address)",
        "eq(#User.Address.City.Name, \"Brussels\")", "gt(#User.Address.City.Population, 100)",
        "in(#User.Role, #AllowedRoles)", "eq(len(#AllowedRoles), 2)", "empty(#Missing)",
        "or(eq(#Score, 1), eq(#Score, 8))", "and(defined(#User), defined(#Score))",
        "match(#User.Name, \"^A\")", "upper(#User.Name)", "concat(#User.Name, \"!\")",
    ];

    private object _smallModel = null!;
    private object _largeModel = null!;
    private Dictionary<string, object?> _dictionaryModel = null!;
    private Func<object?, object?> _parsedSimpleRule = null!;

    [GlobalSetup]
    public void Setup()
    {
        _smallModel = MakeModel(payloadEntries: 0);
        _largeModel = MakeModel(payloadEntries: 200);
        _dictionaryModel = MakeDictionaryModel();
        _parsedSimpleRule = SimpleExpressions.ParseExpression(SimpleRule);

        // Warm the expression caches so the measured loop is model access only.
        SimpleExpressions.ExecuteExpression(_smallModel, SimpleRule);
        SimpleExpressions.ExecuteExpression(_smallModel, DottedRule);
        SimpleExpressions.ExecuteExpression(_smallModel, MembershipRule);
        foreach (string rule in RuleSet)
        {
            SimpleExpressions.ParseExpression(rule)(_smallModel);
        }
    }

    [Benchmark(Baseline = true, Description = "One rule, small model")]
    public bool SingleRule_SmallModel() => SimpleExpressions.ExecuteExpression(_smallModel, SimpleRule);

    [Benchmark(Description = "One rule, large model")]
    public bool SingleRule_LargeModel() => SimpleExpressions.ExecuteExpression(_largeModel, SimpleRule);

    [Benchmark(Description = "20 rules, one small model")]
    public int RuleSet_SmallModel()
    {
        int matched = 0;
        foreach (string rule in RuleSet)
        {
            if (SimpleExpressions.ExecuteExpression(_smallModel, rule))
            {
                matched++;
            }
        }

        return matched;
    }

    [Benchmark(Description = "20 rules, one large model")]
    public int RuleSet_LargeModel()
    {
        int matched = 0;
        foreach (string rule in RuleSet)
        {
            if (SimpleExpressions.ExecuteExpression(_largeModel, rule))
            {
                matched++;
            }
        }

        return matched;
    }

    [Benchmark(Description = "Deep dotted path, large model")]
    public bool DottedPath_LargeModel() => SimpleExpressions.ExecuteExpression(_largeModel, DottedRule);

    [Benchmark(Description = "in() over model array")]
    public bool Membership_SmallModel() => SimpleExpressions.ExecuteExpression(_smallModel, MembershipRule);

    [Benchmark(Description = "Dictionary model")]
    public bool DictionaryModel() => SimpleExpressions.ExecuteExpression(_dictionaryModel, SimpleRule);

    [Benchmark(Description = "Pre-parsed expression, small model")]
    public object? PreParsed_SmallModel() => _parsedSimpleRule(_smallModel);

    private static object MakeModel(int payloadEntries)
    {
        // Payload is never referenced by any expression. Under the serializing implementation
        // it is walked on every evaluation regardless; under lazy access it should be free.
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (int index = 0; index < payloadEntries; index++)
        {
            payload["field" + index] = new
            {
                Index = index,
                Label = "label-" + index,
                Enabled = index % 2 == 0,
                Nested = new { A = index, B = "b" + index, C = new { D = index * 2 } },
            };
        }

        return new
        {
            User = new
            {
                Name = "Ada",
                Role = "engineer",
                Address = new { City = new { Name = "Brussels", Population = 1200 } },
            },
            Score = 8,
            AllowedRoles = new[] { "editor", "engineer" },
            Payload = payload,
        };
    }

    private static Dictionary<string, object?> MakeDictionaryModel() => new(StringComparer.Ordinal)
    {
        ["User"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Name"] = "Ada",
            ["Role"] = "engineer",
            ["Address"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["City"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["Name"] = "Brussels",
                    ["Population"] = 1200,
                },
            },
        },
        ["Score"] = 8,
        ["AllowedRoles"] = new[] { "editor", "engineer" },
    };
}