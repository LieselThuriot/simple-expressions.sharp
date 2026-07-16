#nullable enable

namespace Simple.Expressions.Sharp;

internal sealed class ExpressionModel
{
    private readonly JsonObject? _root;

    private ExpressionModel(JsonObject? root)
    {
        _root = root;
    }

    public static ExpressionModel From(object? model)
    {
        if (model is null)
            return new ExpressionModel(null);

        if (model is JsonObject json)
            return new ExpressionModel(json);

        return new ExpressionModel(System.Text.Json.JsonSerializer.SerializeToNode(model) as JsonObject);
    }

    public ExpressionValue Resolve(string path)
    {
        if (_root is null)
            return ExpressionValue.Missing;

        var segments = path.Split('.');
        if (segments.Length == 1)
            return TryGetProperty(_root, path, out var direct) ? Convert(direct) : ExpressionValue.Missing;

        if (TryGetProperty(_root, path, out var dotted))
            return Convert(dotted);

        object? value = _root;
        foreach (var segment in segments)
        {
            if (value is null || !TryGetProperty(value, segment, out value))
                return ExpressionValue.Missing;
        }

        return Convert(value);
    }

    public static bool TryGetOwnLength(object value, out ExpressionValue length)
    {
        if (value is System.Text.Json.Nodes.JsonArray array)
        {
            length = ExpressionValue.From((double)array.Count);
            return true;
        }

        if (value is System.Text.Json.Nodes.JsonObject obj && TryGetProperty(obj, "length", out var property))
        {
            length = Convert(property);
            return true;
        }

        length = default;
        return false;
    }

    public static string ArrayToString(object value)
    {
        if (value is System.Text.Json.Nodes.JsonArray array)
            return string.Join(",", array.Select(item => item is null ? string.Empty : Convert(item).Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null ? string.Empty : Convert(item).ToJavaScriptString()));
        return string.Empty;
    }

    private static bool TryGetProperty(object value, string name, out object? property)
    {
        if (value is System.Text.Json.Nodes.JsonObject obj && obj.TryGetPropertyValue(name, out var node))
        {
            property = node;
            return true;
        }
        property = null;
        return false;
    }

    private static ExpressionValue Convert(object? value)
    {
        if (value is null)
            return ExpressionValue.Null;

        if (value is System.Text.Json.Nodes.JsonArray array)
            return ExpressionValue.FromArray(array);
        if (value is System.Text.Json.Nodes.JsonObject obj)
            return ExpressionValue.FromObject(obj);

        var node = (System.Text.Json.Nodes.JsonValue)value;
        return node.GetValueKind() switch
        {
            System.Text.Json.JsonValueKind.Null => ExpressionValue.Null,
            System.Text.Json.JsonValueKind.True => ExpressionValue.From(true),
            System.Text.Json.JsonValueKind.False => ExpressionValue.From(false),
            System.Text.Json.JsonValueKind.Number => ExpressionValue.From(GetNumber(node)),
            System.Text.Json.JsonValueKind.String => ExpressionValue.From(node.GetValue<string>()!),
            _ => ExpressionValue.Null,
        };
    }

    private static double GetNumber(System.Text.Json.Nodes.JsonValue value)
    {
        if (value.TryGetValue<double>(out var doubleValue)) return doubleValue;
        if (value.TryGetValue<decimal>(out var decimalValue)) return (double)decimalValue;
        if (value.TryGetValue<long>(out var longValue)) return longValue;
        if (value.TryGetValue<ulong>(out var ulongValue)) return ulongValue;
        return value.GetValue<int>();
    }
}
