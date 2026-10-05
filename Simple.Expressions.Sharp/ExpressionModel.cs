#nullable enable

namespace Simple.Expressions;

/// <summary>
/// Wraps the caller's model. Nothing is copied or converted up front: references are resolved
/// against the live object when an expression asks for them, so the cost of an evaluation is
/// proportional to what the expression reads rather than to the size of the model.
/// </summary>
internal sealed class ExpressionModel
{
    private readonly object? _root;

    private ExpressionModel(object? root)
    {
        _root = root;
    }

    public static ExpressionModel From(object? model) => new(model);

    /// <summary>Resolves a reference with no dots in it.</summary>
    public ExpressionValue ResolveSingle(string name)
    {
        if (_root is null)
        {
            return ExpressionValue.Missing;
        }

        return ModelAccessor.TryGetMember(_root, name, out object? value) ? ClrValueAdapter.From(value) : ExpressionValue.Missing;
    }

    /// <summary>
    /// Resolves a dotted reference. A member whose name is literally the dotted path wins over
    /// traversal, matching upstream.
    /// </summary>
    public ExpressionValue Resolve(string path, string[] segments)
    {
        if (_root is null)
        {
            return ExpressionValue.Missing;
        }

        if (ModelAccessor.TryGetMember(_root, path, out object? dotted))
        {
            return ClrValueAdapter.From(dotted);
        }

        object? value = _root;
        foreach (string segment in segments)
        {
            if (value is null || !ModelAccessor.TryGetMember(value, segment, out value))
            {
                return ExpressionValue.Missing;
            }
        }

        return ClrValueAdapter.From(value);
    }
}