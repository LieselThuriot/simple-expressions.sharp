using SE = Simple.Expressions.Sharp.SimpleExpressions;

namespace Simple.Expressions.Sharp.Tests;

public class SimpleExpressionsTests
{
    private readonly JsonObject _emptyModel = [];

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SimpleBooleansResultInThemselves(bool value)
    {
        Assert.Equal(value, SE.ExecuteExpression(_emptyModel, value));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SimpleStringBooleansResultInThemselves(bool value)
    {
        Assert.Equal(value, SE.ExecuteExpression(_emptyModel, value.ToString()));
    }

    [Fact]
    public void AndNotEqLtFor2ResultsInTrue()
    {
        var model = new JsonObject { ["2"] = 2 };
        Assert.True(SE.ExecuteExpression(model, "and( not(eq(#2, 5)), lt(#2, 10) )"));
    }

    [Fact]
    public void AndNotEqLtFor12ResultsInFalse()
    {
        var model = new JsonObject { ["2"] = 12 };
        Assert.False(SE.ExecuteExpression(model, "and( not(eq(#2, 5)), lt(#2, 10) )"));
    }

    [Fact]
    public void TrueForEmptyObjectResultsInTrue()
    {
        Assert.True(SE.ExecuteExpression(_emptyModel, true));
    }

    [Fact]
    public void FalseForEmptyObjectResultsInFalse()
    {
        Assert.False(SE.ExecuteExpression(_emptyModel, false));
    }

    [Fact]
    public void OrTrueFalseForEmptyObjectResultsInTrue()
    {
        Assert.True(SE.ExecuteExpression(_emptyModel, "or (true, false )"));
    }

    [Fact]
    public void AndTrueFalseForEmptyObjectResultsInFalse()
    {
        Assert.False(SE.ExecuteExpression(_emptyModel, "AND (true, false )"));
    }

    [Fact]
    public void AndTrue0ForEmptyObjectResultsInFalse()
    {
        Assert.False(SE.ExecuteExpression(_emptyModel, "AND (true, 0 )"));
    }

    [Fact]
    public void AndTrueBlubForEmptyObjectResultsInTrue()
    {
        Assert.True(SE.ExecuteExpression(_emptyModel, "AND (true, \"blub\" )"));
    }

    [Fact]
    public void AndTrueNotEmptyTextForTextTestResultsInTrue()
    {
        var model = new JsonObject { ["text"] = "test" };
        Assert.True(SE.ExecuteExpression(model, "AND (true, not( empty( #text )) )"));
    }

    [Fact]
    public void AndTrueNotEmptyTextForTextEmptyResultsInFalse()
    {
        var model = new JsonObject { ["text"] = "" };
        Assert.False(SE.ExecuteExpression(model, "AND (true, not( empty( #text )) )"));
    }

    [Fact]
    public void AndTrueNotEmptyTextForEmptyObjectResultsInFalse()
    {
        Assert.False(SE.ExecuteExpression(_emptyModel, "AND (true, not( empty( #text )) )"));
    }

    [Fact]
    public void AndTrueNotEmptyTextInnerTextForTextInnerTextTestResultsInTrue()
    {
        var model = new JsonObject { ["text"] = new JsonObject { ["innerText"] = "test" } };
        Assert.True(SE.ExecuteExpression(model, "AND (true, not( empty( #text.innerText )) )"));
    }

    [Fact]
    public void AndTrueEmptyTextInnerTextForTextInnerTextTestResultsInFalse()
    {
        var model = new JsonObject { ["text"] = new JsonObject { ["innerText"] = "test" } };
        Assert.False(SE.ExecuteExpression(model, "AND (true, empty( #text.innerText ) )"));
    }

    [Fact]
    public void TooManyQuotesThrows()
    {
        Assert.Throws<Exception>(() => SE.ExecuteExpression(_emptyModel, "eq(\"te\\\"\"st\", \"te\\\"\"st\")"));
    }

    [Theory]
    [InlineData("eq(\"test\", \"test\"\")")]
    [InlineData("eq(\"test\", \"tes\"t\")")]
    [InlineData("eq(\"te\"st\", \"test\")")]
    [InlineData("eq(\"test\"\", \"test\")")]
    [InlineData("eq(\"te\"\"st\", \"test\")")]
    [InlineData("eq(\"te\"\"st\", \"test\"\"\"\"\"d\")")]
    [InlineData("eq(\",\"\",\" \",\")")]
    [InlineData("eq(\"test\", \"test\\\")")]
    [InlineData("eq(\"test\\\", \"test\")")]
    [InlineData("eq(123, \"test\\\" \" \" )")]
    [InlineData("eq(\"test\\\" \" \", 123 )")]
    public void InvalidConstantLocationThrows(string expression)
    {
        Assert.Throws<Exception>(() => SE.ExecuteExpression(_emptyModel, expression));
    }

    [Theory]
    [InlineData("eq(\"test\", \"test\", \"test\")")]
    [InlineData("not(\"test\", \"test\")")]
    public void InvalidParametersThrows(string expression)
    {
        Assert.Throws<Exception>(() => SE.ExecuteExpression(_emptyModel, expression));
    }

    [Theory]
    [InlineData("""AND(eq("te\"st", "te\"st"), not(eq("te,st", "tes,t")))""", true)]
    [InlineData("""eq("test", "test\", \"test")""", false)]
    [InlineData("""eq("test\", \"test", "test\", \"test")""", true)]
    [InlineData("""eq("test", "test', 'test")""", false)]
    [InlineData("""eq("test', 'test", "test', 'test")""", true)]
    [InlineData("""not(eq("tru\"e", "tru\"e"))""", false)]
    [InlineData("""eq(",", ",")""", true)]
    [InlineData("""eq("\",\"", "\",\"")""", true)]
    [InlineData("""eq("','", "','")""", true)]
    [InlineData("""eq(123, "test\"" )""", false)]
    public void ConstantBoundariesAreRespected(string expression, bool expected)
    {
        Assert.Equal(expected, SE.ExecuteExpression(_emptyModel, expression));
    }

    [Fact]
    public void CanMatchRegex()
    {
        Assert.True(SE.ExecuteExpression(new JsonObject { ["test"] = "why hello there" }, "match(#test, \"hello\")"));
        Assert.False(SE.ExecuteExpression(new JsonObject { ["test"] = "why hello there" }, "match(#test, \"^hello\")"));
        Assert.False(SE.ExecuteExpression(new JsonObject { ["test"] = "why hello there" }, "match(#test, \"hello$\")"));
        Assert.True(SE.ExecuteExpression(new JsonObject { ["test"] = "hello" }, "match(#test, \"^hello$\")"));
        Assert.True(SE.ExecuteExpression(new JsonObject { ["test"] = "hello" }, "match(#test, \"[a-z]+\")"));
        Assert.False(SE.ExecuteExpression(new JsonObject { ["test"] = "12345" }, "match(#test, \"[a-z]+\")"));
        Assert.False(SE.ExecuteExpression(new JsonObject { ["test"] = "" }, "match(#test, \"[a-z]+\")"));
        Assert.True(SE.ExecuteExpression(new JsonObject { ["test"] = "" }, "match(#test, \".{0}\")"));
        Assert.False(SE.ExecuteExpression(_emptyModel, "match(#test, \".+\")"));
        Assert.False(SE.ExecuteExpression(new JsonObject { ["test"] = "123" }, "match(#test, \"\")"));
        Assert.True(SE.ExecuteExpression(new JsonObject { ["mail"] = "test@test.com" }, "match(#mail, \"^[^@]+@[^@]+\\.[^@]+$\")"));
        Assert.True(SE.ExecuteExpression(new JsonObject { ["mail"] = "test@test.com" }, "match(#mail, \"^[^@]+@[^@]+\\.[^@]+$\")"));
        Assert.False(SE.ExecuteExpression(new JsonObject { ["mail"] = "test@test" }, "match(#mail, '^[^@]+@[^@]+\\.[^@]+$')"));
        Assert.True(SE.ExecuteExpression(new JsonObject { ["mail"] = "test@test.com" }, "match(#mail, '^[^@]+@[^@]+\\.[^@]+$')"));
        Assert.True(SE.ExecuteExpression(new JsonObject { ["mail"] = "test@test.com" }, "AND(NOT(EMPTY(#mail)), MATCH(#mail, \"^test@test.com$\"))"));
        Assert.True(SE.ExecuteExpression(new JsonObject { ["form"] = new JsonObject { ["mail"] = "test@test.com" } }, "AND(NOT(EMPTY(#form.mail)), MATCH(#form.mail, \"^test@test.com$\"))"));
    }

    [Fact]
    public void CanMatchTwoRegexes()
    {
        Assert.True(SE.ExecuteExpression(new JsonObject { ["test"] = "why hello there" }, "and(match(#test, \"hello\"), match(#test, \"there\"))"));
    }

    [Fact]
    public void CanDoComplexThings()
    {
        Assert.True(SE.ExecuteExpression(new JsonObject { ["test"] = "why hello there" }, "and(not(empty(#test)), and(match(#test, \"hello\"), match(#test, \"there\")))"));
    }

    [Fact]
    public void Concatenation()
    {
        // These should probably throw since they're not boolean operators.
        // For now we are ok with them returning a boolean in the end result.
        Assert.True(SE.ExecuteExpression(_emptyModel, "concat(\"test\", \"123\")"));
        Assert.False(SE.ExecuteExpression(_emptyModel, "concat(#1, #2)"));
        Assert.True(SE.ExecuteExpression(new JsonObject { ["1"] = "123" }, "concat(#1, #2)"));
    }

    [Fact]
    public void ConcatenationAllowsFancyRegexes()
    {
        Assert.True(SE.ExecuteExpression(new JsonObject { ["pattern"] = "hello there", ["test"] = "hello there" }, "match(#test, concat(concat(\"^\", #pattern), \"$\"))"));
        Assert.True(SE.ExecuteExpression(new JsonObject { ["pattern1"] = "hello", ["pattern2"] = "there", ["test"] = "hello there" }, "and( match(#test, concat(\"^\", #pattern1)), match(#test, concat(#pattern2, \"$\")) )"));
        Assert.False(SE.ExecuteExpression(new JsonObject { ["pattern1"] = "hello", ["pattern2"] = "there", ["test"] = "hello there" }, "match(#test, concat(concat(concat(concat(\"(\", #pattern1), \")(?! \"), #pattern2), \")\"))"));
        Assert.True(SE.ExecuteExpression(new JsonObject { ["pattern1"] = "hello", ["pattern2"] = "there", ["test"] = "hello over there" }, "match(#test, concat(concat(concat(concat(\"(\", #pattern1), \")(?! \"), #pattern2), \")\"))"));
    }

    [Theory]
    [InlineData("hello", "lt(len(#test), 10)", true)]
    [InlineData("hello", "gt(len(#test), 10)", false)]
    [InlineData("hello", "eq(len(#test), 5)", true)]
    [InlineData("hello", "len(#test)", true)]
    [InlineData("", "len(#test)", false)]
    [InlineData("", "eq(len(#test), 0)", true)]
    public void CanTestInputLengths(string testValue, string expression, bool expected)
    {
        var model = new JsonObject { ["test"] = testValue };
        Assert.Equal(expected, SE.ExecuteExpression(model, expression));
    }

    [Theory]
    [InlineData("len(#test)", false)]
    [InlineData("eq(len(#test), 0)", true)]
    [InlineData("eq(len(\"test\"), 2)", false)]
    [InlineData("eq(len(\"test\"), 4)", true)]
    public void CanTestInputLengthsOnEmpty(string expression, bool expected)
    {
        Assert.Equal(expected, SE.ExecuteExpression(_emptyModel, expression));
    }
}