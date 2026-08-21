using SE = Simple.Expressions.SimpleExpressions;

namespace Simple.Expressions.Tests;

public sealed class PortSpecificTests
{
    private static readonly int[] model = [1, 2];
    private static readonly int[] modelArray = [2, 3];

    [Fact]
    public void StructuralEqualityIsSupportedForPortModels()
    {
        Assert.True(SE.ExecuteExpression(new { a = model, b = model }, "eq(#a, #b)"));
        Assert.True(SE.ExecuteExpression(new { a = new object[] { 1, modelArray }, b = new object[] { 1, modelArray } }, "eq(#a, #b)"));
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
    public void SimpleExpressionConstructorsArePublic()
    {
        Assert.True(new Simple.Expressions.SimpleExpression(true).Evaluate(new { }));
        Assert.Equal("ok", new Simple.Expressions.SimpleExpression("\"ok\"").EvaluateValue(new { }));
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
        Assert.Throws<InvalidOperationException>(() => SE.ParseExpression("num([1])")(new { }));
        Assert.Throws<InvalidOperationException>(() => SE.ParseExpression("num(\"NaN\")")(new { }));
    }

    [Fact]
    public void TrimUsesEcmaScriptWhitespace()
    {
        Assert.Equal("value", SE.ParseExpression("trim(#value)")(new { value = "\uFEFFvalue\u3000" }));
        Assert.Equal("\u0085value\u0085", SE.ParseExpression("trim(#value)")(new { value = "\u0085value\u0085" }));
    }

    [Fact]
    public void RawValuesPreserveNegativeZero()
    {
        double value = Assert.IsType<double>(SE.ParseExpression("#value")(new { value = -0d }));
        Assert.True(BitConverter.DoubleToInt64Bits(value) < 0);
        Assert.Equal(string.Empty, SE.ParseExpression("concat(#value, \"\")")(new { value = -0d }));
    }
}
