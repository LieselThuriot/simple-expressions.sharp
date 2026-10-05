#nullable enable

namespace Simple.Expressions;

internal static class RuntimeCaches
{
    public const int DefaultLimit = 1000;

    private static readonly List<IRuntimeCache> s_caches = [];
    [ThreadStatic]
    private static int s_factoryDepth;

    internal static object Sync { get; } = new();
    internal static bool Enabled { get; private set; } = true;
    internal static int Limit { get; private set; } = DefaultLimit;
    internal static bool IsCreating => s_factoryDepth != 0;

    internal static void EnterFactory() => s_factoryDepth++;
    internal static void ExitFactory() => s_factoryDepth--;

    public static RuntimeCache<TKey, TValue> Create<TKey, TValue>(IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        var cache = new RuntimeCache<TKey, TValue>(comparer);
        lock (Sync)
        {
            s_caches.Add(cache);
        }

        return cache;
    }

    public static void SetLimit(int limit)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Cache limit must be at least 1.");
        }

        lock (Sync)
        {
            Limit = limit;
            foreach (var cache in s_caches)
            {
                cache.Trim();
            }
        }
    }

    public static void Clear(params IRuntimeCache[] caches)
    {
        lock (Sync)
        {
            foreach (var cache in caches)
            {
                cache.Clear();
            }
        }
    }

    public static void ClearAll()
    {
        lock (Sync)
        {
            foreach (var cache in s_caches)
            {
                cache.Clear();
            }
        }
    }

    public static void Disable()
    {
        lock (Sync)
        {
            Enabled = false;
            foreach (var cache in s_caches)
            {
                cache.Clear();
            }
        }
    }

    public static void Enable()
    {
        lock (Sync)
        {
            Enabled = true;
        }
    }
}

internal interface IRuntimeCache
{
    public void Clear();
    public void Trim();
}

internal sealed class RuntimeCache<TKey, TValue> : IRuntimeCache
    where TKey : notnull
{
    private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _entries;
    private readonly Dictionary<TKey, PendingValue> _pending;
    private readonly LinkedList<KeyValuePair<TKey, TValue>> _order = new();
    private int _generation;

    internal RuntimeCache(IEqualityComparer<TKey>? comparer)
    {
        _entries = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>(comparer);
        _pending = new Dictionary<TKey, PendingValue>(comparer);
    }

    public TValue GetOrCreate(TKey key, Func<TKey, TValue> factory)
    {
        PendingValue? pending = null;
        bool runFactory;
        bool cacheResult;

        lock (RuntimeCaches.Sync)
        {
            cacheResult = RuntimeCaches.Enabled;
            if (!cacheResult)
            {
                runFactory = true;
            }
            else if (_entries.TryGetValue(key, out var cached))
            {
                return cached.Value.Value;
            }
            else if (_pending.TryGetValue(key, out pending))
            {
                if (pending.OwnerThreadId == Environment.CurrentManagedThreadId)
                {
                    throw new InvalidOperationException("A cache factory cannot recursively create the same key.");
                }

                // A factory must not wait on another factory: two factories can otherwise
                // deadlock by requesting each other's pending keys.
                runFactory = RuntimeCaches.IsCreating;
                cacheResult = !runFactory;
            }
            else
            {
                pending = new PendingValue(_generation);
                _pending.Add(key, pending);
                runFactory = true;
            }
        }

        if (!runFactory)
        {
            return pending!.Wait();
        }

        if (!cacheResult)
        {
            return InvokeFactory(key, factory);
        }

        try
        {
            TValue value = InvokeFactory(key, factory);
            lock (RuntimeCaches.Sync)
            {
                RemovePending(key, pending!);
                if (RuntimeCaches.Enabled && pending!.Generation == _generation)
                {
                    Set(key, value);
                }
            }

            pending!.Complete(value);
            return value;
        }
        catch (Exception exception)
        {
            lock (RuntimeCaches.Sync)
            {
                RemovePending(key, pending!);
            }

            pending!.Fail(exception);
            throw;
        }
    }

    private void RemovePending(TKey key, PendingValue pending)
    {
        if (_pending.TryGetValue(key, out var current) && ReferenceEquals(current, pending))
        {
            _pending.Remove(key);
        }
    }

    private static TValue InvokeFactory(TKey key, Func<TKey, TValue> factory)
    {
        RuntimeCaches.EnterFactory();
        try
        {
            return factory(key);
        }
        finally
        {
            RuntimeCaches.ExitFactory();
        }
    }

    public void Clear()
    {
        _generation++;
        _entries.Clear();
        _order.Clear();
        _pending.Clear();
    }

    public void Trim()
    {
        while (_entries.Count > RuntimeCaches.Limit)
        {
            EvictOldest();
        }
    }

    private void Set(TKey key, TValue value)
    {
        if (_entries.TryGetValue(key, out var existing))
        {
            existing.Value = new KeyValuePair<TKey, TValue>(key, value);
            return;
        }

        if (_entries.Count >= RuntimeCaches.Limit)
        {
            EvictOldest();
        }

        _entries.Add(key, _order.AddLast(new KeyValuePair<TKey, TValue>(key, value)));
    }

    private void EvictOldest()
    {
        var oldest = _order.First;
        if (oldest is null)
        {
            return;
        }

        _order.RemoveFirst();
        _entries.Remove(oldest.Value.Key);
    }

    private sealed class PendingValue(int generation)
    {
        private readonly TaskCompletionSource<TValue> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Generation { get; } = generation;
        public int OwnerThreadId { get; } = Environment.CurrentManagedThreadId;

        public void Complete(TValue value) => _completion.SetResult(value);

        public void Fail(Exception exception) => _completion.SetException(exception);

#pragma warning disable VSTHRD002 // This cache has a synchronous API; continuations run asynchronously.
        public TValue Wait() => _completion.Task.GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }
}