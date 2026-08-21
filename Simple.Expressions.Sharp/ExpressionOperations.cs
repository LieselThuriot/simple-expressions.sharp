#nullable enable

using System.Runtime.CompilerServices;

namespace Simple.Expressions;

internal static class ExpressionOperations
{
    public static bool IsEmpty(ExpressionValue value)
        => value.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null
            || (value.Kind == ExpressionValueKind.String && ((string)value.Value!).Length == 0);

    public static string ToFalsyString(ExpressionValue value)
        => value.IsTruthy() ? value.ToJavaScriptString() : string.Empty;

    public static double Num(ExpressionValue value)
    {
        if (value.Kind == ExpressionValueKind.Array)
        {
            throw new InvalidOperationException("Invalid numeric value");
        }

        return value.ToNumber(true);
    }

    public static ExpressionValue Trim(ExpressionValue value)
    {
        if (value.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null)
        {
            return value;
        }

        string text = ToFalsyString(value);
        int start = 0;
        int end = text.Length;
        while (start < end && IsEcmaScriptWhitespace(text[start]))
        {
            start++;
        }

        while (end > start && IsEcmaScriptWhitespace(text[end - 1]))
        {
            end--;
        }

        return ExpressionValue.From(text.Substring(start, end - start));
    }

    public static bool Contains(ExpressionValue left, ExpressionValue right)
        => left.Kind == ExpressionValueKind.String && ((string)left.Value!).IndexOf(right.ToJavaScriptString(), StringComparison.Ordinal) >= 0;

    public static bool StartsWith(ExpressionValue left, ExpressionValue right)
        => left.Kind == ExpressionValueKind.String && ((string)left.Value!).StartsWith(right.ToJavaScriptString(), StringComparison.Ordinal);

    public static bool EndsWith(ExpressionValue left, ExpressionValue right)
        => left.Kind == ExpressionValueKind.String && ((string)left.Value!).EndsWith(right.ToJavaScriptString(), StringComparison.Ordinal);

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

        throw new InvalidOperationException("Input does not have a length");
    }

    public static ExpressionValue Round(ExpressionValue value, ExpressionValue decimals)
    {
        double places = decimals.ToNumber(true);
        if (double.IsNaN(places) || double.IsInfinity(places) || places != Math.Truncate(places) || places < 0 || places > 9007199254740991d)
        {
            throw new InvalidOperationException("Round precision must be a non-negative safe integer");
        }

        double factor = Math.Pow(10, places);
        if (double.IsInfinity(factor))
        {
            throw new InvalidOperationException("Round precision is too large");
        }

        return ExpressionValue.From(Math.Floor((value.ToNumber(true) * factor) + 0.5) / factor);
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
                result += value.ToJavaScriptString();
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
                result += value.ToJavaScriptString();
            }
        }

        return ExpressionValue.From(result);
    }

    public static ExpressionValue EvaluateNumeric(List<Evaluator> arguments, ExpressionModel model, NumericOperation operation, string name)
    {
        var values = new List<double>();
        foreach (var argument in arguments)
        {
            var value = argument(model);
            if (operation is NumericOperation.Add or NumericOperation.Multiply or NumericOperation.Minimum or NumericOperation.Maximum
                && arguments.Count == 1)
            {
                if (value.Kind != ExpressionValueKind.Array || !ClrValueAdapter.TryGetArrayValues(value, out var items))
                {
                    throw new InvalidOperationException("operator " + name + " expects at least two arguments");
                }

                foreach (var item in items)
                {
                    values.Add(item.ToNumber(true));
                }
            }
            else if (operation is NumericOperation.Add or NumericOperation.Multiply or NumericOperation.Minimum or NumericOperation.Maximum
                && value.Kind == ExpressionValueKind.Array && ClrValueAdapter.TryGetArrayValues(value, out var expanded))
            {
                foreach (var item in expanded)
                {
                    values.Add(item.ToNumber(true));
                }
            }
            else
            {
                values.Add(value.ToNumber(true));
            }
        }

        if (operation is NumericOperation.Minimum or NumericOperation.Maximum && values.Count == 0)
        {
            throw new InvalidOperationException(operation == NumericOperation.Minimum ? "Min requires at least one value" : "Max requires at least one value");
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
                        throw new InvalidOperationException("Division by zero");
                    }

                    result /= value;
                    break;
                case NumericOperation.Modulo:
                    if (value == 0)
                    {
                        throw new InvalidOperationException("Division by zero");
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

        return ExpressionValue.From(result);
    }

    public static bool LooseEquals(ExpressionValue left, ExpressionValue right)
        => LooseEquals(left, right, new EqualityContext());

    public static int Compare(ExpressionValue left, ExpressionValue right)
    {
        if (left.Kind is ExpressionValueKind.Array or ExpressionValueKind.Object)
        {
            left = ExpressionValue.From(left.ToJavaScriptString());
        }

        if (right.Kind is ExpressionValueKind.Array or ExpressionValueKind.Object)
        {
            right = ExpressionValue.From(right.ToJavaScriptString());
        }

        if (left.Kind == ExpressionValueKind.String && right.Kind == ExpressionValueKind.String)
        {
            return string.CompareOrdinal((string)left.Value!, (string)right.Value!);
        }

        double leftNumber = left.ToNumber();
        double rightNumber = right.ToNumber();
        return double.IsNaN(leftNumber) || double.IsNaN(rightNumber) ? 0 : leftNumber.CompareTo(rightNumber);
    }

    private static bool IsEcmaScriptWhitespace(char value)
        => value is (>= '\u0009' and <= '\u000D')
            or '\u0020' or '\u00A0' or '\u1680'
            or (>= '\u2000' and <= '\u200A')
            or '\u2028' or '\u2029' or '\u202F' or '\u205F' or '\u3000' or '\uFEFF';

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

        if (left.Kind is ExpressionValueKind.Array or ExpressionValueKind.Object)
        {
            return LooseEquals(ExpressionValue.From(left.ToJavaScriptString()), right, context);
        }

        if (right.Kind is ExpressionValueKind.Array or ExpressionValueKind.Object)
        {
            return LooseEquals(left, ExpressionValue.From(right.ToJavaScriptString()), context);
        }

        if (left.Kind == ExpressionValueKind.Boolean || right.Kind == ExpressionValueKind.Boolean || (left.Kind == ExpressionValueKind.Number && right.Kind == ExpressionValueKind.String) || (left.Kind == ExpressionValueKind.String && right.Kind == ExpressionValueKind.Number))
        {
            return left.ToNumber() == right.ToNumber();
        }

        return false;
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
}
