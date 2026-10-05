using SE = Simple.Expressions.SimpleExpressions;

namespace Simple.Expressions.Sharp.Tests;

public sealed class PortSpecificTests
{
    private static readonly int[] s_model = [1, 2];
    private static readonly int[] s_modelArray = [2, 3];

    [Fact]
    public void StructuralEqualityIsSupportedForPortModels()
    {
        Assert.True(SE.ExecuteExpression(new { a = s_model, b = s_model }, "eq(#a, #b)"));
        Assert.True(SE.ExecuteExpression(new { a = new object[] { 1, s_modelArray }, b = new object[] { 1, s_modelArray } }, "eq(#a, #b)"));
        Assert.True(SE.ExecuteExpression(new { a = new { x = 1 }, b = new { x = 1 } }, "eq(#a, #b)"));
    }

    [Fact]
    public void StructuralEqualityHandlesCyclicObjects()
    {
        var left = new Dictionary<string, object?> { ["value"] = 1 };
        var right = new Dictionary<string, object?> { ["value"] = 1 };
        left["self"] = left;
        right["self"] = right;

        Assert.True(SE.ExecuteExpression(new { left, right }, "eq(#left, #right)"));
    }

    [Fact]
    public void EqualityUsesExplicitPortableConversionsAndShapes()
    {
        var model = new
        {
            list = new object[] { 1 },
            otherList = new object[] { 1 },
            obj = new { value = 1 },
            otherObject = new { value = 1 },
            nullValue = (object?)null,
        };

        Assert.True(SE.ExecuteExpression(model, "eq(#missing, #nullValue)"));
        Assert.True(SE.ExecuteExpression(model, "eq(1, \"1.\")"));
        Assert.True(SE.ExecuteExpression(model, "eq(true, \"1e0\")"));
        Assert.True(SE.ExecuteExpression(model, "eq(false, 0)"));
        Assert.True(SE.ExecuteExpression(model, "eq(#list, #otherList)"));
        Assert.True(SE.ExecuteExpression(model, "eq(#obj, #otherObject)"));
        Assert.False(SE.ExecuteExpression(model, "eq(false, \"\")"));
        Assert.False(SE.ExecuteExpression(model, "eq(16, \"0x10\")"));
        Assert.False(SE.ExecuteExpression(model, "eq(#list, \"1\")"));
        Assert.False(SE.ExecuteExpression(model, "eq(#obj, \"[object Object]\")"));
        Assert.False(SE.ExecuteExpression(model, "eq(#list, #obj)"));
    }

    [Fact]
    public void ComparisonsAreOrdinalForStringsAndNumericForMixedNumericPairs()
    {
        var model = new { list = new[] { 2 }, obj = new { value = 2 } };

        Assert.True(SE.ExecuteExpression(model, "lt(\"10\", \"2\")"));
        Assert.True(SE.ExecuteExpression(model, "gt(10, \"2\")"));
        Assert.True(SE.ExecuteExpression(model, "lt(\".5\", 1)"));
        Assert.False(SE.ExecuteExpression(model, "gt(1, \"0x0\")"));
        Assert.False(SE.ExecuteExpression(model, "lt(false, 1)"));
        Assert.False(SE.ExecuteExpression(model, "lt(\"\", \"a\")"));
        Assert.False(SE.ExecuteExpression(model, "lt(\"   \", \"a\")"));
        Assert.False(SE.ExecuteExpression(model, "gt(#missing, 0)"));
        Assert.False(SE.ExecuteExpression(model, "gt(#list, 1)"));
        Assert.False(SE.ExecuteExpression(model, "lt(#obj, \"z\")"));
    }

    [Fact]
    public void SimpleExpressionConstructorsArePublic()
    {
        Assert.True(new SimpleExpression(true).Evaluate(new { }));
        Assert.Equal("ok", new SimpleExpression("\"ok\"").EvaluateValue(new { }));
    }

    [Fact]
    public void StringBackslashesRemainLiteral()
    {
        Assert.Equal("a\\\"b", SE.ParseExpression("\"a\\\"b\"")(new { }));
        Assert.True(SE.ExecuteExpression(new { }, "eq(\"a\\\"b\", \"a\\\"b\")"));
    }

    [Fact]
    public void NumRejectsListsAndNaNStrings()
    {
        Assert.Throws<ExpressionEvaluationException>(() => SE.ParseExpression("num([1])")(new { }));
        Assert.Throws<ExpressionEvaluationException>(() => SE.ParseExpression("num(\"NaN\")")(new { }));
    }

    [Fact]
    public void WhitespaceUsesTheExplicitPortableSet()
    {
        char[] whitespace =
        [
            '\u0009', '\u000A', '\u000B', '\u000C', '\u000D', '\u0020', '\u0085', '\u00A0', '\u1680',
            '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005', '\u2006', '\u2007', '\u2008', '\u2009', '\u200A',
            '\u2028', '\u2029', '\u202F', '\u205F', '\u3000',
        ];
        foreach (char character in whitespace)
        {
            Assert.Equal("value", SE.ParseExpression("trim(#value)")(new { value = character + "value" + character }));
            Assert.Equal(1d, SE.ParseExpression("num(#value)")(new { value = character + "1" + character }));
        }

        Assert.Equal("value", SE.ParseExpression("trim(#value)")(new { value = "\u0085\u2000value\u3000" }));
        Assert.Equal("\uFEFFvalue\uFEFF", SE.ParseExpression("trim(#value)")(new { value = "\uFEFFvalue\uFEFF" }));
        Assert.True(SE.ExecuteExpression(null, "\u0085 and\u2000(true,\u3000true) \u2029"));
        Assert.Throws<ExpressionParseException>(() => SE.ParseExpression("\uFEFFtrue\uFEFF"));
    }

    [Fact]
    public void RawValuesNormalizeNegativeZero()
    {
        double value = Assert.IsType<double>(SE.ParseExpression("#value")(new { value = -0d }));
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(value));
        Assert.Equal(string.Empty, SE.ParseExpression("concat(#value, \"\")")(new { value = -0d }));

        object[] nested = [-0d];
        Assert.Same(nested, SE.ParseExpression("#value")(new { value = nested }));
        Assert.True(BitConverter.DoubleToInt64Bits((double)nested[0]) < 0);
    }

    [Fact]
    public void CyclicListTextConversionHasAControlledError()
    {
        var cycle = new System.Collections.ArrayList();
        cycle.Add(cycle);

        Assert.Equal("Cyclic list value", Assert.Throws<ExpressionEvaluationException>(
            () => SE.ParseExpression("concat(#value, \"\")")(new { value = cycle })).Message);
    }

    [Theory]
    [InlineData(0.1, "0.1")]
    [InlineData(0.3, "0.3")]
    [InlineData(5e-324, "5e-324")]
    [InlineData(1.7976931348623157e308, "1.7976931348623157e+308")]
    [InlineData(123456789012345680000.0, "123456789012345680000")]
    [InlineData(1e21, "1e+21")]
    [InlineData(0.000001, "0.000001")]
    [InlineData(1e-7, "1e-7")]
    [InlineData(-1.5, "-1.5")]
    public void NumberTextUsesShortestRoundTripDigits(double value, string expected) => Assert.Equal(expected, SE.ParseExpression("concat(#value, \"\")")(new { value })?.ToString());

    [Fact]
    public void ParseFailuresUseExpressionParseException()
    {
        foreach (string? expression in new[] { "1e309", "eq(1", "match(#a, \"(\")" })
        {
            Assert.Throws<ExpressionParseException>(() => SE.ParseExpression(expression));
        }

        Assert.Contains("three arguments", Assert.Throws<ExpressionParseException>(() => SE.ParseExpression("if(1)")).Message);
    }

    [Fact]
    public void RuntimeFailuresUseExpressionEvaluationException()
    {
        Assert.Throws<ExpressionEvaluationException>(() => SE.ParseExpression("num(\"x\")")(new { }));
        Assert.Throws<ExpressionEvaluationException>(() => SE.ParseExpression("div(1, 0)")(new { }));
        Assert.Throws<ExpressionEvaluationException>(() => SE.ParseExpression("match(\"a\", #p)")(new { p = "(" }));
        Assert.StartsWith("Invalid Expression: operator add", Assert.Throws<ExpressionEvaluationException>(
            () => SE.ParseExpression("add(#v)")(new { v = 5 })).Message);
    }

    [Fact]
    public void MatchEvaluatesInputBeforePatternValidity() => Assert.Throws<ExpressionEvaluationException>(() => SE.ParseExpression("match(num(\"x\"), #p)")(new { p = 1 }));

    [Fact]
    public void RegexUsesDocumentedDotNetSemantics()
    {
        Assert.True(SE.ExecuteExpression(new { v = "\u0663", p = @"^\d$" }, "match(#v, #p)"));
        Assert.True(SE.ExecuteExpression(new { v = "a\n", p = "a$" }, "match(#v, #p)"));
    }
}