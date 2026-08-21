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
    public void StringBackslashesRemainLiteral()
    {
        Assert.Equal("a\\\"b", SE.ParseExpression("\"a\\\"b\"")(new { }));
        Assert.True(SE.ExecuteExpression(new { }, "eq(\"a\\\"b\", \"a\\\"b\")"));
    }
}