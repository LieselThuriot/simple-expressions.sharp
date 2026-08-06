#nullable enable

using System.Text.RegularExpressions;

namespace Simple.Expressions;

internal delegate ExpressionValue Evaluator(ExpressionModel model);

internal enum TokenKind
{
    End,
    Identifier,
    Number,
    String,
    Reference,
    OpenParen,
    CloseParen,
    Comma,
    OpenBracket,
    CloseBracket,
}

internal sealed class ExpressionParser
{
    private static readonly HashSet<string> UnsafeReferenceSegments = new(StringComparer.Ordinal)
    {
        "__proto__", "constructor", "prototype",
    };

    private static readonly Regex NumberPattern = new(@"^[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?$", RegexOptions.CultureInvariant);
    private readonly Tokenizer _tokenizer;

    public ExpressionParser(string expression)
    {
        _tokenizer = new Tokenizer(expression);
    }

    public Evaluator Parse()
    {
        var result = ParseValue();
        if (_tokenizer.Kind != TokenKind.End)
            Fail("unexpected trailing input");
        return result;
    }

    private Evaluator ParseValue()
    {
        switch (_tokenizer.Kind)
        {
            case TokenKind.String:
            {
                var value = _tokenizer.Value;
                _tokenizer.Next();
                return Constant(ExpressionValue.From(value));
            }
            case TokenKind.Number:
            {
                var value = double.Parse(_tokenizer.Value, System.Globalization.CultureInfo.InvariantCulture);
                _tokenizer.Next();
                return Constant(ExpressionValue.From(value));
            }
            case TokenKind.Reference:
            {
                var path = _tokenizer.Value;
                var segments = SplitReference(path);
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
                var identifier = _tokenizer.Value;
                _tokenizer.Next();
                if (identifier.Equals("true", StringComparison.OrdinalIgnoreCase))
                    return Constant(ExpressionValue.From(true));
                if (identifier.Equals("false", StringComparison.OrdinalIgnoreCase))
                    return Constant(ExpressionValue.From(false));
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
            Fail("expected closing bracket");
        _tokenizer.Next();
        var items = values.ToArray();
        return model =>
        {
            var results = new ExpressionValue[items.Length];
            for (var index = 0; index < items.Length; index++)
                results[index] = items[index](model);
            return ExpressionValue.FromArray(results);
        };
    }

    private Evaluator ParseCall(string name)
    {
        if (_tokenizer.Kind != TokenKind.OpenParen)
            Fail("invalid operator: " + name);

        var operation = name.ToLowerInvariant();
        var arity = GetArity(operation);
        _tokenizer.Next();
        if (_tokenizer.Kind == TokenKind.CloseParen)
            Fail("missing operator argument");

        var arguments = new List<Evaluator> { ParseValue() };
        if (arity == -1)
        {
            if (_tokenizer.Kind != TokenKind.Comma)
                Fail("operator " + name + " expects at least two arguments");
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
                    Fail("operator " + name + " expects " + ArityText(arity));
                _tokenizer.Next();
                arguments.Add(ParseValue());
            }
        }

        if (_tokenizer.Kind != TokenKind.CloseParen)
            Fail("operator " + name + " expects " + (arity == -1 ? "at least two arguments" : ArityText(arity)));
        _tokenizer.Next();
        return Compile(operation, arguments);
    }

    private int GetArity(string operation)
    {
        return operation switch
        {
            "not" or "empty" or "len" or "lower" or "upper" or "trim" or "defined" => 1,
            "eq" or "contains" or "startswith" or "endswith" or "gt" or "lt" or "match" or "in" => 2,
            "if" => 3,
            "and" or "or" or "concat" => -1,
            _ => FailArity(operation),
        };
    }

    private int FailArity(string operation)
    {
        Fail("invalid operator: " + operation);
        return 0;
    }

    private Evaluator Compile(string operation, List<Evaluator> arguments)
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
            _ => throw new InvalidOperationException("Unknown operator: " + operation),
        };
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

    private const int RuntimePatternCacheLimit = 64;
    private static readonly Dictionary<string, Regex> RuntimePatterns = new(StringComparer.Ordinal);

    /// <summary>
    /// Compiling a <see cref="Regex"/> is expensive and a model-supplied pattern is almost
    /// always the same one every time, so keep a small bounded cache rather than rebuilding it
    /// on every evaluation. The cache is cleared wholesale when full: patterns come from model
    /// data, so an unbounded cache would be caller-controlled growth.
    /// </summary>
    private static Regex GetRuntimeRegex(string pattern)
    {
        lock (RuntimePatterns)
        {
            if (RuntimePatterns.TryGetValue(pattern, out var cached))
                return cached;
        }

        var regex = new Regex(pattern, RegexOptions.CultureInvariant);

        lock (RuntimePatterns)
        {
            if (RuntimePatterns.Count >= RuntimePatternCacheLimit)
                RuntimePatterns.Clear();
            RuntimePatterns[pattern] = regex;
        }

        return regex;
    }

    private static ExpressionValue EvaluateAnd(List<Evaluator> arguments, ExpressionModel model)
    {
        foreach (var argument in arguments)
        {
            if (!argument(model).IsTruthy())
                return ExpressionValue.From(false);
        }
        return ExpressionValue.From(true);
    }

    private static ExpressionValue EvaluateOr(List<Evaluator> arguments, ExpressionModel model)
    {
        foreach (var argument in arguments)
        {
            if (argument(model).IsTruthy())
                return ExpressionValue.From(true);
        }
        return ExpressionValue.From(false);
    }

    private static ExpressionValue EvaluateConcat(List<Evaluator> arguments, ExpressionModel model)
    {
        var result = string.Empty;
        foreach (var argument in arguments)
        {
            var value = argument(model);
            if (value.IsTruthy())
                result += value.ToJavaScriptString();
        }
        return ExpressionValue.From(result);
    }

    private static Evaluator CompileConcat(List<Evaluator> arguments)
    {
        var constants = new ExpressionValue[arguments.Count];
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!TryGetConstant(arguments[index], out constants[index]))
                return model => EvaluateConcat(arguments, model);
        }

        var result = string.Empty;
        foreach (var value in constants)
        {
            if (value.IsTruthy())
                result += value.ToJavaScriptString();
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

    private static bool IsEmpty(ExpressionValue value) => value.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null || value.Kind == ExpressionValueKind.String && ((string)value.Value!).Length == 0;
    private static string ToFalsyString(ExpressionValue value) => value.IsTruthy() ? value.ToJavaScriptString() : string.Empty;
    private static ExpressionValue Trim(ExpressionValue value)
    {
        if (value.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null)
            return value;
        return ExpressionValue.From(ToFalsyString(value).Trim());
    }

    private static bool Contains(ExpressionValue left, ExpressionValue right) => left.Kind == ExpressionValueKind.String && ((string)left.Value!).IndexOf(right.ToJavaScriptString(), StringComparison.Ordinal) >= 0;
    private static bool StartsWith(ExpressionValue left, ExpressionValue right) => left.Kind == ExpressionValueKind.String && ((string)left.Value!).StartsWith(right.ToJavaScriptString(), StringComparison.Ordinal);
    private static bool EndsWith(ExpressionValue left, ExpressionValue right) => left.Kind == ExpressionValueKind.String && ((string)left.Value!).EndsWith(right.ToJavaScriptString(), StringComparison.Ordinal);
    private static bool In(ExpressionValue value, ExpressionValue list) => ExpressionModel.TryGetArrayValues(list, out var values) && values.Any(item => LooseEquals(value, item));

    private static ExpressionValue Length(ExpressionValue value)
    {
        if (value.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null)
            return ExpressionValue.From(0d);
        if (value.Kind == ExpressionValueKind.String)
            return ExpressionValue.From((double)((string)value.Value!).Length);
        if (ExpressionModel.TryGetOwnLength(value, out var length))
            return length;
        throw new InvalidOperationException("Input does not have a length");
    }

    private const int MaxStructuralDepth = 64;

    private static bool LooseEquals(ExpressionValue left, ExpressionValue right) => LooseEquals(left, right, 0);

    private static bool LooseEquals(ExpressionValue left, ExpressionValue right, int depth)
    {
        if (left.Kind == right.Kind)
            return left.Kind switch
            {
                ExpressionValueKind.Missing or ExpressionValueKind.Null => true,
                ExpressionValueKind.Boolean => (bool)left.Value! == (bool)right.Value!,
                ExpressionValueKind.Number => (double)left.Value! == (double)right.Value!,
                ExpressionValueKind.String => (string)left.Value! == (string)right.Value!,
                ExpressionValueKind.Array => StructuralArrayEquals(left, right, depth),
                _ => StructuralObjectEquals(left, right, depth),
            };
        if ((left.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null) && (right.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null))
            return true;
        if (left.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null || right.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null)
            return false;
        if (left.Kind is ExpressionValueKind.Array or ExpressionValueKind.Object)
            return LooseEquals(ExpressionValue.From(left.ToJavaScriptString()), right);
        if (right.Kind is ExpressionValueKind.Array or ExpressionValueKind.Object)
            return LooseEquals(left, ExpressionValue.From(right.ToJavaScriptString()));
        if (left.Kind == ExpressionValueKind.Boolean || right.Kind == ExpressionValueKind.Boolean || left.Kind == ExpressionValueKind.Number && right.Kind == ExpressionValueKind.String || left.Kind == ExpressionValueKind.String && right.Kind == ExpressionValueKind.Number)
            return left.ToNumber() == right.ToNumber();
        return false;
    }

    private static bool StructuralArrayEquals(ExpressionValue left, ExpressionValue right, int depth)
    {
        if (ReferenceEquals(left.Value, right.Value))
            return true;
        if (depth >= MaxStructuralDepth)
            return false;
        if (!ExpressionModel.TryGetArrayValues(left, out var leftValues) || !ExpressionModel.TryGetArrayValues(right, out var rightValues))
            return false;

        using var leftEnumerator = leftValues.GetEnumerator();
        using var rightEnumerator = rightValues.GetEnumerator();
        while (leftEnumerator.MoveNext())
        {
            if (!rightEnumerator.MoveNext() || !LooseEquals(leftEnumerator.Current, rightEnumerator.Current, depth + 1))
                return false;
        }

        return !rightEnumerator.MoveNext();
    }

    private static bool StructuralObjectEquals(ExpressionValue left, ExpressionValue right, int depth)
    {
        if (ReferenceEquals(left.Value, right.Value))
            return true;
        if (depth >= MaxStructuralDepth)
            return false;
        if (!ExpressionModel.TryGetObjectEntries(left, out var leftEntries) || !ExpressionModel.TryGetObjectEntries(right, out var rightEntries))
            return false;

        var rightByName = new Dictionary<string, ExpressionValue>(StringComparer.Ordinal);
        foreach (var entry in rightEntries)
            rightByName[entry.Key] = entry.Value;

        var count = 0;
        foreach (var entry in leftEntries)
        {
            if (!rightByName.TryGetValue(entry.Key, out var other) || !LooseEquals(entry.Value, other, depth + 1))
                return false;
            count++;
        }

        return count == rightByName.Count;
    }

    private static int Compare(ExpressionValue left, ExpressionValue right)
    {
        if (left.Kind is ExpressionValueKind.Array or ExpressionValueKind.Object)
            left = ExpressionValue.From(left.ToJavaScriptString());
        if (right.Kind is ExpressionValueKind.Array or ExpressionValueKind.Object)
            right = ExpressionValue.From(right.ToJavaScriptString());
        if (left.Kind == ExpressionValueKind.String && right.Kind == ExpressionValueKind.String)
            return string.CompareOrdinal((string)left.Value!, (string)right.Value!);
        var leftNumber = left.ToNumber();
        var rightNumber = right.ToNumber();
        return double.IsNaN(leftNumber) || double.IsNaN(rightNumber) ? 0 : leftNumber.CompareTo(rightNumber);
    }

    private static string[] SplitReference(string path)
    {
        var segments = path.Split('.');
        foreach (var segment in segments)
        {
            if (segment.Length == 0 || UnsafeReferenceSegments.Contains(segment))
                throw ExpressionParseException.WithoutPosition("invalid model reference: #" + path);
        }

        return segments;
    }

    private static string ArityText(int arity) => arity switch { 1 => "one argument", 2 => "two arguments", _ => "three arguments" };
    private void Fail(string message) => throw new ExpressionParseException(message, _tokenizer.Position);

    private sealed class ConstantEvaluator
    {
        public ConstantEvaluator(ExpressionValue value) => Value = value;
        public ExpressionValue Value { get; }
        public ExpressionValue Evaluate(ExpressionModel _) => Value;
    }

    private sealed class Tokenizer
    {
        private readonly string _source;
        private int _index;

        public Tokenizer(string source)
        {
            _source = source;
            Next();
        }

        public TokenKind Kind { get; private set; }
        public string Value { get; private set; } = string.Empty;
        public int Position => _index;

        public void Next()
        {
            while (_index < _source.Length && _source[_index] <= ' ')
                _index++;
            if (_index == _source.Length)
            {
                Kind = TokenKind.End;
                Value = string.Empty;
                return;
            }

            var start = _index;
            switch (_source[_index])
            {
                case '(' : Set(TokenKind.OpenParen, "(", 1); return;
                case ')' : Set(TokenKind.CloseParen, ")", 1); return;
                case ',' : Set(TokenKind.Comma, ",", 1); return;
                case '[' : Set(TokenKind.OpenBracket, "[", 1); return;
                case ']' : Set(TokenKind.CloseBracket, "]", 1); return;
                case '#':
                    _index++;
                    ReadDelimited();
                    if (_index == start + 1) Fail("missing model reference");
                    Kind = TokenKind.Reference;
                    Value = _source.Substring(start + 1, _index - start - 1);
                    return;
                case '`':
                case '\'':
                case '"': ReadString(_source[_index]); return;
            }

            if (IsIdentifierLetter(_source[_index]))
            {
                _index++;
                while (_index < _source.Length && IsIdentifierLetter(_source[_index])) _index++;
                Kind = TokenKind.Identifier;
                Value = _source.Substring(start, _index - start);
                return;
            }

            ReadDelimited();
            Value = _source.Substring(start, _index - start);
            if (NumberPattern.IsMatch(Value))
            {
                Kind = TokenKind.Number;
                return;
            }
            Fail("unexpected token " + Value);
        }

        private void ReadDelimited()
        {
            while (_index < _source.Length && _source[_index] is not '(' and not ')' and not ',' and not '[' and not ']' && _source[_index] > ' ')
                _index++;
        }

        private void ReadString(char quote)
        {
            var start = ++_index;
            var slashCount = 0;
            while (_index < _source.Length)
            {
                var character = _source[_index];
                if (character == '\\') { slashCount++; _index++; continue; }
                if (character == quote && slashCount % 2 == 0)
                {
                    Kind = TokenKind.String;
                    Value = _source.Substring(start, _index - start);
                    _index++;
                    return;
                }
                slashCount = 0;
                _index++;
            }
            Fail("unterminated string");
        }

        private void Set(TokenKind kind, string value, int length)
        {
            Kind = kind;
            Value = value;
            _index += length;
        }

        private static bool IsIdentifierLetter(char character) => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

        private void Fail(string message) => throw new ExpressionParseException(message, _index);
    }
}
