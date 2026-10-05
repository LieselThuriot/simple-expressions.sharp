using System.Globalization;
using System.Text.RegularExpressions;

namespace Simple.Expressions;

internal static class ExpressionText
{
    private static readonly Regex s_decimalPattern = new(
        @"^[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsWhiteSpace(char value)
        => value is (>= '\u0009' and <= '\u000D')
            or '\u0020' or '\u0085' or '\u00A0' or '\u1680'
            or (>= '\u2000' and <= '\u200A')
            or '\u2028' or '\u2029' or '\u202F' or '\u205F' or '\u3000';

    public static string Trim(string value)
    {
        int start = 0;
        int end = value.Length;
        while (start < end && IsWhiteSpace(value[start]))
        {
            start++;
        }

        while (end > start && IsWhiteSpace(value[end - 1]))
        {
            end--;
        }

        return start == 0 && end == value.Length ? value : value.Substring(start, end - start);
    }

    public static bool IsDecimalLiteral(string value) => s_decimalPattern.IsMatch(value);

    public static bool TryParseFiniteDecimal(string value, out double number)
    {
        string text = Trim(value);
        if (text.Length != 0
            && IsDecimalLiteral(text)
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
        {
            return !double.IsNaN(number) && !double.IsInfinity(number);
        }

        number = 0;
        return false;
    }
}
