namespace Simple.Expressions;

public readonly struct CacheClearOptions(bool? parsed = null, bool? expression = null)
{
    public bool? Parsed { get; } = parsed;
    public bool? Expression { get; } = expression;
}