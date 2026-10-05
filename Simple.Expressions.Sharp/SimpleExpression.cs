#nullable enable

namespace Simple.Expressions;

public sealed class SimpleExpression
{
    private readonly Evaluator _evaluator;

    public SimpleExpression(bool expression)
    {
        _evaluator = _ => ExpressionValue.From(expression);
    }

    public SimpleExpression(string expression)
    {
        // Upstream's SimpleExpression constructor reports "whitespace";
        // "formatting" is reserved for parseExpression.
        if (expression is null || ExpressionText.Trim(expression).Length == 0)
        {
            throw ExpressionParseException.WithoutPosition("whitespace");
        }

        _evaluator = SimpleExpressions.GetParsedExpression(expression, static value => new ExpressionParser(value).Parse());
    }

    internal SimpleExpression(Evaluator evaluator)
    {
        _evaluator = evaluator;
    }

    public bool Evaluate(object? model) => _evaluator(ExpressionModel.From(model)).IsTruthy();

    public object? EvaluateValue(object? model) => _evaluator(ExpressionModel.From(model)).ToPublicValue();
}
