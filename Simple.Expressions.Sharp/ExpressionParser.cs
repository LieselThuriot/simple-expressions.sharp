#nullable enable

namespace Simple.Expressions;

internal delegate ExpressionValue Evaluator(ExpressionModel model);

internal sealed class ExpressionParser(string expression)
{
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
                return ExpressionCompiler.Constant(ExpressionValue.From(value));
            }
            case TokenKind.Number:
            {
                if (!ExpressionText.TryParseFiniteDecimal(_tokenizer.Value, out double value))
                {
                    throw ExpressionParseException.WithoutPosition("Invalid numeric value");
                }

                _tokenizer.Next();
                return ExpressionCompiler.Constant(ExpressionValue.From(value));
            }
            case TokenKind.Reference:
            {
                string path = _tokenizer.Value;
                _tokenizer.Next();
                return ExpressionCompiler.Reference(path);
            }
            case TokenKind.OpenBracket:
                return ParseList();
            case TokenKind.Identifier:
            {
                string identifier = _tokenizer.Value;
                _tokenizer.Next();
                if (identifier.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    return ExpressionCompiler.Constant(ExpressionValue.From(true));
                }

                if (identifier.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    return ExpressionCompiler.Constant(ExpressionValue.From(false));
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
            return ExpressionCompiler.List([]);
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
        return ExpressionCompiler.List(values);
    }

    private Evaluator ParseCall(string name)
    {
        if (_tokenizer.Kind != TokenKind.OpenParen)
        {
            Fail("invalid operator: " + name);
        }

        string operation = name.ToLowerInvariant();
        if (!ExpressionCompiler.TryGetOperatorSpec(operation, out var operatorSpec))
        {
            Fail("invalid operator: " + operation);
        }

        _tokenizer.Next();
        if (_tokenizer.Kind == TokenKind.CloseParen)
        {
            Fail("missing operator argument");
        }

        var arguments = new List<Evaluator> { ParseValue() };
        if (operatorSpec.Arity == ExpressionCompiler.VariadicArity)
        {
            if (_tokenizer.Kind != TokenKind.Comma)
            {
                if (_tokenizer.Kind != TokenKind.CloseParen || !operatorSpec.AllowsSingleListArgument)
                {
                    Fail("operator " + name + " expects at least two arguments");
                }

                _tokenizer.Next();
                return ExpressionCompiler.Operation(operation, arguments);
            }

            while (_tokenizer.Kind == TokenKind.Comma)
            {
                _tokenizer.Next();
                arguments.Add(ParseValue());
            }
        }
        else
        {
            while (arguments.Count < operatorSpec.Arity)
            {
                if (_tokenizer.Kind != TokenKind.Comma)
                {
                    Fail("operator " + name + " expects " + ArityText(operatorSpec.Arity));
                }

                _tokenizer.Next();
                arguments.Add(ParseValue());
            }
        }

        if (_tokenizer.Kind != TokenKind.CloseParen)
        {
            Fail("operator " + name + " expects " + (operatorSpec.Arity == ExpressionCompiler.VariadicArity ? "at least two arguments" : ArityText(operatorSpec.Arity)));
        }

        _tokenizer.Next();
        return ExpressionCompiler.Operation(operation, arguments);
    }

    private static string ArityText(int arity) => arity switch { 1 => "one argument", 2 => "two arguments", _ => "three arguments" };
    private void Fail(string message) => throw new ExpressionParseException(message, _tokenizer.Position);
}