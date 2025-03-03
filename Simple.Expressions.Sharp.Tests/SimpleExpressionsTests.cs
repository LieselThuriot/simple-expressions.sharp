using SE = Simple.Expressions.Sharp.SimpleExpressions;

namespace Simple.Expressions.Sharp.Tests;

public class SimpleExpressionsTests
{
    private readonly JsonObject _emptyModel = [];

    [Fact]
    public void SimpleTrueResultsInTrue()
    {
        Assert.True(SE.ExecuteExpression(_emptyModel, true));
    }

    [Fact]
    public void SimpleFalseResultsInFalse()
    {
        Assert.False(SE.ExecuteExpression(_emptyModel, false));
    }

    [Fact]
    public void TrueResultsInTrue()
    {
        Assert.True(SE.ExecuteExpression(_emptyModel, "true"));
    }

    [Fact]
    public void FalseResultsInFalse()
    {
        Assert.False(SE.ExecuteExpression(_emptyModel, "false"));
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

    [Fact]
    public void InvalidConstantLocationThrows()
    {
        Assert.Throws<Exception>(() => SE.ExecuteExpression(_emptyModel, "eq(\"test\", \"test\"\")"));
        Assert.Throws<Exception>(() => SE.ExecuteExpression(_emptyModel, "eq(\"test\", \"tes\"t\")"));
        Assert.Throws<Exception>(() => SE.ExecuteExpression(_emptyModel, "eq(\"te\"st\", \"test\")"));
        Assert.Throws<Exception>(() => SE.ExecuteExpression(_emptyModel, "eq(\"test\"\", \"test\")"));
        Assert.Throws<Exception>(() => SE.ExecuteExpression(_emptyModel, "eq(\"te\"\"st\", \"test\")"));
        Assert.Throws<Exception>(() => SE.ExecuteExpression(_emptyModel, "eq(\"te\"\"st\", \"test\"\"\"\"\"d\")"));
        Assert.Throws<Exception>(() => SE.ExecuteExpression(_emptyModel, "eq(\",\"\",\" \",\")"));
        Assert.Throws<Exception>(() => SE.ExecuteExpression(_emptyModel, "eq(\"test\", \"test\\\")"));
        Assert.Throws<Exception>(() => SE.ExecuteExpression(_emptyModel, "eq(\"test\\\", \"test\")"));
        Assert.Throws<Exception>(() => SE.ExecuteExpression(_emptyModel, "eq(123, \"test\\\" \" \" )"));
        Assert.Throws<Exception>(() => SE.ExecuteExpression(_emptyModel, "eq(\"test\\\" \" \", 123 )"));
    }

    [Fact]
    public void InvalidParametersThrows()
    {
        Assert.Throws<Exception>(() => SE.ExecuteExpression(_emptyModel, "eq(\"test\", \"test\", \"test\")"));
        Assert.Throws<Exception>(() => SE.ExecuteExpression(_emptyModel, "not(\"test\", \"test\")"));
    }

    [Fact]
    public void ConstantBoundariesAreRespected()
    {
        Assert.True(SE.ExecuteExpression(_emptyModel, """AND(eq("te\"st", "te\"st"), not(eq("te,st", "tes,t")))"""));
        Assert.False(SE.ExecuteExpression(_emptyModel, """eq("test", "test\", \"test")"""));
        Assert.True(SE.ExecuteExpression(_emptyModel, """eq("test\", \"test", "test\", \"test")"""));
        Assert.False(SE.ExecuteExpression(_emptyModel, """eq("test", "test', 'test")"""));
        Assert.True(SE.ExecuteExpression(_emptyModel, """eq("test', 'test", "test', 'test")"""));
        Assert.False(SE.ExecuteExpression(_emptyModel, """not(eq("tru\"e", "tru\"e"))"""));
        Assert.True(SE.ExecuteExpression(_emptyModel, """eq(",", ",")"""));
        Assert.True(SE.ExecuteExpression(_emptyModel, """eq("\",\"", "\",\"")"""));
        Assert.True(SE.ExecuteExpression(_emptyModel, """eq("','", "','")"""));
        Assert.False(SE.ExecuteExpression(_emptyModel, """eq(123, "test\"" )"""));
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

    [Fact]
    public void CanTestInputLengths()
    {
        Assert.True(SE.ExecuteExpression(new JsonObject { ["test"] = "hello" }, "lt(len(#test), 10)"));
        Assert.False(SE.ExecuteExpression(new JsonObject { ["test"] = "hello" }, "gt(len(#test), 10)"));
        Assert.True(SE.ExecuteExpression(new JsonObject { ["test"] = "hello" }, "eq(len(#test), 5)"));
        Assert.True(SE.ExecuteExpression(new JsonObject { ["test"] = "hello" }, "len(#test)"));
        Assert.False(SE.ExecuteExpression(new JsonObject { ["test"] = "" }, "len(#test)"));
        Assert.True(SE.ExecuteExpression(new JsonObject { ["test"] = "" }, "eq(len(#test), 0)"));
        Assert.False(SE.ExecuteExpression(_emptyModel, "len(#test)"));
        Assert.True(SE.ExecuteExpression(_emptyModel, "eq(len(#test), 0)"));
        Assert.False(SE.ExecuteExpression(_emptyModel, "eq(len(\"test\"), 2)"));
        Assert.True(SE.ExecuteExpression(_emptyModel, "eq(len(\"test\"), 4)"));
    }
}