#nullable enable

using System.Globalization;
using System.Numerics;

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
        double number = Kind switch
        {
            ExpressionValueKind.Null => 0,
            ExpressionValueKind.Boolean => (bool)Value! ? 1 : 0,
            ExpressionValueKind.Number => (double)Value!,
            ExpressionValueKind.String => StringToNumber(Value, strict),
            _ => double.NaN,
        };

        if (strict && (Kind is not (ExpressionValueKind.Number or ExpressionValueKind.String) || double.IsNaN(number) || double.IsInfinity(number)))
        {
            throw new InvalidOperationException("Invalid numeric value");
        }

        return number;

        static double StringToNumber(object? value, bool strict)
        {
            string text = ((string)value!).Trim();
            if (text.Length == 0)
            {
                if (strict)
                {
                    throw new InvalidOperationException("Invalid numeric value");
                }

                return 0;
            }

            if (TryParseJavaScriptNumber(text, out double number))
            {
                return number;
            }

            if (strict)
            {
                throw new InvalidOperationException("Invalid numeric value");
            }

            return double.NaN;
        }

        static bool TryParseJavaScriptNumber(string text, out double number)
        {
            if (text.Length > 2 && text[0] == '0')
            {
                int radix = text[1] switch
                {
                    'x' or 'X' => 16,
                    'b' or 'B' => 2,
                    'o' or 'O' => 8,
                    _ => 0,
                };
                if (radix != 0)
                {
                    try
                    {
                        string digits = text.Substring(2);
                        BigInteger integer;
                        if (radix == 16)
                        {
                            if (!BigInteger.TryParse("0" + digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out integer))
                            {
                                number = 0;
                                return false;
                            }
                        }
                        else
                        {
                            integer = BigInteger.Zero;
                            foreach (char digit in digits)
                            {
                                int value = digit - '0';
                                if (value < 0 || value >= radix)
                                {
                                    number = 0;
                                    return false;
                                }

                                integer = (integer * radix) + value;
                            }
                        }

                        number = (double)integer;
                        return true;
                    }
                    catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
                    {
                        number = 0;
                        return false;
                    }
                }
            }

            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
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
