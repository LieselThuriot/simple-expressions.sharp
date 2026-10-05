using System.Globalization;

namespace Simple.Expressions;

internal static class ExpressionNumberFormatter
{
    private const int PlainNotationMinExponent = -6;
    private const int PlainNotationMaxExponent = 21;

    public static string Format(double value)
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
        int leadingExponent = exponent + (pointIndex < 0 ? mantissa.Length : pointIndex) - 1;
        string text = leadingExponent is >= PlainNotationMinExponent and < PlainNotationMaxExponent
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