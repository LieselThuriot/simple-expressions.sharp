#nullable enable

using System.Text.RegularExpressions;

namespace Simple.Expressions;

internal static class ExpressionCompiler
{
    public const int VariadicArity = -1;

    internal readonly struct OperatorSpec(int arity, bool allowsSingleListArgument = false)
    {
        public int Arity { get; } = arity;
        public bool AllowsSingleListArgument { get; } = allowsSingleListArgument;
    }

    private static readonly HashSet<string> s_unsafeReferenceSegments = new(StringComparer.Ordinal)
    {
        "__proto__", "constructor", "prototype",
    };

    public static bool TryGetOperatorSpec(string operation, out OperatorSpec spec)
    {
        spec = operation switch
        {
            "not" or "empty" or "len" or "lower" or "upper" or "trim" or "defined" or "int" or "num" or "abs" or "floor" or "ceil" or "sqrt" => new OperatorSpec(1),
            "eq" or "contains" or "startswith" or "endswith" or "gt" or "lt" or "match" or "in" or "round" or "pow" => new OperatorSpec(2),
            "clamp" or "if" => new OperatorSpec(3),
            "and" or "or" or "concat" => new OperatorSpec(VariadicArity),
            "add" or "sub" or "mul" or "div" or "mod" or "min" or "max" => new OperatorSpec(VariadicArity, allowsSingleListArgument: true),
            _ => default,
        };

        return spec.Arity != 0;
    }

    public static Evaluator Constant(ExpressionValue value) => new ConstantEvaluator(value).Evaluate;

    public static Evaluator Reference(string path)
    {
        string[] segments = SplitReference(path);
        return segments.Length == 1
            ? model => model.ResolveSingle(path)
            : model => model.Resolve(path, segments);
    }

    public static Evaluator List(List<Evaluator> values)
    {
        if (values.Count == 0)
        {
            return Constant(ExpressionValue.FromArray(Array.Empty<ExpressionValue>()));
        }

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

    public static Evaluator Operation(string operation, List<Evaluator> arguments)
    {
        return operation switch
        {
            "not" => model => ExpressionValue.From(!arguments[0](model).IsTruthy()),
            "empty" => model => ExpressionValue.From(ExpressionOperations.IsEmpty(arguments[0](model))),
            "len" => model => ExpressionOperations.Length(arguments[0](model)),
            "lower" => model => ExpressionValue.From(ExpressionOperations.ToFalsyString(arguments[0](model)).ToLowerInvariant()),
            "upper" => model => ExpressionValue.From(ExpressionOperations.ToFalsyString(arguments[0](model)).ToUpperInvariant()),
            "trim" => model => ExpressionOperations.Trim(arguments[0](model)),
            "defined" => model => ExpressionValue.From(arguments[0](model).Kind is not (ExpressionValueKind.Missing or ExpressionValueKind.Null)),
            "int" => model => ExpressionOperations.NumericResult(Math.Truncate(arguments[0](model).ToNumber(true))),
            "num" => model => ExpressionValue.From(arguments[0](model).ToNumber(true)),
            "abs" => model => ExpressionOperations.NumericResult(Math.Abs(arguments[0](model).ToNumber(true))),
            "floor" => model => ExpressionOperations.NumericResult(Math.Floor(arguments[0](model).ToNumber(true))),
            "ceil" => model => ExpressionOperations.NumericResult(Math.Ceiling(arguments[0](model).ToNumber(true))),
            "sqrt" => model => ExpressionOperations.NumericResult(Math.Sqrt(arguments[0](model).ToNumber(true))),
            "eq" => model => ExpressionValue.From(ExpressionOperations.LooseEquals(arguments[0](model), arguments[1](model))),
            "contains" => model => ExpressionValue.From(ExpressionOperations.Contains(arguments[0](model), arguments[1](model))),
            "startswith" => model => ExpressionValue.From(ExpressionOperations.StartsWith(arguments[0](model), arguments[1](model))),
            "endswith" => model => ExpressionValue.From(ExpressionOperations.EndsWith(arguments[0](model), arguments[1](model))),
            "in" => model => ExpressionValue.From(ExpressionOperations.In(arguments[0](model), arguments[1](model))),
            "gt" => model => ExpressionValue.From(ExpressionOperations.GreaterThan(arguments[0](model), arguments[1](model))),
            "lt" => model => ExpressionValue.From(ExpressionOperations.LessThan(arguments[0](model), arguments[1](model))),
            "match" => CompileMatch(arguments[0], arguments[1]),
            "and" => model => ExpressionOperations.EvaluateAnd(arguments, model),
            "or" => model => ExpressionOperations.EvaluateOr(arguments, model),
            "concat" => CompileConcat(arguments),
            "if" => model => arguments[0](model).IsTruthy() ? arguments[1](model) : arguments[2](model),
            "round" => model => ExpressionOperations.Round(arguments[0](model), arguments[1](model)),
            "pow" => model =>
            {
                var baseValue = arguments[0](model);
                var exponent = arguments[1](model);
                return ExpressionOperations.NumericResult(Math.Pow(baseValue.ToNumber(true), exponent.ToNumber(true)));
            },
            "clamp" => model =>
            {
                var value = arguments[0](model);
                var minimum = arguments[1](model);
                var maximum = arguments[2](model);
                return ExpressionOperations.NumericResult(Math.Min(Math.Max(value.ToNumber(true), minimum.ToNumber(true)), maximum.ToNumber(true)));
            },
            "add" => model => ExpressionOperations.EvaluateNumeric(arguments, model, NumericOperation.Add, "add"),
            "sub" => model => ExpressionOperations.EvaluateNumeric(arguments, model, NumericOperation.Subtract, "sub"),
            "mul" => model => ExpressionOperations.EvaluateNumeric(arguments, model, NumericOperation.Multiply, "mul"),
            "div" => model => ExpressionOperations.EvaluateNumeric(arguments, model, NumericOperation.Divide, "div"),
            "mod" => model => ExpressionOperations.EvaluateNumeric(arguments, model, NumericOperation.Modulo, "mod"),
            "min" => model => ExpressionOperations.EvaluateNumeric(arguments, model, NumericOperation.Minimum, "min"),
            "max" => model => ExpressionOperations.EvaluateNumeric(arguments, model, NumericOperation.Maximum, "max"),
            _ => throw ExpressionParseException.WithoutPosition("invalid operator: " + operation),
        };
    }

    private static Evaluator CompileMatch(Evaluator input, Evaluator pattern)
    {
        if (TryGetConstant(pattern, out var constant) && constant.Kind == ExpressionValueKind.String && constant.IsTruthy())
        {
            Regex regex;
            try
            {
                regex = new Regex((string)constant.Value!, RegexOptions.CultureInvariant);
            }
            catch (ArgumentException exception)
            {
                throw ExpressionParseException.WithoutPosition("invalid regular expression: " + exception.Message);
            }

            return model => ExpressionValue.From(regex.IsMatch(ExpressionOperations.ToFalsyString(input(model))));
        }

        return model =>
        {
            var runtimeInput = input(model);
            var runtimePattern = pattern(model);
            if (runtimePattern.Kind != ExpressionValueKind.String || !runtimePattern.IsTruthy())
            {
                return ExpressionValue.From(false);
            }

            Regex runtimeRegex;
            try
            {
                runtimeRegex = new Regex((string)runtimePattern.Value!, RegexOptions.CultureInvariant);
            }
            catch (ArgumentException exception)
            {
                throw new ExpressionEvaluationException("Invalid regular expression: " + exception.Message);
            }

            return ExpressionValue.From(runtimeRegex.IsMatch(ExpressionOperations.ToFalsyString(runtimeInput)));
        };
    }

    private static Evaluator CompileConcat(List<Evaluator> arguments)
    {
        var constants = new ExpressionValue[arguments.Count];
        for (int index = 0; index < arguments.Count; index++)
        {
            if (!TryGetConstant(arguments[index], out constants[index]))
            {
                return model => ExpressionOperations.EvaluateConcat(arguments, model);
            }
        }

        return Constant(ExpressionOperations.Concat(constants));
    }

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

    private static string[] SplitReference(string path)
    {
        string[] segments = path.Split('.');
        foreach (string segment in segments)
        {
            if (segment.Length == 0 || s_unsafeReferenceSegments.Contains(segment))
            {
                throw ExpressionParseException.WithoutPosition("invalid model reference: #" + path);
            }
        }

        return segments;
    }

    private sealed class ConstantEvaluator(ExpressionValue value)
    {
        public ExpressionValue Value { get; } = value;
        public ExpressionValue Evaluate(ExpressionModel _) => Value;
    }
}

internal enum NumericOperation
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    Minimum,
    Maximum,
}
