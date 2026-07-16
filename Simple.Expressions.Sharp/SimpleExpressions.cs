#nullable enable

namespace Simple.Expressions.Sharp;

public static class SimpleExpressions
{
    private const int DefaultCacheLimit = 1000;
    private static readonly object CacheLock = new();
    private static bool _enabledCaches = true;
    private static int _cacheLimit = DefaultCacheLimit;
    private static readonly Dictionary<string, Evaluator> ParseCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, SimpleExpression> SimpleCache = new(StringComparer.Ordinal);

    public static bool ExecuteExpression(ModelType model, ExpressionType expression) => Get(expression).Evaluate(model);

    public static bool ExecuteExpression(object? model, string expression) => Get(expression).Evaluate(model);

    public static bool ExecuteExpression(object? model, bool expression) => Get(expression).Evaluate(model);

    public static Func<object?, object?> ParseExpression(string expression)
    {
        var evaluator = GetParsedExpression(expression, static value => new ExpressionParser(value).Parse());
        return model => evaluator(ExpressionModel.From(model)).ToPublicValue();
    }

    public static SimpleExpression Get(ExpressionType expression)
    {
        var key = expression.ToString();
        lock (CacheLock)
        {
            if (_enabledCaches && SimpleCache.TryGetValue(key, out var cached))
                return cached;
        }

        var result = new SimpleExpression(expression);
        lock (CacheLock)
        {
            if (_enabledCaches)
                SetCacheValue(SimpleCache, key, result);
        }
        return result;
    }

    public static void Clear(Dictionary<string, bool>? options = null)
    {
        var clearParsed = options is null || options.TryGetValue("parsed", out var parsed) && parsed;
        var clearExpressions = options is null || options.TryGetValue("expression", out var expression) && expression;
        lock (CacheLock)
        {
            if (clearParsed) ParseCache.Clear();
            if (clearExpressions) SimpleCache.Clear();
        }
    }

    public static void SetCacheLimit(int limit)
    {
        if (limit <= 0)
        {
            DisableCaches();
            return;
        }

        lock (CacheLock)
        {
            _cacheLimit = limit;
            TrimCache(ParseCache);
            TrimCache(SimpleCache);
        }
    }

    public static void DisableCaches()
    {
        lock (CacheLock)
        {
            _enabledCaches = false;
            ParseCache.Clear();
            SimpleCache.Clear();
        }
    }

    public static void EnableCaches()
    {
        lock (CacheLock)
        {
            _enabledCaches = true;
        }
    }

    internal static Evaluator GetParsedExpression(string expression, Func<string, Evaluator> factory)
    {
        if (expression is null) throw new ArgumentNullException(nameof(expression));
        expression = expression.Trim();
        if (expression.Length == 0) throw new ExpressionParseException("formatting", 0);

        lock (CacheLock)
        {
            if (_enabledCaches && ParseCache.TryGetValue(expression, out var cached))
                return cached;
        }

        var result = factory(expression);
        lock (CacheLock)
        {
            if (_enabledCaches)
                SetCacheValue(ParseCache, expression, result);
        }
        return result;
    }

    private static void SetCacheValue<T>(Dictionary<string, T> cache, string key, T value)
    {
        if (cache.Count >= _cacheLimit)
            cache.Remove(cache.Keys.First());
        cache[key] = value;
    }

    private static void TrimCache<T>(Dictionary<string, T> cache)
    {
        while (cache.Count > _cacheLimit)
            cache.Remove(cache.Keys.First());
    }
}
