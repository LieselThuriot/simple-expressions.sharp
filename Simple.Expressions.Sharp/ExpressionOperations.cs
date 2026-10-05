#nullable enable

using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Simple.Expressions;

internal static class ExpressionOperations
{
    public static bool IsEmpty(ExpressionValue value)
        => value.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null
            || (value.Kind == ExpressionValueKind.String && ((string)value.Value!).Length == 0);

    public static string ToFalsyString(ExpressionValue value)
        => value.IsTruthy() ? value.ToText() : string.Empty;

    public static ExpressionValue Trim(ExpressionValue value)
    {
        if (value.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null)
        {
            return value;
        }

        return ExpressionValue.From(ExpressionText.Trim(ToFalsyString(value)));
    }

    public static bool Contains(ExpressionValue left, ExpressionValue right)
        => left.Kind == ExpressionValueKind.String && ((string)left.Value!).IndexOf(right.ToText(), StringComparison.Ordinal) >= 0;

    public static bool StartsWith(ExpressionValue left, ExpressionValue right)
        => left.Kind == ExpressionValueKind.String && ((string)left.Value!).StartsWith(right.ToText(), StringComparison.Ordinal);

    public static bool EndsWith(ExpressionValue left, ExpressionValue right)
        => left.Kind == ExpressionValueKind.String && ((string)left.Value!).EndsWith(right.ToText(), StringComparison.Ordinal);

    public static bool In(ExpressionValue value, ExpressionValue list)
        => ClrValueAdapter.TryGetArrayValues(list, out var values) && values.Any(item => LooseEquals(value, item));

    public static ExpressionValue Length(ExpressionValue value)
    {
        if (value.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null)
        {
            return ExpressionValue.From(0d);
        }

        if (value.Kind == ExpressionValueKind.String)
        {
            return ExpressionValue.From((double)((string)value.Value!).Length);
        }

        if (ClrValueAdapter.TryGetOwnLength(value, out var length))
        {
            return length;
        }

        throw new ExpressionEvaluationException("Input does not have a length");
    }

    public static ExpressionValue Round(ExpressionValue value, ExpressionValue decimals)
    {
        double places = decimals.ToNumber(true);
        if (places != Math.Truncate(places) || places < 0)
        {
            throw new ExpressionEvaluationException("Round precision must be a non-negative safe integer");
        }

        if (places > 308)
        {
            throw new ExpressionEvaluationException("Round precision is too large");
        }

        double number = value.ToNumber(true);
        return NumericResult(RoundAwayFromZero(number, (int)places));
    }

    public static ExpressionValue NumericResult(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ExpressionEvaluationException("Invalid numeric result");
        }

        return ExpressionValue.From(value);
    }

    public static ExpressionValue EvaluateAnd(List<Evaluator> arguments, ExpressionModel model)
    {
        foreach (var argument in arguments)
        {
            if (!argument(model).IsTruthy())
            {
                return ExpressionValue.From(false);
            }
        }

        return ExpressionValue.From(true);
    }

    public static ExpressionValue EvaluateOr(List<Evaluator> arguments, ExpressionModel model)
    {
        foreach (var argument in arguments)
        {
            if (argument(model).IsTruthy())
            {
                return ExpressionValue.From(true);
            }
        }

        return ExpressionValue.From(false);
    }

    public static ExpressionValue EvaluateConcat(List<Evaluator> arguments, ExpressionModel model)
    {
        string result = string.Empty;
        foreach (var argument in arguments)
        {
            var value = argument(model);
            if (value.IsTruthy())
            {
                result += value.ToText();
            }
        }

        return ExpressionValue.From(result);
    }

    public static ExpressionValue Concat(IEnumerable<ExpressionValue> values)
    {
        string result = string.Empty;
        foreach (var value in values)
        {
            if (value.IsTruthy())
            {
                result += value.ToText();
            }
        }

        return ExpressionValue.From(result);
    }

    public static ExpressionValue EvaluateNumeric(List<Evaluator> arguments, ExpressionModel model, NumericOperation operation, string name)
    {
        var argumentsValues = new List<ExpressionValue>(arguments.Count);
        foreach (var argument in arguments)
        {
            argumentsValues.Add(argument(model));
        }

        if (argumentsValues.Count == 1 && argumentsValues[0].Kind != ExpressionValueKind.Array)
        {
            throw new ExpressionEvaluationException("Invalid Expression: operator " + name + " expects at least two arguments or one list argument");
        }

        var leaves = new List<ExpressionValue>();
        var activeEnumerables = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var value in argumentsValues)
        {
            AddNumericValues(value);
        }

        var values = new List<double>(leaves.Count);
        foreach (var leaf in leaves)
        {
            values.Add(leaf.ToNumber(true));
        }

        if (operation is NumericOperation.Subtract or NumericOperation.Divide or NumericOperation.Modulo && values.Count < 2)
        {
            throw new ExpressionEvaluationException(operation switch
            {
                NumericOperation.Subtract => "Sub requires at least two values",
                NumericOperation.Divide => "Div requires at least two values",
                _ => "Mod requires at least two values",
            });
        }

        if (operation is NumericOperation.Minimum or NumericOperation.Maximum && values.Count == 0)
        {
            throw new ExpressionEvaluationException(operation == NumericOperation.Minimum ? "Min requires at least one value" : "Max requires at least one value");
        }

        double result = values.Count == 0 ? 0d : values[0];
        int start = 1;
        if (operation == NumericOperation.Add)
        {
            result = 0;
        }

        if (operation == NumericOperation.Multiply)
        {
            result = 1;
        }

        if (operation is NumericOperation.Add or NumericOperation.Multiply)
        {
            start = 0;
        }

        for (int index = start; index < values.Count; index++)
        {
            double value = values[index];
            switch (operation)
            {
                case NumericOperation.Add:
                    result += value;
                    break;
                case NumericOperation.Subtract:
                    result -= value;
                    break;
                case NumericOperation.Multiply:
                    result *= value;
                    break;
                case NumericOperation.Divide:
                    if (value == 0)
                    {
                        throw new ExpressionEvaluationException("Division by zero");
                    }

                    result /= value;
                    break;
                case NumericOperation.Modulo:
                    if (value == 0)
                    {
                        throw new ExpressionEvaluationException("Division by zero");
                    }

                    result %= value;
                    break;
                case NumericOperation.Minimum:
                    result = Math.Min(result, value);
                    break;
                case NumericOperation.Maximum:
                    result = Math.Max(result, value);
                    break;
            }
        }

        return NumericResult(result);

        void AddNumericValues(ExpressionValue value)
        {
            if (value.Kind != ExpressionValueKind.Array)
            {
                leaves.Add(value);
                return;
            }

            object enumerable = value.Value!;
            if (!activeEnumerables.Add(enumerable))
            {
                throw new ExpressionEvaluationException("Invalid numeric value");
            }

            try
            {
                if (!ClrValueAdapter.TryGetArrayValues(value, out var items))
                {
                    throw new ExpressionEvaluationException("Invalid numeric value");
                }

                foreach (var item in items)
                {
                    AddNumericValues(item);
                }
            }
            finally
            {
                activeEnumerables.Remove(enumerable);
            }
        }
    }

    public static bool LooseEquals(ExpressionValue left, ExpressionValue right)
        => LooseEquals(left, right, new EqualityContext());

    public static bool GreaterThan(ExpressionValue left, ExpressionValue right)
        => TryCompare(left, right, out int comparison) && comparison > 0;

    public static bool LessThan(ExpressionValue left, ExpressionValue right)
        => TryCompare(left, right, out int comparison) && comparison < 0;

    private static bool TryCompare(ExpressionValue left, ExpressionValue right, out int comparison)
    {
        if (IsBlankString(left) || IsBlankString(right))
        {
            comparison = 0;
            return false;
        }

        if (left.Kind == ExpressionValueKind.String && right.Kind == ExpressionValueKind.String)
        {
            comparison = string.CompareOrdinal((string)left.Value!, (string)right.Value!);
            return true;
        }

        if (TryGetFiniteNumber(left, out double leftNumber) && TryGetFiniteNumber(right, out double rightNumber))
        {
            comparison = leftNumber.CompareTo(rightNumber);
            return true;
        }

        comparison = 0;
        return false;
    }

    private static bool IsBlankString(ExpressionValue value)
        => value.Kind == ExpressionValueKind.String && ExpressionText.Trim((string)value.Value!).Length == 0;

    private static bool LooseEquals(ExpressionValue left, ExpressionValue right, EqualityContext context)
    {
        if (left.Kind == right.Kind)
        {
            return left.Kind switch
            {
                ExpressionValueKind.Missing or ExpressionValueKind.Null => true,
                ExpressionValueKind.Boolean => (bool)left.Value! == (bool)right.Value!,
                ExpressionValueKind.Number => (double)left.Value! == (double)right.Value!,
                ExpressionValueKind.String => (string)left.Value! == (string)right.Value!,
                ExpressionValueKind.Array => StructuralArrayEquals(left, right, context),
                _ => StructuralObjectEquals(left, right, context),
            };
        }

        if ((left.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null) && (right.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null))
        {
            return true;
        }

        if (left.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null || right.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null)
        {
            return false;
        }

        if (left.Kind is ExpressionValueKind.Array or ExpressionValueKind.Object || right.Kind is ExpressionValueKind.Array or ExpressionValueKind.Object)
        {
            return false;
        }

        if (left.Kind == ExpressionValueKind.Number && right.Kind == ExpressionValueKind.String)
        {
            return IsFiniteNumber(left) && ExpressionText.TryParseFiniteDecimal((string)right.Value!, out double number) && (double)left.Value! == number;
        }

        if (left.Kind == ExpressionValueKind.String && right.Kind == ExpressionValueKind.Number)
        {
            return LooseEquals(right, left, context);
        }

        if (left.Kind == ExpressionValueKind.Boolean)
        {
            return TryGetFiniteNumber(right, out double number) && ((bool)left.Value! ? 1d : 0d) == number;
        }

        if (right.Kind == ExpressionValueKind.Boolean)
        {
            return LooseEquals(right, left, context);
        }

        return false;
    }

    private static bool TryGetFiniteNumber(ExpressionValue value, out double number)
    {
        if (value.Kind == ExpressionValueKind.Number && IsFiniteNumber(value))
        {
            number = (double)value.Value!;
            return true;
        }

        if (value.Kind == ExpressionValueKind.String)
        {
            return ExpressionText.TryParseFiniteDecimal((string)value.Value!, out number);
        }

        number = 0;
        return false;
    }

    private static bool IsFiniteNumber(ExpressionValue value)
    {
        double number = (double)value.Value!;
        return !double.IsNaN(number) && !double.IsInfinity(number);
    }

    private static double RoundAwayFromZero(double value, int places)
    {
        ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(Math.Abs(value)));
        int exponentBits = (int)((bits >> 52) & 0x7ff);
        ulong fraction = bits & 0x000fffffffffffff;
        BigInteger significand = exponentBits == 0 ? fraction : 0x0010000000000000 | fraction;
        int binaryExponent = exponentBits == 0 ? -1074 : exponentBits - 1023 - 52;
        BigInteger numerator = significand * BigInteger.Pow(5, places);
        int shift = binaryExponent + places;

        BigInteger rounded;
        if (shift >= 0)
        {
            rounded = numerator << shift;
        }
        else
        {
            BigInteger divisor = BigInteger.One << -shift;
            rounded = BigInteger.DivRem(numerator, divisor, out BigInteger remainder);
            if ((remainder * 2) >= divisor)
            {
                rounded++;
            }
        }

        if (value < 0)
        {
            rounded = -rounded;
        }

        string roundedText = rounded.ToString(CultureInfo.InvariantCulture) + "e-" + places.ToString(CultureInfo.InvariantCulture);
        if (double.TryParse(roundedText, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
        {
            return result;
        }

        return value < 0 ? double.NegativeInfinity : double.PositiveInfinity;
    }

    private static bool StructuralArrayEquals(ExpressionValue left, ExpressionValue right, EqualityContext context)
    {
        if (ReferenceEquals(left.Value, right.Value))
        {
            return true;
        }

        if (!context.Pairs.Add((left.Value!, right.Value!)))
        {
            return true;
        }

        if (!ClrValueAdapter.TryGetArrayValues(left, out var leftValues) || !ClrValueAdapter.TryGetArrayValues(right, out var rightValues))
        {
            return false;
        }

        using var leftEnumerator = leftValues.GetEnumerator();
        using var rightEnumerator = rightValues.GetEnumerator();
        while (leftEnumerator.MoveNext())
        {
            if (!rightEnumerator.MoveNext() || !LooseEquals(leftEnumerator.Current, rightEnumerator.Current, context))
            {
                return false;
            }
        }

        return !rightEnumerator.MoveNext();
    }

    private static bool StructuralObjectEquals(ExpressionValue left, ExpressionValue right, EqualityContext context)
    {
        if (ReferenceEquals(left.Value, right.Value))
        {
            return true;
        }

        if (!context.Pairs.Add((left.Value!, right.Value!)))
        {
            return true;
        }

        if (!ClrValueAdapter.TryGetObjectEntries(left, out var leftEntries) || !ClrValueAdapter.TryGetObjectEntries(right, out var rightEntries))
        {
            return false;
        }

        var rightByName = new Dictionary<string, ExpressionValue>(StringComparer.Ordinal);
        foreach (var entry in rightEntries)
        {
            rightByName[entry.Key] = entry.Value;
        }

        int count = 0;
        foreach (var entry in leftEntries)
        {
            if (!rightByName.TryGetValue(entry.Key, out var other) || !LooseEquals(entry.Value, other, context))
            {
                return false;
            }

            count++;
        }

        return count == rightByName.Count;
    }

    private sealed class EqualityContext
    {
        public HashSet<(object Left, object Right)> Pairs { get; } = new(ReferencePairComparer.Instance);
    }

    private sealed class ReferencePairComparer : IEqualityComparer<(object Left, object Right)>
    {
        public static ReferencePairComparer Instance { get; } = new();

        public bool Equals((object Left, object Right) x, (object Left, object Right) y)
            => ReferenceEquals(x.Left, y.Left) && ReferenceEquals(x.Right, y.Right);

        public int GetHashCode((object Left, object Right) value)
            => (RuntimeHelpers.GetHashCode(value.Left) * 397) ^ RuntimeHelpers.GetHashCode(value.Right);
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static ReferenceEqualityComparer Instance { get; } = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
    }
}