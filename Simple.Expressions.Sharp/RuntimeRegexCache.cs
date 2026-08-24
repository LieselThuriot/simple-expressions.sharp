using System.Text.RegularExpressions;

namespace Simple.Expressions;

internal static class RuntimeRegexCache
{
    internal static readonly RuntimeCache<string, Regex> Cache = RuntimeCaches.Create<string, Regex>(StringComparer.Ordinal);

    public static Regex Get(string pattern)
    {
        return Cache.GetOrCreate(pattern, static value => new Regex(value, RegexOptions.CultureInvariant));
    }
}
