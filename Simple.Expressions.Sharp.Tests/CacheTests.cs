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
}
