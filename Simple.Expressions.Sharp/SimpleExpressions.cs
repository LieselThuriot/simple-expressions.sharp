#nullable enable

namespace Simple.Expressions;

public static class SimpleExpressions
{
    private static readonly RuntimeCache<string, Evaluator> s_parseCache = RuntimeCaches.Create<string, Evaluator>(StringComparer.Ordinal);
    private static readonly RuntimeCache<string, SimpleExpression> s_simpleCache = RuntimeCaches.Create<string, SimpleExpression>(StringComparer.Ordinal);

    public static bool ExecuteExpression(object? model, string expression) => Get(expression).Evaluate(model);

    public static bool ExecuteExpression(object? model, bool expression) => Get(expression).Evaluate(model);

    public static Func<object?, object?> ParseExpression(string expression)
    {
        var evaluator = GetParsedExpression(expression, static value => new ExpressionParser(value).Parse());
        return model => evaluator(ExpressionModel.From(model)).ToPublicValue();
    }

    public static SimpleExpression Get(string expression)
    {
        string key = "s:" + expression;
        return s_simpleCache.GetOrCreate(key, _ => new SimpleExpression(expression));
    }

    public static SimpleExpression Get(bool expression)
    {
        string key = expression ? "b:true" : "b:false";

        return s_simpleCache.GetOrCreate(key, _ => new SimpleExpression(expression));
    }

    public static void Clear(Dictionary<string, bool>? options = null)
    {
        bool clearParsed = options is null || (options.TryGetValue("parsed", out bool parsed) && parsed);
        bool clearExpressions = options is null || (options.TryGetValue("expression", out bool expression) && expression);
        if (options is null)
        {
            RuntimeCaches.ClearAll();
            return;
        }

        if (clearParsed)
        {
            RuntimeCaches.Clear(s_parseCache, RuntimeRegexCache.Cache, ModelAccessor.Cache);
        }

        if (clearExpressions)
        {
            RuntimeCaches.Clear(s_simpleCache);
        }
    }

    public static void SetCacheLimit(int limit)
    {
        RuntimeCaches.SetLimit(limit);
    }

    public static void DisableCaches()
    {
        RuntimeCaches.Disable();
    }

    public static void EnableCaches()
    {
        RuntimeCaches.Enable();
    }

    internal static Evaluator GetParsedExpression(string expression, Func<string, Evaluator> factory)
    {
        if (expression is null)
        {
            throw new ArgumentNullException(nameof(expression));
        }

        expression = expression.Trim();
        if (expression.Length == 0)
        {
            throw ExpressionParseException.WithoutPosition("formatting");
        }

        return s_parseCache.GetOrCreate(expression, factory);
    }
}
