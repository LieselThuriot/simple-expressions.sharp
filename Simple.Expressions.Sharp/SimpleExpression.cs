#nullable enable

namespace Simple.Expressions;

public sealed class SimpleExpression
{
    private readonly Evaluator _evaluator;

    public SimpleExpression(ExpressionType expression)
    {
        _evaluator = expression.SwitchCase(
            static value => _ => ExpressionValue.From(value),
            static expression => Parse(expression),
            onMismatch: static () => new ArgumentException("Expression must be a Boolean or string.")
        );

        static Evaluator Parse(string expression)
        {
            // Upstream's SimpleExpression constructor reports "whitespace";
            // "formatting" is reserved for parseExpression.
            if (expression is null || expression.Trim().Length == 0)
                throw ExpressionParseException.WithoutPosition("whitespace");

            return SimpleExpressions.GetParsedExpression(expression, static value => new ExpressionParser(value).Parse());
        }
    }

    internal SimpleExpression(Evaluator evaluator)
    {
        _evaluator = evaluator;
    }

    public bool Evaluate(ModelType model)
    {
        var input = model.SwitchCase<object?>(static json => json, static value => value);
        return _evaluator(ExpressionModel.From(input)).IsTruthy();
    }

    public bool Evaluate(object? model) => _evaluator(ExpressionModel.From(model)).IsTruthy();

    public object? EvaluateValue(object? model) => _evaluator(ExpressionModel.From(model)).ToPublicValue();
}
