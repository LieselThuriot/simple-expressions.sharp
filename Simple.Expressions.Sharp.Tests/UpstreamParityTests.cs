using Simple.Expressions;
using SE = Simple.Expressions.SimpleExpressions;

namespace Simple.Expressions.Tests;

public class UpstreamParityTests
{
    public UpstreamParityTests()
    {
        SE.EnableCaches();
        SE.SetCacheLimit(1000);
        SE.Clear();
    }

    [Theory]
    [InlineData("+12", 12d)]
    [InlineData("-.5", -0.5d)]
    [InlineData("1.", 1d)]
    [InlineData("1e3", 1000d)]
    [InlineData("-2.5E-2", -0.025d)]
    public void ParseExpressionSupportsCurrentNumericGrammar(string expression, double expected)
    {
        Assert.Equal(expected, SE.ParseExpression(expression)(new { }));
    }

    [Theory]
    [InlineData("contains(#value, \"world\")", true)]
    [InlineData("contains(#value, \"World\")", false)]
    [InlineData("startswith(#value, \"Hello\")", true)]
    [InlineData("endswith(#value, \"world\")", true)]
    public void StringOperatorsAreCaseSensitive(string expression, bool expected)
    {
        Assert.Equal(expected, SE.ExecuteExpression(new { value = "Hello world" }, expression));
    }

    [Fact]
    public void StringOperatorsRequireAStringLeftOperand()
    {
        Assert.False(SE.ExecuteExpression(new { value = 12345 }, "contains(#value, \"23\")"));
        Assert.False(SE.ExecuteExpression(new { value = 12345 }, "startswith(#value, \"12\")"));
        Assert.False(SE.ExecuteExpression(new { value = 12345 }, "endswith(#value, \"45\")"));
    }

    [Fact]
    public void VariadicLogicalAndConcatOperatorsMatchUpstream()
    {
        Assert.True(SE.ExecuteExpression(new { }, "and(true, true, true)"));
        Assert.False(SE.ExecuteExpression(new { }, "and(true, false, true)"));
        Assert.True(SE.ExecuteExpression(new { }, "or(false, false, true)"));
        Assert.Equal("ab", SE.ParseExpression("concat(\"a\", 0, false, #missing, \"b\")")(new { }));
    }

    [Fact]
    public void LogicalOperatorsAndIfOnlyEvaluateTheSelectedExpressions()
    {
        var model = new { value = new { } };
        Assert.False(SE.ExecuteExpression(model, "and(false, len(#value))"));
        Assert.True(SE.ExecuteExpression(model, "or(true, len(#value))"));
        Assert.Equal("yes", SE.ParseExpression("if(true, \"yes\", len(#value))")(model));
        Assert.Equal("no", SE.ParseExpression("if(false, len(#value), \"no\")")(model));
    }

    [Fact]
    public void CaseInsensitiveNewOperatorsAndRawValuesAreSupported()
    {
        Assert.Equal("hello", SE.ParseExpression("LOWER(\"Hello\")")(new { }));
        Assert.Equal("HELLO", SE.ParseExpression("upper(\"Hello\")")(new { }));
        Assert.True(SE.ExecuteExpression(new { }, "STARTSWITH(\"Hello\", \"He\")"));
        Assert.False(SE.ExecuteExpression(new { }, "if(eq(1, 1), 0, 1)"));
    }

    [Fact]
    public void InSupportsLiteralAndModelListsWithLooseEquality()
    {
        Assert.True(SE.ExecuteExpression(new { }, "in(1, [\"1\", 2])"));
        Assert.True(SE.ExecuteExpression(new { }, "in(false, [0, true])"));
        Assert.False(SE.ExecuteExpression(new { }, "in(\"yes\", [])"));
        Assert.True(SE.ExecuteExpression(new { value = "yes", allowed = new[] { "yes", "no" } }, "in(#value, #allowed)"));
        Assert.True(SE.ExecuteExpression(new { }, "in(\"yes\", [lower(\"YES\"), #missing])"));
        Assert.False(SE.ExecuteExpression(new { }, "in(\"yes\", \"yes\")"));
    }

    [Fact]
    public void ListLiteralsEvaluateNestedExpressionsAndPreserveValues()
    {
        var expression = SE.ParseExpression("[1, lower(\"YES\"), [true, 2]]");

        Assert.Equal(new object[] { 1d, "yes", new object[] { true, 2d } }, expression(new { }));
        Assert.True(SE.ExecuteExpression(new { }, "eq(len([1, 2, 3]), 3)"));
    }

    [Fact]
    public void TrimPreservesNullishValuesAndConvertsOtherValues()
    {
        Assert.Equal("hello world", SE.ParseExpression("trim(\"  hello world  \")")(new { }));
        Assert.Equal("123", SE.ParseExpression("trim(123)")(new { }));
        Assert.Equal("", SE.ParseExpression("trim(false)")(new { }));
        Assert.Null(SE.ParseExpression("trim(null)")(new { }));
        Assert.Null(SE.ParseExpression("trim(#missing)")(new { }));
    }

    [Fact]
    public void DefinedOnlyRejectsNullAndMissingValues()
    {
        Assert.True(SE.ExecuteExpression(new { }, "defined(\"\")"));
        Assert.True(SE.ExecuteExpression(new { value = 0 }, "DEFINED(#value)"));
        Assert.False(SE.ExecuteExpression(new { value = (object?)null }, "defined(#value)"));
        Assert.False(SE.ExecuteExpression(new { }, "defined(#missing)"));
    }

    [Fact]
    public void JavaScriptComparisonAndValueSemanticsArePreserved()
    {
        Assert.True(SE.ExecuteExpression(new { }, "eq(1, \"1\")"));
        Assert.True(SE.ExecuteExpression(new { }, "eq(false, 0)"));
        Assert.False(SE.ExecuteExpression(new { }, "eq(null, false)"));
        Assert.False(SE.ExecuteExpression(new { }, "eq(null, 0)"));
        Assert.False(SE.ExecuteExpression(new { }, "eq(#missing, false)"));
        Assert.False(SE.ExecuteExpression(new { }, "eq(#missing, 0)"));
        Assert.True(SE.ExecuteExpression(new { }, "gt(10, 2)"));
        Assert.True(SE.ExecuteExpression(new { }, "empty(#missing)"));
        Assert.False(SE.ExecuteExpression(new { value = 0 }, "empty(#value)"));
        Assert.True(SE.ExecuteExpression(new { values = new[] { 1, 2, 3 } }, "eq(len(#values), 3)"));
        Assert.True(SE.ExecuteExpression(new { value = new { length = 4 } }, "eq(len(#value), 4)"));
    }

    [Fact]
    public void DottedReferencePrefersTheLiteralKeyThenTraversesSafely()
    {
        var model = new Dictionary<string, object?>
        {
            ["value"] = new Dictionary<string, object?> { ["child"] = "nested" },
            ["value.child"] = "direct",
        };

        Assert.True(SE.ExecuteExpression(model, "eq(#value.child, \"direct\")"));
        Assert.True(SE.ExecuteExpression(new { value = (object?)null }, "empty(#value.child)"));
        Assert.True(SE.ExecuteExpression(null, "empty(#value)"));
    }

    [Theory]
    [InlineData("#")]
    [InlineData("#.value")]
    [InlineData("#value.")]
    [InlineData("#value..child")]
    [InlineData("#__proto__")]
    [InlineData("#value.constructor")]
    [InlineData("#value.prototype.name")]
    [InlineData("not()")]
    [InlineData("eq(1,)")]
    [InlineData("and(true)")]
    [InlineData("if(true, 1, 2, 3)")]
    [InlineData("eq(1, 2) trailing")]
    public void CurrentParserRejectsInvalidInput(string expression)
    {
        Assert.ThrowsAny<ExpressionParseException>(() => SE.ParseExpression(expression));
    }

    [Fact]
    public void RegexIsCaseSensitiveAndSupportsDynamicPatterns()
    {
        Assert.False(SE.ExecuteExpression(new { value = "Hello" }, "match(#value, \"^hello$\")"));
        var expression = SE.ParseExpression("match(#value, #pattern)");
        Assert.True((bool)expression(new { value = "hello", pattern = "^hello$" })!);
        Assert.ThrowsAny<ArgumentException>(() => SE.ParseExpression("match(\"value\", \"[\")"));
    }

    [Fact]
    public void CacheLimitAndDisableCacheFollowTheUpstreamContract()
    {
        SE.SetCacheLimit(1);
        var first = SE.Get("true");
        SE.Get("false");
        Assert.NotSame(first, SE.Get("true"));

        SE.DisableCaches();
        Assert.NotSame(SE.Get("true"), SE.Get("true"));
    }
}
