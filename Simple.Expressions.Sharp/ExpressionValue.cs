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

    public static ExpressionValue From(double value) => new(ExpressionValueKind.Number, value switch
    {
        0 => s_boxedZero,
        1 => s_boxedOne,
        _ => value,
    });
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

    public double ToNumber()
    {
        return Kind switch
        {
            ExpressionValueKind.Null => 0,
            ExpressionValueKind.Boolean => (bool)Value! ? 1 : 0,
            ExpressionValueKind.Number => (double)Value!,
            ExpressionValueKind.String => StringToNumber(Value),
            _ => double.NaN,
        };

        static double StringToNumber(object? value)
        {
            if (string.IsNullOrWhiteSpace((string)value!))
            {
                return 0;
            }

            if (double.TryParse((string)value!, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
            {
                return number;
            }

            return double.NaN;
        }
    }

    public string ToJavaScriptString() => Kind switch
    {
        ExpressionValueKind.Missing => "undefined",
        ExpressionValueKind.Null => "null",
        ExpressionValueKind.Boolean => (bool)Value! ? "true" : "false",
        ExpressionValueKind.Number => FormatNumber((double)Value!),
        ExpressionValueKind.String => (string)Value!,
        ExpressionValueKind.Array => ExpressionModel.ArrayToString(Value!),
        _ => "[object Object]",
    };

    public object? ToPublicValue() => Kind switch
    {
        ExpressionValueKind.Missing or ExpressionValueKind.Null => null,
        ExpressionValueKind.Boolean or ExpressionValueKind.Number or ExpressionValueKind.String => Value,
        ExpressionValueKind.Array when Value is IReadOnlyList<ExpressionValue> values => values.Select(value => value.ToPublicValue()).ToArray(),
        _ => Value,
    };

    private const int PlainNotationMinExponent = -6;
    private const int PlainNotationMaxExponent = 21;

    /// <summary>
    /// Renders a double as its shortest round-trip decimal, using plain notation for
    /// magnitudes in <c>[1e-6, 1e21)</c> and lowercase exponent notation outside that range.
    /// </summary>
    private static string FormatNumber(double value)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }

        if (double.IsPositiveInfinity(value))
        {
            return "Infinity";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "-Infinity";
        }

        if (value == 0)
        {
            return "0";
        }

        string roundTrip = value.ToString("R", CultureInfo.InvariantCulture);
        int exponentIndex = roundTrip.IndexOf('E');
        if (exponentIndex < 0)
        {
            return roundTrip;
        }

        string mantissa = roundTrip.Substring(0, exponentIndex);
        int exponent = int.Parse(roundTrip.Substring(exponentIndex + 1), CultureInfo.InvariantCulture);

        bool negative = mantissa[0] == '-';
        if (negative)
        {
            mantissa = mantissa.Substring(1);
        }

        int pointIndex = mantissa.IndexOf('.');
        string digits = pointIndex < 0 ? mantissa : mantissa.Remove(pointIndex, 1);
        // Decimal exponent of the leading digit, e.g. 1.5E+3 -> 3, 1.5E-7 -> -7.
        int leadingExponent = exponent + (pointIndex < 0 ? mantissa.Length : pointIndex) - 1;

        string text = leadingExponent >= PlainNotationMinExponent && leadingExponent < PlainNotationMaxExponent
            ? ToPlainNotation(digits, leadingExponent)
            : ToExponentNotation(digits, leadingExponent);

        return negative ? "-" + text : text;
    }

    private static string ToPlainNotation(string digits, int leadingExponent)
    {
        if (leadingExponent < 0)
        {
            return "0." + new string('0', -leadingExponent - 1) + digits;
        }

        if (leadingExponent >= digits.Length - 1)
        {
            return digits + new string('0', leadingExponent - digits.Length + 1);
        }

        return digits.Substring(0, leadingExponent + 1) + "." + digits.Substring(leadingExponent + 1);
    }

    private static string ToExponentNotation(string digits, int leadingExponent)
    {
        string mantissa = digits.Length == 1 ? digits : digits.Substring(0, 1) + "." + digits.Substring(1);
        string sign = leadingExponent < 0 ? "-" : "+";
        return mantissa + "e" + sign + Math.Abs(leadingExponent).ToString(CultureInfo.InvariantCulture);
    }
}