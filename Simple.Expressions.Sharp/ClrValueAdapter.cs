#nullable enable

using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Simple.Expressions;

internal static class ClrValueAdapter
{
    public static ExpressionValue From(object? value) => value switch
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
        IEnumerable sequence when !ModelAccessor.IsDictionary(value) => ExpressionValue.FromArray(sequence),
        _ => ExpressionValue.FromObject(value),
    };

    public static bool TryGetOwnLength(ExpressionValue value, out ExpressionValue length)
    {
        if (value.Kind == ExpressionValueKind.Array)
        {
            length = ExpressionValue.From((double)Count(value.Value!));
            return true;
        }

        if (value.Kind == ExpressionValueKind.Object && ModelAccessor.TryGetMember(value.Value!, "length", out object? member))
        {
            length = From(member);
            return true;
        }

        length = default;
        return false;
    }

    public static bool TryGetArrayValues(ExpressionValue value, out IEnumerable<ExpressionValue> values)
    {
        if (value.Kind != ExpressionValueKind.Array)
        {
            values = [];
            return false;
        }

        if (value.Value is IReadOnlyList<ExpressionValue> expressionValues)
        {
            values = expressionValues;
            return true;
        }

        values = ConvertItems((IEnumerable)value.Value!);
        return true;
    }

    public static bool TryGetObjectEntries(ExpressionValue value, out IEnumerable<KeyValuePair<string, ExpressionValue>> entries)
    {
        if (value.Kind != ExpressionValueKind.Object)
        {
            entries = [];
            return false;
        }

        entries = ConvertEntries(value.Value!);
        return true;
    }

    public static string ArrayToString(object value)
        => ArrayToString(value, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static string ArrayToString(object value, HashSet<object> activeArrays)
    {
        if (!activeArrays.Add(value))
        {
            throw new ExpressionEvaluationException("Cyclic list value");
        }

        try
        {
            IEnumerable<ExpressionValue> values = value is IReadOnlyList<ExpressionValue> expressionValues
                ? expressionValues
                : ConvertItems((IEnumerable)value);
            return string.Join(",", values.Select(item => ItemToString(item, activeArrays)));
        }
        finally
        {
            activeArrays.Remove(value);
        }
    }

    private static string ItemToString(ExpressionValue item, HashSet<object> activeArrays)
        => item.Kind switch
        {
            ExpressionValueKind.Missing or ExpressionValueKind.Null => string.Empty,
            ExpressionValueKind.Array => ArrayToString(item.Value!, activeArrays),
            _ => item.ToText(),
        };

    private static IEnumerable<ExpressionValue> ConvertItems(IEnumerable source)
    {
        foreach (object? item in source)
        {
            yield return From(item);
        }
    }

    private static IEnumerable<KeyValuePair<string, ExpressionValue>> ConvertEntries(object source)
    {
        foreach (var entry in ModelAccessor.GetEntries(source))
        {
            yield return new KeyValuePair<string, ExpressionValue>(entry.Key, From(entry.Value));
        }
    }

    private static int Count(object value)
    {
        if (value is ICollection collection)
        {
            return collection.Count;
        }

        int count = 0;
        foreach (object? _ in (IEnumerable)value)
        {
            count++;
        }

        return count;
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static ReferenceEqualityComparer Instance { get; } = new();
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
        public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
    }
}