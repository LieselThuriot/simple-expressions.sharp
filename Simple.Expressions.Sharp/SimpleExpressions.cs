namespace Simple.Expressions.Sharp;

public static class SimpleExpressions
{
    private static bool _enabledCaches = true;

    private static Dictionary<string, Func<JsonObject, SimpleType>> _parseCache = new()
    {
        { "true", _ => true },
        { "false", _ => false }
    };

    private static Dictionary<string, SimpleExpression> _simpleCache = [];

    public static bool ExecuteExpression(ModelType model, ExpressionType expression)
    {
        return Get(expression).Evaluate(model);
    }

    public static SimpleExpression Get(ExpressionType e)
    {
        var key = e.ToString();
        if (_enabledCaches)
        {
            if (_simpleCache.TryGetValue(key, out var cachedExpression))
            {
                return cachedExpression;
            }
        }

        var result = new SimpleExpression(e);

        if (_enabledCaches)
        {
            _simpleCache[key] = result;
        }

        return result;
    }

    internal static Func<JsonObject, SimpleType> GetParsedExpression(string expression, Func<string, Func<JsonObject, SimpleType>> factory)
    {
        expression = expression.Trim();

        if (string.IsNullOrEmpty(expression))
        {
            throw new Exception("Invalid Expression: formatting");
        }

        if (_enabledCaches)
        {
            if (_parseCache.TryGetValue(expression, out var cachedExpression))
            {
                return cachedExpression;
            }
        }

        var parsedResult = factory(expression);

        if (_enabledCaches)
        {
            _parseCache[expression] = parsedResult;
        }

        return parsedResult;
    }

    public static void Clear(Dictionary<string, bool> options = null)
    {
        options ??= new Dictionary<string, bool>
        {
            { "parsed", true },
            { "expression", true }
        };

        if (options.ContainsKey("parsed") && options["parsed"])
        {
            _parseCache = new Dictionary<string, Func<JsonObject, SimpleType>>
            {
                { "true", _ => true },
                { "false", _ => false }
            };
        }

        if (options.ContainsKey("expression") && options["expression"])
        {
            _simpleCache = [];
        }
    }

    public static void DisableCaches()
    {
        _enabledCaches = false;
    }

    public static void EnableCaches()
    {
        _enabledCaches = true;
    }
}
