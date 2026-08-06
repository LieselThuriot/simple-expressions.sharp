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

    public static ExpressionValue Missing => new(ExpressionValueKind.Missing);
    public static ExpressionValue Null => new(ExpressionValueKind.Null);
    public static ExpressionValue From(bool value) => new(ExpressionValueKind.Boolean, value);
    public static ExpressionValue From(double value) => new(ExpressionValueKind.Number, value);
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

    public double ToNumber() => Kind switch
    {
        ExpressionValueKind.Null => 0,
        ExpressionValueKind.Boolean => (bool)Value! ? 1 : 0,
        ExpressionValueKind.Number => (double)Value!,
        ExpressionValueKind.String => double.TryParse((string)Value!, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN,
        _ => double.NaN,
    };

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
        if (double.IsNaN(value)) return "NaN";
        if (double.IsPositiveInfinity(value)) return "Infinity";
        if (double.IsNegativeInfinity(value)) return "-Infinity";
        if (value == 0) return "0";

        var roundTrip = value.ToString("R", CultureInfo.InvariantCulture);
        var exponentIndex = roundTrip.IndexOf('E');
        if (exponentIndex < 0)
            return roundTrip;

        var mantissa = roundTrip.Substring(0, exponentIndex);
        var exponent = int.Parse(roundTrip.Substring(exponentIndex + 1), CultureInfo.InvariantCulture);

        var negative = mantissa[0] == '-';
        if (negative)
            mantissa = mantissa.Substring(1);

        var pointIndex = mantissa.IndexOf('.');
        var digits = pointIndex < 0 ? mantissa : mantissa.Remove(pointIndex, 1);
        // Decimal exponent of the leading digit, e.g. 1.5E+3 -> 3, 1.5E-7 -> -7.
        var leadingExponent = exponent + (pointIndex < 0 ? mantissa.Length : pointIndex) - 1;

        var text = leadingExponent >= PlainNotationMinExponent && leadingExponent < PlainNotationMaxExponent
            ? ToPlainNotation(digits, leadingExponent)
            : ToExponentNotation(digits, leadingExponent);

        return negative ? "-" + text : text;
    }

    private static string ToPlainNotation(string digits, int leadingExponent)
    {
        if (leadingExponent < 0)
            return "0." + new string('0', -leadingExponent - 1) + digits;
        if (leadingExponent >= digits.Length - 1)
            return digits + new string('0', leadingExponent - digits.Length + 1);
        return digits.Substring(0, leadingExponent + 1) + "." + digits.Substring(leadingExponent + 1);
    }

    private static string ToExponentNotation(string digits, int leadingExponent)
    {
        var mantissa = digits.Length == 1 ? digits : digits.Substring(0, 1) + "." + digits.Substring(1);
        var sign = leadingExponent < 0 ? "-" : "+";
        return mantissa + "e" + sign + Math.Abs(leadingExponent).ToString(CultureInfo.InvariantCulture);
    }
}
