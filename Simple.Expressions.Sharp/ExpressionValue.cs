#nullable enable

using System.Globalization;

namespace Simple.Expressions;

internal enum ExpressionValueKind
{
    Missing,
    Null,
    Boolean,
    Number,
    String,
    Array,
    Object,
}

internal readonly struct ExpressionValue
{
    private ExpressionValue(ExpressionValueKind kind, object? value = null)
    {
        Kind = kind;
        Value = value;
    }

    public ExpressionValueKind Kind { get; }
    public object? Value { get; }

    // Every logical and comparison operator produces a boolean, and 0/1 are the numbers that
    // show up most; pre-boxing them keeps those intermediates allocation-free.
    private static readonly object s_boxedTrue = true;
    private static readonly object s_boxedFalse = false;
    private static readonly object s_boxedZero = 0d;
    private static readonly object s_boxedOne = 1d;

    public static ExpressionValue Missing => new(ExpressionValueKind.Missing);
    public static ExpressionValue Null => new(ExpressionValueKind.Null);
    public static ExpressionValue From(bool value) => new(ExpressionValueKind.Boolean, value ? s_boxedTrue : s_boxedFalse);

    public static ExpressionValue From(double value)
    {
        object boxed = value;
        if (value == 0 && BitConverter.DoubleToInt64Bits(value) >= 0)
        {
            boxed = s_boxedZero;
        }
        else if (value == 1)
        {
            boxed = s_boxedOne;
        }

        return new ExpressionValue(ExpressionValueKind.Number, boxed);
    }
    public static ExpressionValue From(string value) => new(ExpressionValueKind.String, value);
    public static ExpressionValue FromArray(object value) => new(ExpressionValueKind.Array, value);
    public static ExpressionValue FromArray(IReadOnlyList<ExpressionValue> value) => new(ExpressionValueKind.Array, value);
    public static ExpressionValue FromObject(object value) => new(ExpressionValueKind.Object, value);

    public bool IsTruthy() => Kind switch
    {
        ExpressionValueKind.Missing or ExpressionValueKind.Null => false,
        ExpressionValueKind.Boolean => (bool)Value!,
        ExpressionValueKind.Number => (double)Value! != 0 && !double.IsNaN((double)Value!),
        ExpressionValueKind.String => ((string)Value!).Length != 0,
        _ => true,
    };

    public double ToNumber(bool strict = false)
    {
        return Kind switch
        {
            ExpressionValueKind.Null => 0,
            ExpressionValueKind.Boolean => (bool)Value! ? 1 : 0,
            ExpressionValueKind.Number => (double)Value!,
            ExpressionValueKind.String => StringToNumber(Value, strict),
            _ => double.NaN,
        };

        static double StringToNumber(object? value, bool strict)
        {
            if (string.IsNullOrWhiteSpace((string)value!))
            {
                if (strict)
                {
                    throw new InvalidOperationException("Invalid numeric value");
                }

                return 0;
            }

            if (double.TryParse((string)value!, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
            {
                if (strict && double.IsNaN(number))
                {
                    throw new InvalidOperationException("Invalid numeric value");
                }

                return number;
            }

            if (strict)
            {
                throw new InvalidOperationException("Invalid numeric value");
            }

            return double.NaN;
        }
    }

    public string ToJavaScriptString() => Kind switch
    {
        ExpressionValueKind.Missing => "undefined",
        ExpressionValueKind.Null => "null",
        ExpressionValueKind.Boolean => (bool)Value! ? "true" : "false",
        ExpressionValueKind.Number => JavaScriptNumberFormatter.Format((double)Value!),
        ExpressionValueKind.String => (string)Value!,
        ExpressionValueKind.Array => ClrValueAdapter.ArrayToString(Value!),
        _ => "[object Object]",
    };

    public object? ToPublicValue() => Kind switch
    {
        ExpressionValueKind.Missing or ExpressionValueKind.Null => null,
        ExpressionValueKind.Boolean or ExpressionValueKind.Number or ExpressionValueKind.String => Value,
        ExpressionValueKind.Array when Value is IReadOnlyList<ExpressionValue> values => values.Select(value => value.ToPublicValue()).ToArray(),
        _ => Value,
    };

}
