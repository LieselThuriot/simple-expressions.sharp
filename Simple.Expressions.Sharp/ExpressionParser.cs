using System.Collections;
using System.Text.RegularExpressions;

namespace Simple.Expressions.Sharp;

internal static class ExpressionParser
{
    public static bool Falsy(this SimpleType value) => value.SwitchCaseOrDefault(
        static @bool => @bool,
        static @int => @int is not 0,
        static @string => !string.IsNullOrEmpty(@string)
    );

    private static bool Equals(SimpleType value1, SimpleType value2)
    {
        return value1 == value2;
    }

    private static bool GreaterThan(SimpleType value1, SimpleType value2) => value1.SwitchCase(
        bool1 => value2.SwitchCaseOrDefault(first: bool2 => (bool1 ? 1 : 0) > (bool2 ? 1 : 0)),
        int1 => value2.SwitchCaseOrDefault(second: int2 => int1 > int2),
        string1 => value2.SwitchCaseOrDefault(third: string2 => StringComparer.Ordinal.Compare(string1, string2) > 0)
    );

    private static bool LessThan(SimpleType value1, SimpleType value2) => value1.SwitchCase(
        bool1 => value2.SwitchCaseOrDefault(first: bool2 => (bool1 ? 1 : 0) < (bool2 ? 1 : 0)),
        int1 => value2.SwitchCaseOrDefault(second: int2 => int1 < int2),
        string1 => value2.SwitchCaseOrDefault(third: string2 => StringComparer.Ordinal.Compare(string1, string2) < 0)
    );

    private static bool Contains(SimpleType value1, SimpleType value2)
    {
        var stringValue1 = value1.ToString();
        var stringValue2 = value2.ToString();

        return stringValue1 is not null &&
               stringValue2 is not null &&
               stringValue1.IndexOf(stringValue2, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool Not(SimpleType value)
    {
        return !value.Falsy();
    }

    private static bool Or(SimpleType value1, SimpleType value2)
    {
        return value1.Falsy() || value2.Falsy();
    }

    private static bool And(SimpleType value1, SimpleType value2)
    {
        return value1.Falsy() && value2.Falsy();
    }

    private static bool Empty(SimpleType value)
    {
        return value.IsThird && string.IsNullOrEmpty(value.Third);
    }

    private static Func<JsonObject, SimpleType> ParseSingleBody(string value)
    {
        value = value.Trim();

        if (value.StartsWith("("))
        {
            value = value.Substring(1);
        }

        if (value.EndsWith(")"))
        {
            value = value.Substring(0, value.Length - 1);
        }

        return InnerParseExpression(value.Trim());
    }

    private static Func<JsonObject, SimpleType> ParseNot(string value)
    {
        var inner = ParseSingleBody(value);
        return model => Not(inner(model));
    }

    private static Func<JsonObject, SimpleType> ParseEmpty(string value)
    {
        var inner = ParseSingleBody(value);
        return model => Empty(inner(model));
    }

    private static (Func<JsonObject, SimpleType> left, Func<JsonObject, SimpleType> right) ResolveParameters(string value)
    {
        int bracketCount = 0;
        char? constantStart = null;
        bool canConstantStart = true;

        for (int i = 0; i < value.Length; i++)
        {
            switch (value[i])
            {
                case '(':
                    if (!constantStart.HasValue)
                    {
                        bracketCount++;
                    }

                    break;
                case ')':
                    if (!constantStart.HasValue)
                    {
                        bracketCount--;
                    }

                    break;
                case '`':
                case '"':
                case '\'':
                    if (!constantStart.HasValue)
                    {
                        if (canConstantStart)
                        {
                            constantStart = value[i];
                            canConstantStart = false;
                        }
                        else
                        {
                            throw new Exception("Invalid Expression: invalid constant start: " + value);
                        }
                    }
                    else if (constantStart.HasValue && constantStart.GetValueOrDefault() == value[i])
                    {
                        int backslashCount = 0;
                        int backslashIndex = i - 1;
                        while (backslashIndex >= 0 && value[backslashIndex] == '\\')
                        {
                            backslashCount++;
                            backslashIndex--;
                        }

                        if (backslashCount % 2 == 0)
                        {
                            constantStart = null;
                        }
                    }

                    break;
                case ',':
                    if (!constantStart.HasValue)
                    {
                        if (bracketCount == 1)
                        {
                            var leftBody = value.Substring(1, i - 1);
                            var left = InnerParseExpression(leftBody);

                            var rightBody = value.Substring(i + 1).Trim();
                            if (rightBody[rightBody.Length - 1] != ')')
                            {
                                throw new Exception("Invalid Expression: missing closing bracket: " + rightBody);
                            }

                            var right = InnerParseExpression(rightBody.Substring(0, rightBody.Length - 1));

                            return (left, right);
                        }

                        canConstantStart = true;
                    }

                    break;
            }
        }

        throw new Exception("Invalid Expression Part: " + value);
    }

    private static Func<JsonObject, SimpleType> ParseEquals(string value)
    {
        var (left, right) = ResolveParameters(value);
        return model => Equals(left(model), right(model));
    }

    private static Func<JsonObject, SimpleType> ParseOr(string value)
    {
        var (left, right) = ResolveParameters(value);
        return model => Or(left(model), right(model));
    }

    private static Func<JsonObject, SimpleType> ParseAnd(string value)
    {
        var (left, right) = ResolveParameters(value);
        return model => And(left(model), right(model));
    }

    private static Func<JsonObject, SimpleType> ParseContains(string value)
    {
        var (left, right) = ResolveParameters(value);
        return model => Contains(left(model), right(model));
    }

    private static Func<JsonObject, SimpleType> ParseGreaterThan(string value)
    {
        var (left, right) = ResolveParameters(value);
        return model => GreaterThan(left(model), right(model));
    }

    private static Func<JsonObject, SimpleType> ParseLessThan(string value)
    {
        var (left, right) = ResolveParameters(value);
        return model => LessThan(left(model), right(model));
    }

    private static Func<JsonObject, SimpleType> ParseRegex(string value)
    {
        var (left, right) = ResolveParameters(value);
        return model => EvaluateRegex(left(model), right(model));
    }

    private static Func<JsonObject, SimpleType> ParseConcat(string value)
    {
        var (left, right) = ResolveParameters(value);
        return model => Concat(left(model), right(model));
    }

    private static Func<JsonObject, SimpleType> ParseOperator(string operatorName, string body)
    {
        if (string.IsNullOrEmpty(operatorName))
        {
            throw new Exception("Invalid Expression: no operator");
        }

        return operatorName.ToLowerInvariant() switch
        {
            "not" => ParseNot(body),
            "eq" => ParseEquals(body),
            "or" => ParseOr(body),
            "and" => ParseAnd(body),
            "contains" => ParseContains(body),
            "gt" => ParseGreaterThan(body),
            "lt" => ParseLessThan(body),
            "empty" => ParseEmpty(body),
            "len" => ParseLength(body),
            "match" => ParseRegex(body),
            "concat" => ParseConcat(body),
            _ => throw new Exception("Invalid Operator: " + operatorName),
        };
    }

    private static readonly Regex OperatorRegex = new(@"^([a-zA-Z]+)\s*(\(.+?\))$", RegexOptions.Compiled);

    private static Func<JsonObject, SimpleType> Parse(string value)
    {
        if (value.Length > 1)
        {
            char firstChar = value[0];
            switch (firstChar)
            {
                case '#':
                    value = value.Substring(1);
                    return model =>
                    {
                        var token = model[value];

                        if (token is null)
                        {
                            return "";
                        }

#if Newton
                        return token.Type switch
                        {
                            JsonTokenType.String => token.ToString(),
                            JsonTokenType.Integer => token.ToObject<int>(),
                            JsonTokenType.Boolean => token.ToObject<bool>(),
                            _ => throw new InvalidOperationException("Unsupported JToken type"),
                        };
#else
                        return token.GetValueKind() switch
                        {
                            JsonTokenType.String => token.GetValue<string>(),
                            JsonTokenType.Number => token.GetValue<int>(),
                            JsonTokenType.True => true,
                            JsonTokenType.False => false,
                            _ => throw new InvalidOperationException("Unsupported JToken type"),
                        };
#endif
                    };
                case '`':
                case '"':
                case '\'':
                    if (value[value.Length - 1] == firstChar)
                    {
                        value = value.Substring(1, value.Length - 2);
                        string escapeCheckPattern = "(?:\\\\)*" + firstChar;

                        {
                            var matches = Regex.Matches(value, escapeCheckPattern);
                            foreach (Match match in matches)
                            {
                                int backslashCount = match.Value.Length - 1;
                                if (backslashCount % 2 == 0)
                                {
                                    throw new Exception("Invalid Expression: invalid escaped constant: " + value);
                                }
                            }
                        }

                        {
                            var match = Regex.Match(value, @"(?:\\)+$");
                            if (match.Success)
                            {
                                int backslashCount = match.Value.Length;
                                if (backslashCount % 2 != 0)
                                {
                                    throw new Exception("Invalid Expression: escaped constant ending: " + value);
                                }
                            }
                        }

                        return model => value;
                    }

                    throw new Exception("Invalid Expression: end quotes don't match starting quotes for constant: " + value);
            }

            switch (value.ToLowerInvariant())
            {
                case "true":
                    return model => true;
                case "false":
                    return model => false;
            }

            var operatorMatch = OperatorRegex.Match(value);
            if (operatorMatch.Success)
            {
                var operatorName = operatorMatch.Groups[1].Value;
                var body = operatorMatch.Groups[2].Value;

                return ParseOperator(operatorName, body);
            }
        }

        if (int.TryParse(value, out var numericValue))
        {
            return model => numericValue;
        }

        throw new Exception("Invalid Expression: invalid constant format: " + value);
    }

    private static Func<JsonObject, SimpleType> InnerParseExpression(string expression)
    {
        return SimpleExpressions.GetParsedExpression(expression, Parse);
    }

    public static Func<JsonObject, SimpleType> ParseExpression(string expression)
    {
        return InnerParseExpression(expression);
    }

    private static bool EvaluateRegex(SimpleType value, SimpleType pattern)
    {
        if (value.IsThird && pattern.IsThird && !string.IsNullOrEmpty(pattern.Third))
        {
            var stringValue = value.Third ?? string.Empty;
            return Regex.IsMatch(stringValue, pattern.Third, RegexOptions.IgnoreCase);
        }

        return false;
    }

    private static string Concat(SimpleType value1, SimpleType value2)
    {
        return "" + value1 + value2;
    }

    private static Func<JsonObject, SimpleType> ParseLength(string value)
    {
        var inner = ParseSingleBody(value);
        return model => Len(inner(model));
    }

    private static int Len(object value)
    {
        if (value is null)
        {
            return 0;
        }

        if (value is string str)
        {
            return str.Length;
        }

        if (value is IEnumerable enumerable)
        {
            var enumerator = enumerable.GetEnumerator();

            int count = 0;
            while (enumerator.MoveNext())
            {
                count++;
            }

            return count;
        }

        if (value is SimpleType simple && simple.IsThird)
        {
            return simple.Third.Length;
        }

        throw new Exception("Input does not have a length");
    }
}
