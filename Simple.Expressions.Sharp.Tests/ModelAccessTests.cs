using System.Collections;
using System.Globalization;
using System.Text.Json.Serialization;
using SE = Simple.Expressions.SimpleExpressions;

namespace Simple.Expressions.Tests;

/// <summary>
/// Covers reading models by reflection. None of this was reachable while models were flattened
/// through a JSON serializer first, because the serializer normalised every shape and value
/// before the evaluator saw it.
/// </summary>
public class ModelAccessTests
{
    private sealed class MatchEvaluationModel(List<string> reads, string pattern)
    {
        public string Input { get { reads.Add("input"); return "value"; } }
        public string Pattern { get { reads.Add("pattern"); return pattern; } }
    }

    public ModelAccessTests()
    {
        SE.EnableCaches();
        SE.SetCacheLimit(1000);
        SE.Clear();
    }

    private enum Status
    {
        Unknown = 0,
        Active = 1,
        Archived = 7,
    }

    private sealed class Person
    {
        public string Name { get; set; } = "Ada";
        public string? Nickname { get; set; }
        public int Score { get; set; } = 8;
        public Person? Manager { get; set; }
        public static string Computed => "computed";

        public static string Shared => "static";
    }

    private readonly struct Point(int x, int y)
    {
        public int X { get; } = x;
        public int Y { get; } = y;
    }

    // --- shapes -------------------------------------------------------------------------

    [Fact]
    public void ReadsPublicInstancePropertiesOfAClass()
    {
        var model = new Person();
        Assert.True(SE.ExecuteExpression(model, "eq(#Name, \"Ada\")"));
        Assert.True(SE.ExecuteExpression(model, "eq(#Score, 8)"));
    }

    private sealed class Annotated
    {
        [JsonPropertyName("renamed")]
        public string Value { get; set; } = "kept";

        [JsonIgnore]
        public string Hidden { get; set; } = "still readable";
    }

    [Fact]
    public void JsonAttributesAreNotHonoured()
    {
        // Models are no longer routed through a serializer, so serializer metadata has no say
        // in what a member is called or whether it is visible. This is a deliberate break with
        // the previous behaviour and is called out in the README.
        var model = new Annotated();

        Assert.True(SE.ExecuteExpression(model, "eq(#Value, \"kept\")"));
        Assert.False(SE.ExecuteExpression(model, "defined(#renamed)"));
        Assert.True(SE.ExecuteExpression(model, "eq(#Hidden, \"still readable\")"));
    }

    [Fact]
    public void DoesNotReadPrivateOrStaticMembers()
    {
        var model = new Person();
        Assert.False(SE.ExecuteExpression(model, "defined(#Secret)"));
        Assert.False(SE.ExecuteExpression(model, "defined(#Shared)"));
    }

    [Fact]
    public void PropertyNamesAreOrdinalAndCaseSensitive()
    {
        var model = new Person();
        Assert.True(SE.ExecuteExpression(model, "defined(#Name)"));
        Assert.False(SE.ExecuteExpression(model, "defined(#name)"));
        Assert.False(SE.ExecuteExpression(model, "defined(#NAME)"));
    }

    [Fact]
    public void ReadsStructModelsWithoutBoxingProblems()
    {
        var model = new Point(3, 4);
        Assert.True(SE.ExecuteExpression(model, "and(eq(#X, 3), eq(#Y, 4))"));
    }

    [Fact]
    public void TraversesNestedObjectsAndStopsAtNull()
    {
        var model = new Person { Manager = new Person { Name = "Grace" } };
        Assert.True(SE.ExecuteExpression(model, "eq(#Manager.Name, \"Grace\")"));

        // Manager.Manager is null, so anything below it is missing rather than an error.
        Assert.True(SE.ExecuteExpression(model, "empty(#Manager.Manager.Name)"));
        Assert.False(SE.ExecuteExpression(model, "defined(#Manager.Manager.Name)"));
    }

    [Fact]
    public void AbsentAndNullPropertiesAreBothNullish()
    {
        var model = new Person { Nickname = null };
        Assert.False(SE.ExecuteExpression(model, "defined(#Nickname)"));
        Assert.False(SE.ExecuteExpression(model, "defined(#NotAProperty)"));
        Assert.True(SE.ExecuteExpression(model, "empty(#Nickname)"));
        Assert.True(SE.ExecuteExpression(model, "eq(#Nickname, #NotAProperty)"));
        Assert.Null(SE.ParseExpression("#Nickname")(model));
    }

    // --- dictionaries -------------------------------------------------------------------

    [Fact]
    public void ReadsGenericDictionariesWithNonObjectValues()
    {
        var model = new Dictionary<string, string> { ["role"] = "engineer" };
        Assert.True(SE.ExecuteExpression(model, "eq(#role, \"engineer\")"));
        Assert.False(SE.ExecuteExpression(model, "defined(#missing)"));
    }

    [Fact]
    public void ReadsReadOnlyDictionariesAsKeyedModels()
    {
        IReadOnlyDictionary<string, object?> model = new Dictionary<string, object?>
        {
            ["role"] = "engineer",
        };

        Assert.True(SE.ExecuteExpression(model, "eq(#role, \"engineer\")"));
        Assert.False(SE.ExecuteExpression(model, "defined(#missing)"));
    }

    [Fact]
    public void ReadsNonGenericDictionaries()
    {
        var model = new Hashtable { ["role"] = "engineer", ["score"] = 8 };
        Assert.True(SE.ExecuteExpression(model, "eq(#role, \"engineer\")"));
        Assert.True(SE.ExecuteExpression(model, "gt(#score, 5)"));
    }

    [Fact]
    public void NestedDictionariesTraverse()
    {
        var model = new Dictionary<string, object?>
        {
            ["user"] = new Dictionary<string, object?> { ["address"] = new Dictionary<string, object?> { ["city"] = "Brussels" } },
        };

        Assert.True(SE.ExecuteExpression(model, "eq(#user.address.city, \"Brussels\")"));
    }

    [Fact]
    public void ADictionaryIsAnObjectNotAnArray()
    {
        var model = new Dictionary<string, object?>
        {
            ["bag"] = new Dictionary<string, object?> { ["length"] = 4 },
        };

        // Reached via the own `length` member, which only an object has.
        Assert.True(SE.ExecuteExpression(model, "eq(len(#bag), 4)"));
    }

    // --- collections --------------------------------------------------------------------

    [Fact]
    public void ArraysAndListsBehaveAsArrays()
    {
        var arrayModel = new { values = new[] { 1, 2, 3 } };
        var listModel = new { values = new List<int> { 1, 2, 3 } };

        Assert.True(SE.ExecuteExpression(arrayModel, "eq(len(#values), 3)"));
        Assert.True(SE.ExecuteExpression(listModel, "eq(len(#values), 3)"));
        Assert.True(SE.ExecuteExpression(listModel, "in(2, #values)"));
        Assert.False(SE.ExecuteExpression(listModel, "in(9, #values)"));
    }

    [Fact]
    public void LazySequencesAreEnumeratedOnDemand()
    {
        var model = new { values = Enumerable.Range(1, 5).Select(value => value * 2) };
        Assert.True(SE.ExecuteExpression(model, "in(4, #values)"));
        Assert.True(SE.ExecuteExpression(model, "eq(len(#values), 5)"));
    }

    [Fact]
    public void OwnLengthUsesTheOrdinalMemberName()
    {
        Assert.True(SE.ExecuteExpression(new { value = new { length = 4 } }, "eq(len(#value), 4)"));

        // `Length` is a different member; upstream only ever reads `length`.
        Assert.Throws<ExpressionEvaluationException>(
            () => SE.ExecuteExpression(new { value = new { Length = 4 } }, "eq(len(#value), 4)"));
    }

    // --- value coercion -----------------------------------------------------------------

    [Fact]
    public void EnumsBecomeTheirNumericValue()
    {
        Assert.Equal(1d, SE.ParseExpression("#status")(new { status = Status.Active }));
        Assert.Equal(7d, SE.ParseExpression("#status")(new { status = Status.Archived }));
        Assert.True(SE.ExecuteExpression(new { status = Status.Archived }, "eq(#status, 7)"));
    }

    [Fact]
    public void DatesBecomeRoundTripStrings()
    {
        var moment = new DateTime(2026, 8, 6, 13, 45, 0, DateTimeKind.Utc);
        Assert.Equal(moment.ToString("O", CultureInfo.InvariantCulture), SE.ParseExpression("#when")(new { when = moment }));

        var offset = new DateTimeOffset(moment);
        Assert.Equal(offset.ToString("O", CultureInfo.InvariantCulture), SE.ParseExpression("#when")(new { when = offset }));
    }

    [Fact]
    public void GuidsBytesAndUrisBecomeStrings()
    {
        var identifier = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");
        Assert.Equal(identifier.ToString("D"), SE.ParseExpression("#id")(new { id = identifier }));
        Assert.Equal("AQID", SE.ParseExpression("#data")(new { data = new byte[] { 1, 2, 3 } }));
        Assert.Equal("https://example.com/a", SE.ParseExpression("#link")(new { link = new Uri("https://example.com/a") }));
    }

    [Theory]
    [InlineData((byte)7)]
    [InlineData((sbyte)7)]
    [InlineData((short)7)]
    [InlineData((ushort)7)]
    [InlineData(7)]
    [InlineData(7u)]
    [InlineData(7L)]
    [InlineData(7ul)]
    [InlineData(7f)]
    [InlineData(7d)]
    public void EveryNumericPrimitiveWidensToANumber(object value)
    {
        Assert.Equal(7d, SE.ParseExpression("#value")(new { value }));
    }

    [Fact]
    public void DecimalsAndCharsCoerce()
    {
        Assert.Equal(7d, SE.ParseExpression("#value")(new { value = 7.0m }));
        Assert.Equal("x", SE.ParseExpression("#value")(new { value = 'x' }));
    }

    // --- live reads ---------------------------------------------------------------------

    [Fact]
    public void ModelsAreReadLiveRatherThanSnapshotted()
    {
        var model = new Person { Name = "Ada", Score = 8 };

        Assert.True(SE.ExecuteExpression(model, "eq(#Name, \"Ada\")"));
        Assert.True(SE.ExecuteExpression(model, "gt(#Score, 5)"));

        model.Name = "Grace";
        model.Score = 2;

        Assert.False(SE.ExecuteExpression(model, "eq(#Name, \"Ada\")"));
        Assert.True(SE.ExecuteExpression(model, "eq(#Name, \"Grace\")"));
        Assert.False(SE.ExecuteExpression(model, "gt(#Score, 5)"));
    }

    [Fact]
    public void ReusedEvaluatorsSeeEachModelTheyAreGiven()
    {
        var evaluator = SE.ParseExpression("concat(#Name, \"!\")");

        Assert.Equal("Ada!", evaluator(new Person { Name = "Ada" }));
        Assert.Equal("Grace!", evaluator(new Person { Name = "Grace" }));
    }

    [Fact]
    public void DynamicMatchEvaluatesInputBeforePattern()
    {
        var reads = new List<string>();
        Assert.False(SE.ExecuteExpression(new MatchEvaluationModel(reads, string.Empty), "match(#Input, #Pattern)"));
        Assert.Equal(["input", "pattern"], reads);
    }

    private static readonly int[] model = [1, 2, 3];

    // --- roots --------------------------------------------------------------------------

    [Fact]
    public void NonObjectRootsResolveEveryReferenceToMissing()
    {
        Assert.True(SE.ExecuteExpression(null, "empty(#value)"));
        Assert.False(SE.ExecuteExpression(42, "defined(#value)"));
        Assert.False(SE.ExecuteExpression("text", "defined(#value)"));
        Assert.False(SE.ExecuteExpression(model, "defined(#value)"));
    }
}
