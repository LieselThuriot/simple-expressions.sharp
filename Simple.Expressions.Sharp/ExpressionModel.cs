#nullable enable

using System.Collections;
using System.Globalization;

namespace Simple.Expressions;

/// <summary>
/// Wraps the caller's model. Nothing is copied or converted up front: references are resolved
/// against the live object when an expression asks for them, so the cost of an evaluation is
/// proportional to what the expression reads rather than to the size of the model.
/// </summary>
internal sealed class ExpressionModel
{
    private readonly object? _root;

    private ExpressionModel(object? root)
    {
        _root = root;
    }

    public static ExpressionModel From(object? model) => new(model);

    /// <summary>Resolves a reference with no dots in it.</summary>
    public ExpressionValue ResolveSingle(string name)
    {
        if (_root is null)
            return ExpressionValue.Missing;

        return ModelAccessor.TryGetMember(_root, name, out var value) ? Convert(value) : ExpressionValue.Missing;
    }

    /// <summary>
    /// Resolves a dotted reference. A member whose name is literally the dotted path wins over
    /// traversal, matching upstream.
    /// </summary>
    public ExpressionValue Resolve(string path, string[] segments)
    {
        if (_root is null)
            return ExpressionValue.Missing;

        if (ModelAccessor.TryGetMember(_root, path, out var dotted))
            return Convert(dotted);

        var value = _root;
        foreach (var segment in segments)
        {
            if (value is null || !ModelAccessor.TryGetMember(value, segment, out value))
                return ExpressionValue.Missing;
        }

        return Convert(value);
    }

    public static bool TryGetOwnLength(ExpressionValue value, out ExpressionValue length)
    {
        if (value.Kind == ExpressionValueKind.Array)
        {
            length = ExpressionValue.From((double)Count(value.Value!));
            return true;
        }

        // Upstream reads an own `length` member; on a model object that is an ordinary member
        // lookup, so `new { length = 4 }` has a length and `new { Length = 4 }` does not.
        if (value.Kind == ExpressionValueKind.Object && ModelAccessor.TryGetMember(value.Value!, "length", out var member))
        {
            length = Convert(member);
            return true;
        }

        length = default;
        return false;
    }

    public static bool TryGetArrayValues(ExpressionValue value, out IEnumerable<ExpressionValue> values)
    {
        if (value.Kind != ExpressionValueKind.Array)
        {
            values = Enumerable.Empty<ExpressionValue>();
            return false;
        }

        // List literals already hold converted values.
        if (value.Value is IReadOnlyList<ExpressionValue> expressionValues)
        {
            values = expressionValues;
            return true;
        }

        // Deferred, so in() and structural comparison can stop early.
        values = ConvertItems((IEnumerable)value.Value!);
        return true;
    }

    public static bool TryGetObjectEntries(ExpressionValue value, out IEnumerable<KeyValuePair<string, ExpressionValue>> entries)
    {
        if (value.Kind != ExpressionValueKind.Object)
        {
            entries = Enumerable.Empty<KeyValuePair<string, ExpressionValue>>();
            return false;
        }

        entries = ConvertEntries(value.Value!);
        return true;
    }

    public static string ArrayToString(object value)
    {
        if (value is IReadOnlyList<ExpressionValue> expressionValues)
            return string.Join(",", expressionValues.Select(ItemToString));

        return string.Join(",", ConvertItems((IEnumerable)value).Select(ItemToString));
    }

    private static string ItemToString(ExpressionValue item)
        => item.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null ? string.Empty : item.ToJavaScriptString();

    private static IEnumerable<ExpressionValue> ConvertItems(IEnumerable source)
    {
        foreach (var item in source)
            yield return Convert(item);
    }

    private static IEnumerable<KeyValuePair<string, ExpressionValue>> ConvertEntries(object source)
    {
        foreach (var entry in ModelAccessor.GetEntries(source))
            yield return new KeyValuePair<string, ExpressionValue>(entry.Key, Convert(entry.Value));
    }

    private static int Count(object value)
    {
        if (value is ICollection collection)
            return collection.Count;

        var count = 0;
        foreach (var _ in (IEnumerable)value)
            count++;
        return count;
    }

    /// <summary>
    /// Maps a CLR value onto the JavaScript value model the evaluator works in. The coercions
    /// for enums, dates, GUIDs and byte arrays deliberately match what the JSON serializer
    /// produced before, so expressions comparing against ISO date strings or enum numbers keep
    /// working.
    /// </summary>
    private static ExpressionValue Convert(object? value) => value switch
    {
        null => ExpressionValue.Null,
        bool boolean => ExpressionValue.From(boolean),
        string text => ExpressionValue.From(text),
        double number => ExpressionValue.From(number),
        int number => ExpressionValue.From(number),
        long number => ExpressionValue.From(number),
        decimal number => ExpressionValue.From((double)number),
        float number => ExpressionValue.From(number),
        short number => ExpressionValue.From(number),
        byte number => ExpressionValue.From(number),
        sbyte number => ExpressionValue.From(number),
        ushort number => ExpressionValue.From(number),
        uint number => ExpressionValue.From(number),
        ulong number => ExpressionValue.From(number),
        char character => ExpressionValue.From(character.ToString()),
        Enum enumeration => ExpressionValue.From(System.Convert.ToDouble(enumeration, CultureInfo.InvariantCulture)),
        DateTime date => ExpressionValue.From(date.ToString("O", CultureInfo.InvariantCulture)),
        DateTimeOffset date => ExpressionValue.From(date.ToString("O", CultureInfo.InvariantCulture)),
        Guid identifier => ExpressionValue.From(identifier.ToString("D", CultureInfo.InvariantCulture)),
        byte[] bytes => ExpressionValue.From(System.Convert.ToBase64String(bytes)),
        Uri uri => ExpressionValue.From(uri.OriginalString),
        // A dictionary is an object, not a sequence of its entries.
        IEnumerable sequence when !ModelAccessor.IsDictionary(value) => ExpressionValue.FromArray(sequence),
        _ => ExpressionValue.FromObject(value),
    };
}
