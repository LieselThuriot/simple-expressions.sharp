using System.Text.RegularExpressions;

namespace Simple.Expressions;

internal static class RuntimeRegexCache
{
    private const int Capacity = 64;
    private static readonly Dictionary<string, Regex> s_patterns = new(StringComparer.Ordinal);

    public static Regex Get(string pattern)
    {
        lock (s_patterns)
        {
            if (s_patterns.TryGetValue(pattern, out var cached))
            {
                return cached;
            }
        }

        var regex = new Regex(pattern, RegexOptions.CultureInvariant);
        lock (s_patterns)
        {
            if (s_patterns.Count >= Capacity)
            {
                s_patterns.Clear();
            }

            s_patterns[pattern] = regex;
        }

        return regex;
    }

    public static void Clear()
    {
        lock (s_patterns)
        {
            s_patterns.Clear();
        }
    }
}
