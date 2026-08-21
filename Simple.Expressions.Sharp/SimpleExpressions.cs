#nullable enable

namespace Simple.Expressions;

public static class SimpleExpressions
{
    private const int DefaultCacheLimit = 1000;
    private static readonly object s_cacheLock = new();
    private static bool s_enabledCaches = true;
    private static int s_cacheLimit = DefaultCacheLimit;
    private static readonly FifoCache<Evaluator> s_parseCache = new();
    private static readonly FifoCache<SimpleExpression> s_simpleCache = new();

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

        lock (s_cacheLock)
        {
            if (s_enabledCaches && s_simpleCache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        SimpleExpression result = new(expression);

        lock (s_cacheLock)
        {
            if (s_enabledCaches)
            {
                SetCacheValue(s_simpleCache, key, result);
            }
        }

        return result;
    }

    public static SimpleExpression Get(bool expression)
    {
        string key = expression ? "b:true" : "b:false";

        lock (s_cacheLock)
        {
            if (s_enabledCaches && s_simpleCache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        SimpleExpression result = new(expression);

        lock (s_cacheLock)
        {
            if (s_enabledCaches)
            {
                SetCacheValue(s_simpleCache, key, result);
            }
        }

        return result;
    }

    public static void Clear(Dictionary<string, bool>? options = null)
    {
        bool clearParsed = options is null || (options.TryGetValue("parsed", out bool parsed) && parsed);
        bool clearExpressions = options is null || (options.TryGetValue("expression", out bool expression) && expression);
        lock (s_cacheLock)
        {
            if (clearParsed)
            {
                s_parseCache.Clear();
                RuntimeRegexCache.Clear();
            }

            if (clearExpressions)
            {
                s_simpleCache.Clear();
            }
        }
    }

    public static void SetCacheLimit(int limit)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Cache limit must be at least 1.");
        }

        lock (s_cacheLock)
        {
            s_cacheLimit = limit;
            TrimCache(s_parseCache);
            TrimCache(s_simpleCache);
        }
    }

    public static void DisableCaches()
    {
        lock (s_cacheLock)
        {
            s_enabledCaches = false;
            s_parseCache.Clear();
            s_simpleCache.Clear();
        }
    }

    public static void EnableCaches()
    {
        lock (s_cacheLock)
        {
            s_enabledCaches = true;
        }
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

        lock (s_cacheLock)
        {
            if (s_enabledCaches && s_parseCache.TryGetValue(expression, out var cached))
            {
                return cached;
            }
        }

        var result = factory(expression);
        lock (s_cacheLock)
        {
            if (s_enabledCaches)
            {
                SetCacheValue(s_parseCache, expression, result);
            }
        }

        return result;
    }

    private static void SetCacheValue<T>(FifoCache<T> cache, string key, T value)
    {
        if (!cache.ContainsKey(key) && cache.Count >= s_cacheLimit)
        {
            cache.EvictOldest();
        }

        cache.Set(key, value);
    }

    private static void TrimCache<T>(FifoCache<T> cache)
    {
        while (cache.Count > s_cacheLimit)
        {
            cache.EvictOldest();
        }
    }

    /// <summary>
    /// A string-keyed cache with insertion-ordered eviction, matching the upstream
    /// <c>Map</c>-based caches. <see cref="Dictionary{TKey,TValue}"/> alone cannot be used
    /// because its enumeration order is undefined once entries have been removed.
    /// </summary>
    private sealed class FifoCache<T>
    {
        private readonly Dictionary<string, LinkedListNode<KeyValuePair<string, T>>> _entries = new(StringComparer.Ordinal);
        private readonly LinkedList<KeyValuePair<string, T>> _order = new();

        public int Count => _entries.Count;

        public bool ContainsKey(string key) => _entries.ContainsKey(key);

        public bool TryGetValue(string key, out T value)
        {
            if (_entries.TryGetValue(key, out var node))
            {
                value = node.Value.Value;
                return true;
            }

            value = default!;
            return false;
        }

        public void Set(string key, T value)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                // Replacing a value keeps its original insertion position, as Map.set does.
                existing.Value = new KeyValuePair<string, T>(key, value);
                return;
            }

            _entries[key] = _order.AddLast(new KeyValuePair<string, T>(key, value));
        }

        public void EvictOldest()
        {
            var oldest = _order.First;
            if (oldest is null)
            {
                return;
            }

            _order.RemoveFirst();
            _entries.Remove(oldest.Value.Key);
        }

        public void Clear()
        {
            _entries.Clear();
            _order.Clear();
        }
    }
}
