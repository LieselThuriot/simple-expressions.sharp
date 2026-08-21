#nullable enable

using System.Text.RegularExpressions;

namespace Simple.Expressions;

internal delegate ExpressionValue Evaluator(ExpressionModel model);

internal sealed class ExpressionParser(string expression)
{
    private static readonly HashSet<string> s_unsafeReferenceSegments = new(StringComparer.Ordinal)
    {
        "__proto__", "constructor", "prototype",
    };

    private readonly Tokenizer _tokenizer = new(expression);

    public Evaluator Parse()
    {
        var result = ParseValue();
        if (_tokenizer.Kind != TokenKind.End)
        {
            Fail("unexpected trailing input");
        }

        return result;
    }

    private Evaluator ParseValue()
    {
        switch (_tokenizer.Kind)
        {
            case TokenKind.String:
            {
                string value = _tokenizer.Value;
                _tokenizer.Next();
                return Constant(ExpressionValue.From(value));
            }
            case TokenKind.Number:
            {
                double value = double.Parse(_tokenizer.Value, System.Globalization.CultureInfo.InvariantCulture);
                _tokenizer.Next();
                return Constant(ExpressionValue.From(value));
            }
            case TokenKind.Reference:
            {
                string path = _tokenizer.Value;
                string[] segments = SplitReference(path);
                _tokenizer.Next();
                // Split once here rather than on every resolution, and specialise the
                // single-segment case, mirroring upstream's referenceEvaluator.
                return segments.Length == 1
                    ? model => model.ResolveSingle(path)
                    : model => model.Resolve(path, segments);
            }
            case TokenKind.OpenBracket:
                return ParseList();
            case TokenKind.Identifier:
            {
                string identifier = _tokenizer.Value;
                _tokenizer.Next();
                if (identifier.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    return Constant(ExpressionValue.From(true));
                }

                if (identifier.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    return Constant(ExpressionValue.From(false));
                }

                return ParseCall(identifier);
            }
            default:
                Fail("expected an expression");
                return null!;
        }
    }

    private Evaluator ParseList()
    {
        _tokenizer.Next();
        if (_tokenizer.Kind == TokenKind.CloseBracket)
        {
            _tokenizer.Next();
            return Constant(ExpressionValue.FromArray(Array.Empty<ExpressionValue>()));
        }

        var values = new List<Evaluator> { ParseValue() };
        while (_tokenizer.Kind == TokenKind.Comma)
        {
            _tokenizer.Next();
            values.Add(ParseValue());
        }

        if (_tokenizer.Kind != TokenKind.CloseBracket)
        {
            Fail("expected closing bracket");
        }

        _tokenizer.Next();
        var items = values.ToArray();
        return model =>
        {
            var results = new ExpressionValue[items.Length];
            for (int index = 0; index < items.Length; index++)
            {
                results[index] = items[index](model);
            }

            return ExpressionValue.FromArray(results);
        };
    }

    private Evaluator ParseCall(string name)
    {
        if (_tokenizer.Kind != TokenKind.OpenParen)
        {
            Fail("invalid operator: " + name);
        }

        string operation = name.ToLowerInvariant();
        int arity = GetArity(operation);
        _tokenizer.Next();
        if (_tokenizer.Kind == TokenKind.CloseParen)
        {
            Fail("missing operator argument");
        }

        var arguments = new List<Evaluator> { ParseValue() };
        if (arity == -1)
        {
            if (_tokenizer.Kind != TokenKind.Comma)
            {
                if (_tokenizer.Kind != TokenKind.CloseParen || !SupportsSingleListArgument(operation))
                {
                    Fail("operator " + name + " expects at least two arguments");
                }

                _tokenizer.Next();
                return Compile(operation, arguments);
            }

            while (_tokenizer.Kind == TokenKind.Comma)
            {
                _tokenizer.Next();
                arguments.Add(ParseValue());
            }
        }
        else
        {
            while (arguments.Count < arity)
            {
                if (_tokenizer.Kind != TokenKind.Comma)
                {
                    Fail("operator " + name + " expects " + ArityText(arity));
                }

                _tokenizer.Next();
                arguments.Add(ParseValue());
            }
        }

        if (_tokenizer.Kind != TokenKind.CloseParen)
        {
            Fail("operator " + name + " expects " + (arity == -1 ? "at least two arguments" : ArityText(arity)));
        }

        _tokenizer.Next();
        return Compile(operation, arguments);
    }

    private int GetArity(string operation)
    {
        return operation switch
        {
            "not" or "empty" or "len" or "lower" or "upper" or "trim" or "defined" or "int" or "num" or "abs" or "floor" or "ceil" or "sqrt" => 1,
            "eq" or "contains" or "startswith" or "endswith" or "gt" or "lt" or "match" or "in" or "round" or "pow" => 2,
            "clamp" => 3,
            "if" => 3,
            "and" or "or" or "concat" or "add" or "sub" or "mul" or "div" or "mod" or "min" or "max" => -1,
            _ => FailArity(operation),
        };
    }

    private int FailArity(string operation)
    {
        Fail("invalid operator: " + operation);
        return 0;
    }

    private static Evaluator Compile(string operation, List<Evaluator> arguments)
    {
        return operation switch
        {
            "not" => model => ExpressionValue.From(!arguments[0](model).IsTruthy()),
            "empty" => model => ExpressionValue.From(IsEmpty(arguments[0](model))),
            "len" => model => Length(arguments[0](model)),
            "lower" => model => ExpressionValue.From(ToFalsyString(arguments[0](model)).ToLowerInvariant()),
            "upper" => model => ExpressionValue.From(ToFalsyString(arguments[0](model)).ToUpperInvariant()),
            "trim" => model => Trim(arguments[0](model)),
            "defined" => model => ExpressionValue.From(arguments[0](model).Kind is not (ExpressionValueKind.Missing or ExpressionValueKind.Null)),
            "int" => model => ExpressionValue.From(Math.Truncate(arguments[0](model).ToNumber())),
            "num" => model => ExpressionValue.From(arguments[0](model).ToNumber()),
            "abs" => model => ExpressionValue.From(Math.Abs(arguments[0](model).ToNumber())),
            "floor" => model => ExpressionValue.From(Math.Floor(arguments[0](model).ToNumber())),
            "ceil" => model => ExpressionValue.From(Math.Ceiling(arguments[0](model).ToNumber())),
            "sqrt" => model => ExpressionValue.From(Math.Sqrt(arguments[0](model).ToNumber())),
            "eq" => model => ExpressionValue.From(LooseEquals(arguments[0](model), arguments[1](model))),
            "contains" => model => ExpressionValue.From(Contains(arguments[0](model), arguments[1](model))),
            "startswith" => model => ExpressionValue.From(StartsWith(arguments[0](model), arguments[1](model))),
            "endswith" => model => ExpressionValue.From(EndsWith(arguments[0](model), arguments[1](model))),
            "in" => model => ExpressionValue.From(In(arguments[0](model), arguments[1](model))),
            "gt" => model => ExpressionValue.From(Compare(arguments[0](model), arguments[1](model)) > 0),
            "lt" => model => ExpressionValue.From(Compare(arguments[0](model), arguments[1](model)) < 0),
            "match" => CompileMatch(arguments[0], arguments[1]),
            "and" => model => EvaluateAnd(arguments, model),
            "or" => model => EvaluateOr(arguments, model),
            "concat" => CompileConcat(arguments),
            "if" => model => arguments[0](model).IsTruthy() ? arguments[1](model) : arguments[2](model),
            "round" => model => Round(arguments[0](model), arguments[1](model)),
            "pow" => model => ExpressionValue.From(Math.Pow(arguments[0](model).ToNumber(), arguments[1](model).ToNumber())),
            "clamp" => model => ExpressionValue.From(Math.Min(Math.Max(arguments[0](model).ToNumber(), arguments[1](model).ToNumber()), arguments[2](model).ToNumber())),
            "add" => CompileNumericVariadic(arguments, NumericOperation.Add, "add"),
            "sub" => CompileNumericVariadic(arguments, NumericOperation.Subtract, "sub"),
            "mul" => CompileNumericVariadic(arguments, NumericOperation.Multiply, "mul"),
            "div" => CompileNumericVariadic(arguments, NumericOperation.Divide, "div"),
            "mod" => CompileNumericVariadic(arguments, NumericOperation.Modulo, "mod"),
            "min" => CompileNumericVariadic(arguments, NumericOperation.Minimum, "min"),
            "max" => CompileNumericVariadic(arguments, NumericOperation.Maximum, "max"),
            _ => throw new InvalidOperationException("Unknown operator: " + operation),
        };
    }

    private enum NumericOperation { Add, Subtract, Multiply, Divide, Modulo, Minimum, Maximum }

    private static bool SupportsSingleListArgument(string operation)
        => operation is "add" or "mul" or "min" or "max";

    private static Evaluator CompileNumericVariadic(List<Evaluator> arguments, NumericOperation operation, string name)
    {
        return model =>
        {
            var values = new List<double>();
            foreach (var argument in arguments)
            {
                var value = argument(model);
                if (operation is NumericOperation.Add or NumericOperation.Multiply or NumericOperation.Minimum or NumericOperation.Maximum
                    && arguments.Count == 1)
                {
                    if (value.Kind != ExpressionValueKind.Array || !ExpressionModel.TryGetArrayValues(value, out var items))
                    {
                        throw new InvalidOperationException("operator " + name + " expects at least two arguments");
                    }

                    foreach (var item in items)
                    {
                        values.Add(item.ToNumber());
                    }
                }
                else if (operation is NumericOperation.Add or NumericOperation.Multiply or NumericOperation.Minimum or NumericOperation.Maximum
                    && value.Kind == ExpressionValueKind.Array && ExpressionModel.TryGetArrayValues(value, out var expanded))
                {
                    foreach (var item in expanded)
                    {
                        values.Add(item.ToNumber());
                    }
                }
                else
                {
                    values.Add(value.ToNumber());
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
        };
    }

    private static ExpressionValue Round(ExpressionValue value, ExpressionValue decimals)
    {
        double places = decimals.ToNumber();
        if (double.IsNaN(places) || double.IsInfinity(places) || places != Math.Truncate(places) || places < 0 || places > 9007199254740991d)
        {
            throw new InvalidOperationException("Round precision must be a non-negative safe integer");
        }

        double factor = Math.Pow(10, places);
        if (double.IsInfinity(factor))
        {
            throw new InvalidOperationException("Round precision is too large");
        }

        return ExpressionValue.From(Math.Floor((value.ToNumber() * factor) + 0.5) / factor);
    }

    private static Evaluator CompileMatch(Evaluator input, Evaluator pattern)
    {
        if (TryGetConstant(pattern, out var constant) && constant.Kind == ExpressionValueKind.String && constant.IsTruthy())
        {
            var regex = new Regex((string)constant.Value!, RegexOptions.CultureInvariant);
            return model => ExpressionValue.From(regex.IsMatch(ToFalsyString(input(model))));
        }

        return model =>
        {
            var runtimePattern = pattern(model);
            return ExpressionValue.From(runtimePattern.Kind == ExpressionValueKind.String && runtimePattern.IsTruthy() && GetRuntimeRegex((string)runtimePattern.Value!).IsMatch(ToFalsyString(input(model))));
        };
    }

    internal static void ClearRuntimePatterns() => RuntimeRegexCache.Clear();

    private static Regex GetRuntimeRegex(string pattern) => RuntimeRegexCache.Get(pattern);

    private static ExpressionValue EvaluateAnd(List<Evaluator> arguments, ExpressionModel model)
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

    private static ExpressionValue EvaluateOr(List<Evaluator> arguments, ExpressionModel model)
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

    private static ExpressionValue EvaluateConcat(List<Evaluator> arguments, ExpressionModel model)
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

    private static Evaluator CompileConcat(List<Evaluator> arguments)
    {
        var constants = new ExpressionValue[arguments.Count];
        for (int index = 0; index < arguments.Count; index++)
        {
            if (!TryGetConstant(arguments[index], out constants[index]))
            {
                return model => EvaluateConcat(arguments, model);
            }
        }

        string result = string.Empty;
        foreach (var value in constants)
        {
            if (value.IsTruthy())
            {
                result += value.ToJavaScriptString();
            }
        }

        return Constant(ExpressionValue.From(result));
    }

    private static Evaluator Constant(ExpressionValue value) => new ConstantEvaluator(value).Evaluate;
    private static bool TryGetConstant(Evaluator evaluator, out ExpressionValue value)
    {
        if (evaluator.Target is ConstantEvaluator constant)
        {
            value = constant.Value;
            return true;
        }

        value = default;
        return false;
    }

    private static bool IsEmpty(ExpressionValue value) => value.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null || (value.Kind == ExpressionValueKind.String && ((string)value.Value!).Length == 0);
    private static string ToFalsyString(ExpressionValue value) => value.IsTruthy() ? value.ToJavaScriptString() : string.Empty;
    private static ExpressionValue Trim(ExpressionValue value)
    {
        if (value.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null)
        {
            return value;
        }

        return ExpressionValue.From(ToFalsyString(value).Trim());
    }

    private static bool Contains(ExpressionValue left, ExpressionValue right) => left.Kind == ExpressionValueKind.String && ((string)left.Value!).IndexOf(right.ToJavaScriptString(), StringComparison.Ordinal) >= 0;
    private static bool StartsWith(ExpressionValue left, ExpressionValue right) => left.Kind == ExpressionValueKind.String && ((string)left.Value!).StartsWith(right.ToJavaScriptString(), StringComparison.Ordinal);
    private static bool EndsWith(ExpressionValue left, ExpressionValue right) => left.Kind == ExpressionValueKind.String && ((string)left.Value!).EndsWith(right.ToJavaScriptString(), StringComparison.Ordinal);
    private static bool In(ExpressionValue value, ExpressionValue list) => ExpressionModel.TryGetArrayValues(list, out var values) && values.Any(item => LooseEquals(value, item));

    private static ExpressionValue Length(ExpressionValue value)
    {
        if (value.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null)
        {
            return ExpressionValue.From(0d);
        }

        if (value.Kind == ExpressionValueKind.String)
        {
            return ExpressionValue.From((double)((string)value.Value!).Length);
        }

        if (ExpressionModel.TryGetOwnLength(value, out var length))
        {
            return length;
        }

        throw new InvalidOperationException("Input does not have a length");
    }

    private const int MaxStructuralDepth = 64;

    private static bool LooseEquals(ExpressionValue left, ExpressionValue right) => LooseEquals(left, right, 0);

    private static bool LooseEquals(ExpressionValue left, ExpressionValue right, int depth)
    {
        if (left.Kind == right.Kind)
        {
            return left.Kind switch
            {
                ExpressionValueKind.Missing or ExpressionValueKind.Null => true,
                ExpressionValueKind.Boolean => (bool)left.Value! == (bool)right.Value!,
                ExpressionValueKind.Number => (double)left.Value! == (double)right.Value!,
                ExpressionValueKind.String => (string)left.Value! == (string)right.Value!,
                ExpressionValueKind.Array => StructuralArrayEquals(left, right, depth),
                _ => StructuralObjectEquals(left, right, depth),
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
            return LooseEquals(ExpressionValue.From(left.ToJavaScriptString()), right);
        }

        if (right.Kind is ExpressionValueKind.Array or ExpressionValueKind.Object)
        {
            return LooseEquals(left, ExpressionValue.From(right.ToJavaScriptString()));
        }

        if (left.Kind == ExpressionValueKind.Boolean || right.Kind == ExpressionValueKind.Boolean || (left.Kind == ExpressionValueKind.Number && right.Kind == ExpressionValueKind.String) || (left.Kind == ExpressionValueKind.String && right.Kind == ExpressionValueKind.Number))
        {
            return left.ToNumber() == right.ToNumber();
        }

        return false;
    }

    private static bool StructuralArrayEquals(ExpressionValue left, ExpressionValue right, int depth)
    {
        if (ReferenceEquals(left.Value, right.Value))
        {
            return true;
        }

        if (depth >= MaxStructuralDepth)
        {
            return false;
        }

        if (!ExpressionModel.TryGetArrayValues(left, out var leftValues) || !ExpressionModel.TryGetArrayValues(right, out var rightValues))
        {
            return false;
        }

        using var leftEnumerator = leftValues.GetEnumerator();
        using var rightEnumerator = rightValues.GetEnumerator();
        while (leftEnumerator.MoveNext())
        {
            if (!rightEnumerator.MoveNext() || !LooseEquals(leftEnumerator.Current, rightEnumerator.Current, depth + 1))
            {
                return false;
            }
        }

        return !rightEnumerator.MoveNext();
    }

    private static bool StructuralObjectEquals(ExpressionValue left, ExpressionValue right, int depth)
    {
        if (ReferenceEquals(left.Value, right.Value))
        {
            return true;
        }

        if (depth >= MaxStructuralDepth)
        {
            return false;
        }

        if (!ExpressionModel.TryGetObjectEntries(left, out var leftEntries) || !ExpressionModel.TryGetObjectEntries(right, out var rightEntries))
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
            if (!rightByName.TryGetValue(entry.Key, out var other) || !LooseEquals(entry.Value, other, depth + 1))
            {
                return false;
            }

            count++;
        }

        return count == rightByName.Count;
    }

    private static int Compare(ExpressionValue left, ExpressionValue right)
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

    private static string[] SplitReference(string path)
    {
        string[] segments = path.Split('.');
        foreach (string? segment in segments)
        {
            if (segment.Length == 0 || s_unsafeReferenceSegments.Contains(segment))
            {
                throw ExpressionParseException.WithoutPosition("invalid model reference: #" + path);
            }
        }

        return segments;
    }

    private static string ArityText(int arity) => arity switch { 1 => "one argument", 2 => "two arguments", _ => "three arguments" };
    private void Fail(string message) => throw new ExpressionParseException(message, _tokenizer.Position);

    private sealed class ConstantEvaluator(ExpressionValue value)
    {
        public ExpressionValue Value { get; } = value;
        public ExpressionValue Evaluate(ExpressionModel _) => Value;
    }
}
