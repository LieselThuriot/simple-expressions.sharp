using SE = Simple.Expressions.SimpleExpressions;

namespace Simple.Expressions.Tests;

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
        var first = SE.Get("true");
        SE.Get("false");
        Assert.NotSame(first, SE.Get("true"));

        SE.DisableCaches();
        Assert.NotSame(SE.Get("true"), SE.Get("true"));
    }

    [Fact]
    public void CacheKeysKeepBooleanAndStringExpressionsSeparate()
    {
        var boolean = SE.Get(true);
        var text = SE.Get("True");

        Assert.NotSame(boolean, text);
        Assert.True(boolean.Evaluate(new { }));
        Assert.True(text.Evaluate(new { }));
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
        var tasks = Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
        {
            start.Wait();
            return SE.Get("and(true, true)");
        })).ToArray();

        start.Set();
        var values = await Task.WhenAll(tasks);
        Assert.All(values, value => Assert.Same(values[0], value));
    }

    [Fact]
    public async Task ConcurrentCacheMissesRunOneFactory()
    {
        var cache = new RuntimeCache<string, object>(StringComparer.Ordinal);
        using var factoryStarted = new ManualResetEventSlim();
        using var releaseFactory = new ManualResetEventSlim();
        int calls = 0;
        var tasks = Enumerable.Range(0, 16).Select(_ => Task.Run(() => cache.GetOrCreate("key", _ =>
        {
            Interlocked.Increment(ref calls);
            factoryStarted.Set();
            releaseFactory.Wait();
            return new object();
        }))).ToArray();

        factoryStarted.Wait(TestContext.Current.CancellationToken);
        releaseFactory.Set();
        var values = await Task.WhenAll(tasks);

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

        var exception = Assert.Throws<InvalidOperationException>(() =>
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

        var firstTask = Task.Run(() => first.GetOrCreate("first", _ =>
        {
            factoriesStarted.Signal();
            factoriesStarted.Wait(TestContext.Current.CancellationToken);
            return second.GetOrCreate("second", _ => 2);
        }));
        var secondTask = Task.Run(() => second.GetOrCreate("second", _ =>
        {
            factoriesStarted.Signal();
            factoriesStarted.Wait(TestContext.Current.CancellationToken);
            return first.GetOrCreate("first", _ => 1);
        }));

        var completion = Task.WhenAll(firstTask, secondTask);
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
        var stale = Task.Run(() => cache.GetOrCreate("key", _ =>
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
    public void ClearResetsReflectionAndDynamicRegexCachesWithoutChangingConstantRegexes()
    {
        var constant = SE.ParseExpression("match(#value, \"^a\")");
        var dynamic = SE.ParseExpression("match(#value, #pattern)");

        Assert.True((bool)constant(new { value = "abc" })!);
        Assert.True((bool)dynamic(new { value = "abc", pattern = "^a" })!);
        SE.Clear();
        Assert.True((bool)constant(new { value = "abc" })!);
        Assert.True((bool)dynamic(new { value = "abc", pattern = "^a" })!);
    }
}
