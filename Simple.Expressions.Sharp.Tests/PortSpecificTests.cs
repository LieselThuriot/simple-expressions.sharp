using SE = Simple.Expressions.SimpleExpressions;

namespace Simple.Expressions.Tests;

public sealed class PortSpecificTests
{
    [Fact]
    public void StructuralEqualityIsSupportedForPortModels()
    {
        Assert.True(SE.ExecuteExpression(new { a = new[] { 1, 2 }, b = new[] { 1, 2 } }, "eq(#a, #b)"));
        Assert.True(SE.ExecuteExpression(new { a = new object[] { 1, new[] { 2, 3 } }, b = new object[] { 1, new[] { 2, 3 } } }, "eq(#a, #b)"));
        Assert.True(SE.ExecuteExpression(new { a = new { x = 1 }, b = new { x = 1 } }, "eq(#a, #b)"));
    }

    [Fact]
    public void EmptyAndBlankStringsDoNotConvertToZero()
    {
        Assert.False(SE.ExecuteExpression(new { value = "" }, "eq(#value, 0)"));
        Assert.False(SE.ExecuteExpression(new { value = "   " }, "eq(#value, 0)"));
    }
}
