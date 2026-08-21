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
}