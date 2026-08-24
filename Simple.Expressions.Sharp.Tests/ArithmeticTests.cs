using System.Collections;
using SE = Simple.Expressions.SimpleExpressions;

namespace Simple.Expressions.Tests;

public sealed class ArithmeticTests
{
    private static readonly int[] s_values = [1, 2, 3];

    [Theory]
    [InlineData("add([1, [2, 3]])", 6d)]
    [InlineData("sub([10, [2, 3]])", 5d)]
    [InlineData("mul([2, [3, 4]])", 24d)]
    [InlineData("div([24, [2, 3]])", 4d)]
    [InlineData("mod([20, [6, 3]])", 2d)]
    [InlineData("min([3, [1, 2]])", 1d)]
    [InlineData("max([1, [3, 2]])", 3d)]
    public void NumericOperatorsDepthFirstFlattenOneRuntimeList(string expression, double expected)
    {
        Assert.Equal(expected, SE.ParseExpression(expression)(null));
    }

    [Theory]
    [InlineData("add(#values)", 6d)]
    [InlineData("sub(#values)", -4d)]
    [InlineData("mul(#values)", 6d)]
    [InlineData("div(#values)", 1d / 6d)]
    [InlineData("mod(#values)", 1d)]
    [InlineData("min(#values)", 1d)]
    [InlineData("max(#values)", 3d)]
    public void NumericOperatorsAcceptOneRuntimeList(string expression, double expected)
    {
        Assert.Equal(expected, SE.ParseExpression(expression)(new { values = s_values }));
    }

    [Fact]
    public void NumericOperatorLeafCountsUseFlattenedValues()
    {
        Assert.Equal(0d, SE.ParseExpression("add([])")(null));
        Assert.Equal(1d, SE.ParseExpression("mul([[], []])")(null));
        Assert.Equal("Sub requires at least two values", Assert.Throws<InvalidOperationException>(() => SE.ParseExpression("sub([1, []])")(null)).Message);
        Assert.Equal("Div requires at least two values", Assert.Throws<InvalidOperationException>(() => SE.ParseExpression("div([1])")(null)).Message);
        Assert.Equal("Mod requires at least two values", Assert.Throws<InvalidOperationException>(() => SE.ParseExpression("mod([[]])")(null)).Message);
        Assert.Throws<InvalidOperationException>(() => SE.ParseExpression("min([[]])")(null));
        Assert.Throws<InvalidOperationException>(() => SE.ParseExpression("max([])")(null));
    }

    [Theory]
    [InlineData("add(1)", "operator add expects at least two arguments")]
    [InlineData("sub(1)", "operator sub expects at least two arguments")]
    [InlineData("mul(1)", "operator mul expects at least two arguments")]
    [InlineData("div(1)", "operator div expects at least two arguments")]
    [InlineData("mod(1)", "operator mod expects at least two arguments")]
    [InlineData("min(1)", "operator min expects at least two arguments")]
    [InlineData("max(1)", "operator max expects at least two arguments")]
    public void SingleNumericArgumentMustEvaluateToAList(string expression, string message)
    {
        Assert.Equal(message, Assert.Throws<InvalidOperationException>(() => SE.ParseExpression(expression)(null)).Message);
    }

    [Fact]
    public void NumericOperatorsRejectNonNumericOrNonfiniteLeaves()
    {
        object?[] invalid = [true, null, new object(), "", " ", "invalid", double.NaN, double.PositiveInfinity];
        foreach (object? value in invalid)
        {
            var exception = Assert.Throws<InvalidOperationException>(() => SE.ParseExpression("add([1, #value])")(new { value }));
            Assert.Equal("Invalid numeric value", exception.Message);
        }

        Assert.Throws<InvalidOperationException>(() => SE.ParseExpression("add([1, #missing])")(new { }));
    }

    [Fact]
    public void NumericOperatorOutputsMayBeNonfinite()
    {
        Assert.Equal(double.PositiveInfinity, SE.ParseExpression("mul([1e308, 1e308])")(null));
        Assert.True(double.IsNaN(Assert.IsType<double>(SE.ParseExpression("sqrt(-1)")(null))));

        Assert.Equal("Invalid numeric value", Assert.Throws<InvalidOperationException>(() => SE.ParseExpression("abs(mul(1e308, 1e308))")(null)).Message);
        Assert.Equal("Invalid numeric value", Assert.Throws<InvalidOperationException>(() => SE.ParseExpression("add([sqrt(-1)])")(null)).Message);
    }

    [Fact]
    public void ActiveEnumerableCyclesAreRejectedButRepeatedListsAreAllowed()
    {
        var cycle = new ArrayList();
        cycle.Add(1);
        cycle.Add(cycle);
        var shared = new[] { 2, 3 };

        Assert.Throws<InvalidOperationException>(() => SE.ParseExpression("add(#value, 1)")(new { value = cycle }));
        Assert.Equal(10d, SE.ParseExpression("add([#value, #value])")(new { value = shared }));
    }
}
