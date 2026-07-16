#nullable enable

using System.Text.RegularExpressions;

namespace Simple.Expressions.Sharp;

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
                ValidateReference(path);
                _tokenizer.Next();
                return model => model.Resolve(path);
            }
            case TokenKind.Identifier:
            {
                var identifier = _tokenizer.Value;
                _tokenizer.Next();
                if (identifier.Equals("true", StringComparison.OrdinalIgnoreCase))
                    return Constant(ExpressionValue.From(true));
                if (identifier.Equals("false", StringComparison.OrdinalIgnoreCase))
                    return Constant(ExpressionValue.From(false));
                if (identifier.Equals("null", StringComparison.OrdinalIgnoreCase))
                    return Constant(ExpressionValue.Null);
                return ParseCall(identifier);
            }
            default:
                Fail("expected an expression");
                return null!;
        }
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
                    Fail("operator " + name + " expects " + ArityText(arity) + " arguments");
                _tokenizer.Next();
                arguments.Add(ParseValue());
            }
        }

        if (_tokenizer.Kind != TokenKind.CloseParen)
            Fail("operator " + name + " expects " + (arity == -1 ? "at least two" : ArityText(arity)) + " arguments");
        _tokenizer.Next();
        return Compile(operation, arguments);
    }

    private int GetArity(string operation)
    {
        return operation switch
        {
            "not" or "empty" or "len" or "lower" or "upper" => 1,
            "eq" or "contains" or "startswith" or "endswith" or "gt" or "lt" or "match" => 2,
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
            "eq" => model => ExpressionValue.From(LooseEquals(arguments[0](model), arguments[1](model))),
            "contains" => model => ExpressionValue.From(Contains(arguments[0](model), arguments[1](model))),
            "startswith" => model => ExpressionValue.From(StartsWith(arguments[0](model), arguments[1](model))),
            "endswith" => model => ExpressionValue.From(EndsWith(arguments[0](model), arguments[1](model))),
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
            return ExpressionValue.From(runtimePattern.Kind == ExpressionValueKind.String && runtimePattern.IsTruthy() && new Regex((string)runtimePattern.Value!, RegexOptions.CultureInvariant).IsMatch(ToFalsyString(input(model))));
        };
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
    private static bool Contains(ExpressionValue left, ExpressionValue right) => left.Kind == ExpressionValueKind.String && ((string)left.Value!).IndexOf(right.ToJavaScriptString(), StringComparison.Ordinal) >= 0;
    private static bool StartsWith(ExpressionValue left, ExpressionValue right) => left.Kind == ExpressionValueKind.String && ((string)left.Value!).StartsWith(right.ToJavaScriptString(), StringComparison.Ordinal);
    private static bool EndsWith(ExpressionValue left, ExpressionValue right) => left.Kind == ExpressionValueKind.String && ((string)left.Value!).EndsWith(right.ToJavaScriptString(), StringComparison.Ordinal);

    private static ExpressionValue Length(ExpressionValue value)
    {
        if (value.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null)
            return ExpressionValue.From(0d);
        if (value.Kind == ExpressionValueKind.String)
            return ExpressionValue.From((double)((string)value.Value!).Length);
        if ((value.Kind is ExpressionValueKind.Array or ExpressionValueKind.Object) && ExpressionModel.TryGetOwnLength(value.Value!, out var length))
            return length;
        throw new InvalidOperationException("Input does not have a length");
    }

    private static bool LooseEquals(ExpressionValue left, ExpressionValue right)
    {
        if (left.Kind == right.Kind)
            return left.Kind switch
            {
                ExpressionValueKind.Missing or ExpressionValueKind.Null => true,
                ExpressionValueKind.Boolean => (bool)left.Value! == (bool)right.Value!,
                ExpressionValueKind.Number => (double)left.Value! == (double)right.Value!,
                ExpressionValueKind.String => (string)left.Value! == (string)right.Value!,
                _ => ReferenceEquals(left.Value, right.Value),
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

    private static void ValidateReference(string path)
    {
        foreach (var segment in path.Split('.'))
        {
            if (segment.Length == 0 || UnsafeReferenceSegments.Contains(segment))
                throw new ExpressionParseException("invalid model reference: #" + path, 0);
        }
    }

    private static string ArityText(int arity) => arity switch { 1 => "one", 2 => "two", _ => "three" };
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
            while (_index < _source.Length && _source[_index] is not '(' and not ')' and not ',' && _source[_index] > ' ')
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
