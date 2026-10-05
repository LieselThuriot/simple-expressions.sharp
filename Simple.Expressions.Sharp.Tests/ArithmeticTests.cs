using System.Collections;
using SE = Simple.Expressions.SimpleExpressions;

namespace Simple.Expressions.Sharp.Tests;

public sealed class ArithmeticTests
{
    private static readonly int[] s_values = [1, 2, 3];

    private sealed class NumericEvaluationModel
    {
        public required object Invalid { get; init; }
        public object Later => throw new InvalidOperationException(Invalid.ToString());
    }

    private sealed class ThrowingEnumerable : IEnumerable
    {
        public IEnumerator GetEnumerator() => throw new InvalidOperationException("later list flattened");
    }

    [Theory]
    [InlineData("add([1, [2, 3]])", 6d)]
    [InlineData("sub([10, [2, 3]])", 5d)]
    [InlineData("mul([2, [3, 4]])", 24d)]
    [InlineData("div([24, [2, 3]])", 4d)]
    [InlineData("mod([20, [6, 3]])", 2d)]
    [InlineData("min([3, [1, 2]])", 1d)]
    [InlineData("max([1, [3, 2]])", 3d)]
    public void NumericOperatorsDepthFirstFlattenOneRuntimeList(string expression, double expected) => Assert.Equal(expected, SE.ParseExpression(expression)(null));

    [Theory]
    [InlineData("add(#values)", 6d)]
    [InlineData("sub(#values)", -4d)]
    [InlineData("mul(#values)", 6d)]
    [InlineData("div(#values)", 1d / 6d)]
    [InlineData("mod(#values)", 1d)]
    [InlineData("min(#values)", 1d)]
    [InlineData("max(#values)", 3d)]
    public void NumericOperatorsAcceptOneRuntimeList(string expression, double expected) => Assert.Equal(expected, SE.ParseExpression(expression)(new { values = s_values }));

    [Fact]
    public void NumericOperatorLeafCountsUseFlattenedValues()
    {
        Assert.Equal(0d, SE.ParseExpression("add([])")(null));
        Assert.Equal(1d, SE.ParseExpression("mul([[], []])")(null));
        Assert.Equal("Sub requires at least two values", Assert.Throws<ExpressionEvaluationException>(() => SE.ParseExpression("sub([1, []])")(null)).Message);
        Assert.Equal("Div requires at least two values", Assert.Throws<ExpressionEvaluationException>(() => SE.ParseExpression("div([1])")(null)).Message);
        Assert.Equal("Mod requires at least two values", Assert.Throws<ExpressionEvaluationException>(() => SE.ParseExpression("mod([[]])")(null)).Message);
        Assert.Throws<ExpressionEvaluationException>(() => SE.ParseExpression("min([[]])")(null));
        Assert.Throws<ExpressionEvaluationException>(() => SE.ParseExpression("max([])")(null));
    }

    [Theory]
    [InlineData("add(1)", "Invalid Expression: operator add expects at least two arguments or one list argument")]
    [InlineData("sub(1)", "Invalid Expression: operator sub expects at least two arguments or one list argument")]
    [InlineData("mul(1)", "Invalid Expression: operator mul expects at least two arguments or one list argument")]
    [InlineData("div(1)", "Invalid Expression: operator div expects at least two arguments or one list argument")]
    [InlineData("mod(1)", "Invalid Expression: operator mod expects at least two arguments or one list argument")]
    [InlineData("min(1)", "Invalid Expression: operator min expects at least two arguments or one list argument")]
    [InlineData("max(1)", "Invalid Expression: operator max expects at least two arguments or one list argument")]
    public void SingleNumericArgumentMustEvaluateToAList(string expression, string message) => Assert.Equal(message, Assert.Throws<ExpressionEvaluationException>(() => SE.ParseExpression(expression)(null)).Message);

    [Fact]
    public void NumericOperatorsRejectNonNumericOrNonfiniteLeaves()
    {
        object?[] invalid = [true, null, new object(), "", " ", "invalid", double.NaN, double.PositiveInfinity];
        foreach (object? value in invalid)
        {
            ExpressionEvaluationException exception = Assert.Throws<ExpressionEvaluationException>(() => SE.ParseExpression("add([1, #value])")(new { value }));
            Assert.Equal("Invalid numeric value", exception.Message);
        }

        Assert.Throws<ExpressionEvaluationException>(() => SE.ParseExpression("add([1, #missing])")(new { }));
    }

    [Theory]
    [InlineData(".5", 0.5d)]
    [InlineData("5.", 5d)]
    [InlineData("-2.5e+2", -250d)]
    [InlineData("\u00851.5\u3000", 1.5d)]
    public void NumericStringsUseThePortableDecimalGrammar(string value, double expected)
    {
        var model = new { value };
        Assert.Equal(expected, SE.ParseExpression("num(#value)")(model));
        Assert.Equal(expected + 1, SE.ParseExpression("add(#value, 1)")(model));
        Assert.True(SE.ExecuteExpression(model, "eq(#value, " + expected.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")"));
    }

    [Theory]
    [InlineData("0x10")]
    [InlineData("0b10")]
    [InlineData("0o10")]
    [InlineData("1e309")]
    [InlineData("\uFEFF1\uFEFF")]
    public void NumericStringsRejectNonDecimalOrNonfiniteForms(string value)
    {
        Assert.Equal("Invalid numeric value", Assert.Throws<ExpressionEvaluationException>(
            () => SE.ParseExpression("num(#value)")(new { value })).Message);
    }

    [Fact]
    public void NumericLiteralsMustBeFinite()
    {
        Assert.Contains("Invalid numeric value", Assert.Throws<ExpressionParseException>(
            () => SE.ParseExpression("1e309")).Message);
    }

    [Theory]
    [InlineData("round(-0.4, 0)")]
    [InlineData("mul(-1, 0)")]
    [InlineData("ceil(-0.5)")]
    public void NumericResultsNormalizeNegativeZero(string expression)
    {
        double result = Assert.IsType<double>(SE.ParseExpression(expression)(null));
        Assert.Equal(0d, result);
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(result));
    }

    [Fact]
    public void NumericOperatorOutputsMustBeFinite()
    {
        string[] invalidResults =
        [
            "sqrt(-1)",
            "pow(1e308, 2)",
            "add(1e308, 1e308)",
            "sub(-1e308, 1e308)",
            "mul(1e308, 1e308)",
            "div(1e308, 1e-308)",
        ];
        foreach (string expression in invalidResults)
        {
            Assert.Equal("Invalid numeric result", Assert.Throws<ExpressionEvaluationException>(
                () => SE.ParseExpression(expression)(null)).Message);
        }

        Assert.Equal("Division by zero", Assert.Throws<ExpressionEvaluationException>(
            () => SE.ParseExpression("div(1e308, 1e-308, 0)")(null)).Message);
    }

    [Theory]
    [InlineData("round(2.5, 0)", 3d)]
    [InlineData("round(-2.5, 0)", -3d)]
    [InlineData("round(1.25, 1)", 1.3d)]
    [InlineData("round(-1.25, 1)", -1.3d)]
    [InlineData("round(7.164692098702749e-215, 230)", 7.164692098702749e-215)]
    [InlineData("round(4.898568327955125e-190, 204)", 4.89856832795512e-190)]
    [InlineData("round(6.663776354903525e-38, 52)", 6.66377635490352e-38)]
    [InlineData("round(1e308, 308)", 1e308)]
    [InlineData("round(5e-324, 308)", 0d)]
    public void RoundUsesBoundedPrecisionAndTiesAwayFromZero(string expression, double expected) => Assert.Equal(expected, SE.ParseExpression(expression)(null));

    [Fact]
    public void RoundRejectsPrecisionAbove308()
    {
        Assert.Equal("Round precision is too large", Assert.Throws<ExpressionEvaluationException>(
            () => SE.ParseExpression("round(1, 309)")(null)).Message);
    }

    [Fact]
    public void VariadicNumericOperatorsEvaluateAndFlattenBeforeConversion()
    {
        Assert.Equal("later argument evaluated", Assert.Throws<InvalidOperationException>(
            () => SE.ParseExpression("add(#Invalid, #Later)")(new NumericEvaluationModel { Invalid = "later argument evaluated" })).Message);

        object?[] values = ["invalid", new ThrowingEnumerable()];
        Assert.Equal("later list flattened", Assert.Throws<InvalidOperationException>(
            () => SE.ParseExpression("add(#values)")(new { values })).Message);
    }

    [Fact]
    public void ActiveEnumerableCyclesAreRejectedButRepeatedListsAreAllowed()
    {
        var cycle = new ArrayList
        {
            1
        };
        cycle.Add(cycle);
        int[] shared = [2, 3];

        Assert.Throws<ExpressionEvaluationException>(() => SE.ParseExpression("add(#value, 1)")(new { value = cycle }));
        Assert.Equal(10d, SE.ParseExpression("add([#value, #value])")(new { value = shared }));
    }
}