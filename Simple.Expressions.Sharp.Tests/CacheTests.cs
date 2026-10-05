using SE = Simple.Expressions.SimpleExpressions;

namespace Simple.Expressions.Sharp.Tests;

public sealed class CacheTests
{
    public CacheTests()
    {
        SE.EnableCaches();
        SE.SetCacheLimit(1000);
        SE.Clear();
    }

    [Fact]
    public void CacheLimitAndDisableCacheFollowThePublicContract()
    {
        SE.SetCacheLimit(1);
        SimpleExpression first = SE.Get("true");
        SE.Get("false");
        Assert.NotSame(first, SE.Get("true"));

        SE.DisableCaches();
        Assert.NotSame(SE.Get("true"), SE.Get("true"));
    }

    [Fact]
    public void BooleanCacheKeysShareLowercaseStringEntries()
    {
        Assert.Same(SE.Get(true), SE.Get("true"));
        Assert.Same(SE.Get(false), SE.Get("false"));
        Assert.NotSame(SE.Get(true), SE.Get("True"));
    }

    [Fact]
    public void ClearOptionsSelectCachesWithoutRestoringTheOldDictionaryApi()
    {
        Func<object?, object?> parsed = SE.ParseExpression("eq(1, 1)");
        SimpleExpression expression = SE.Get("eq(1, 1)");

        SE.Clear(new CacheClearOptions(parsed: true));

        Assert.NotSame(parsed, SE.ParseExpression("eq(1, 1)"));
        Assert.Same(expression, SE.Get("eq(1, 1)"));
    }

    [Fact]
    public void SettingPositiveCacheLimitDoesNotReenableCaching()
    {
        SE.DisableCaches();
        SE.SetCacheLimit(10);

        Assert.NotSame(SE.Get("true"), SE.Get("true"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidCacheLimitsThrowWithoutChangingCacheState(int limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SE.SetCacheLimit(limit));

        SE.EnableCaches();
        Assert.Throws<ArgumentOutOfRangeException>(() => SE.SetCacheLimit(limit));
        Assert.Same(SE.Get("true"), SE.Get("true"));
    }

    [Fact]
    public async Task ConcurrentGetsCreateOneWrapper()
    {
        using var start = new ManualResetEventSlim();
        Task<SimpleExpression>[] tasks = [.. Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
        {
            start.Wait();
            return SE.Get("and(true, true)");
        }))];

        start.Set();
        SimpleExpression[] values = await Task.WhenAll(tasks);
        Assert.All(values, value => Assert.Same(values[0], value));
    }

    [Fact]
    public async Task ConcurrentCacheMissesRunOneFactory()
    {
        var cache = new RuntimeCache<string, object>(StringComparer.Ordinal);
        using var factoryStarted = new ManualResetEventSlim();
        using var releaseFactory = new ManualResetEventSlim();
        int calls = 0;
        Task<object>[] tasks = [.. Enumerable.Range(0, 16).Select(_ => Task.Run(() => cache.GetOrCreate("key", _ =>
        {
            Interlocked.Increment(ref calls);
            factoryStarted.Set();
            releaseFactory.Wait();
            return new object();
        })))];

        factoryStarted.Wait(TestContext.Current.CancellationToken);
        releaseFactory.Set();
        object[] values = await Task.WhenAll(tasks);

        Assert.Equal(1, calls);
        Assert.All(values, value => Assert.Same(values[0], value));
    }

    [Fact]
    public void FailedFactoriesDoNotPoisonCacheEntries()
    {
        var cache = new RuntimeCache<string, int>(StringComparer.Ordinal);
        int calls = 0;

        Assert.Throws<InvalidOperationException>(() => cache.GetOrCreate("key", _ =>
        {
            calls++;
            throw new InvalidOperationException();
        }));

        Assert.Equal(7, cache.GetOrCreate("key", _ => { calls++; return 7; }));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void FactoryCannotDeadlockByRequestingItsOwnKey()
    {
        var cache = new RuntimeCache<string, int>(StringComparer.Ordinal);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            cache.GetOrCreate("key", key => cache.GetOrCreate(key, _ => 1)));

        Assert.Equal("A cache factory cannot recursively create the same key.", exception.Message);
        Assert.Equal(2, cache.GetOrCreate("key", _ => 2));
    }

    [Fact]
    public async Task FactoriesCannotDeadlockByWaitingOnEachOther()
    {
        var first = new RuntimeCache<string, int>(StringComparer.Ordinal);
        var second = new RuntimeCache<string, int>(StringComparer.Ordinal);
        using var factoriesStarted = new CountdownEvent(2);

        Task<int> firstTask = Task.Run(() => first.GetOrCreate("first", _ =>
        {
            factoriesStarted.Signal();
            factoriesStarted.Wait(TestContext.Current.CancellationToken);
            return second.GetOrCreate("second", _ => 2);
        }));
        Task<int> secondTask = Task.Run(() => second.GetOrCreate("second", _ =>
        {
            factoriesStarted.Signal();
            factoriesStarted.Wait(TestContext.Current.CancellationToken);
            return first.GetOrCreate("first", _ => 1);
        }));

        Task<int[]> completion = Task.WhenAll(firstTask, secondTask);
        Assert.Same(completion, await Task.WhenAny(completion, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)));
        int[] results = await completion;
        Assert.All(results, result => Assert.InRange(result, 1, 2));
    }

    [Fact]
    public async Task ClearDuringFactoryDoesNotRepopulateStaleEntry()
    {
        var cache = new RuntimeCache<string, int>(StringComparer.Ordinal);
        using var factoryStarted = new ManualResetEventSlim();
        using var releaseFactory = new ManualResetEventSlim();
        int calls = 0;
        Task<int> stale = Task.Run(() => cache.GetOrCreate("key", _ =>
        {
            calls++;
            factoryStarted.Set();
            releaseFactory.Wait();
            return 1;
        }));

        factoryStarted.Wait(TestContext.Current.CancellationToken);
        RuntimeCaches.Clear(cache);
        releaseFactory.Set();

        Assert.Equal(1, await stale);
        Assert.Equal(2, cache.GetOrCreate("key", _ => { calls++; return 2; }));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void ClearDoesNotInvalidateExistingConstantOrDynamicRegexEvaluators()
    {
        Func<object?, object?> constant = SE.ParseExpression("match(#value, \"^a\")");
        Func<object?, object?> dynamic = SE.ParseExpression("match(#value, #pattern)");

        Assert.True((bool)constant(new { value = "abc" })!);
        Assert.True((bool)dynamic(new { value = "abc", pattern = "^a" })!);
        SE.Clear();
        Assert.True((bool)constant(new { value = "abc" })!);
        Assert.True((bool)dynamic(new { value = "abc", pattern = "^a" })!);
    }
}