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

#if Newton
        return new ExpressionModel(Newtonsoft.Json.Linq.JObject.FromObject(model));
#else
        return new ExpressionModel(System.Text.Json.JsonSerializer.SerializeToNode(model) as JsonObject);
#endif
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
        if (value is IReadOnlyCollection<ExpressionValue> expressionValues)
        {
            length = ExpressionValue.From((double)expressionValues.Count);
            return true;
        }

#if Newton
        if (value is Newtonsoft.Json.Linq.JArray array)
        {
            length = ExpressionValue.From((double)array.Count);
            return true;
        }

        if (value is JsonObject obj && TryGetProperty(obj, "length", out var property))
        {
            length = Convert(property);
            return true;
        }
#else
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
#endif

        length = default;
        return false;
    }

    public static bool TryGetArrayValues(ExpressionValue value, out IEnumerable<ExpressionValue> values)
    {
        if (value.Kind != ExpressionValueKind.Array)
        {
            values = Enumerable.Empty<ExpressionValue>();
            return false;
        }

        if (value.Value is IReadOnlyList<ExpressionValue> expressionValues)
        {
            values = expressionValues;
            return true;
        }

#if Newton
        if (value.Value is Newtonsoft.Json.Linq.JArray newtonArray)
        {
            values = newtonArray.Select(Convert);
            return true;
        }
#else
        if (value.Value is System.Text.Json.Nodes.JsonArray systemTextJsonArray)
        {
            values = systemTextJsonArray.Select(Convert);
            return true;
        }
#endif

        values = Enumerable.Empty<ExpressionValue>();
        return false;
    }

    public static string ArrayToString(object value)
    {
        if (value is IReadOnlyList<ExpressionValue> expressionValues)
            return string.Join(",", expressionValues.Select(item => item.Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null ? string.Empty : item.ToJavaScriptString()));

#if Newton
        if (value is Newtonsoft.Json.Linq.JArray array)
            return string.Join(",", array.Select(item => item is null ? string.Empty : Convert(item).Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null ? string.Empty : Convert(item).ToJavaScriptString()));
#else
        if (value is System.Text.Json.Nodes.JsonArray array)
            return string.Join(",", array.Select(item => item is null ? string.Empty : Convert(item).Kind is ExpressionValueKind.Missing or ExpressionValueKind.Null ? string.Empty : Convert(item).ToJavaScriptString()));
#endif
        return string.Empty;
    }

    private static bool TryGetProperty(object value, string name, out object? property)
    {
#if Newton
        if (value is JsonObject obj && obj.TryGetValue(name, out var token))
        {
            property = token;
            return true;
        }
#else
        if (value is System.Text.Json.Nodes.JsonObject obj && obj.TryGetPropertyValue(name, out var node))
        {
            property = node;
            return true;
        }
#endif
        property = null;
        return false;
    }

    private static ExpressionValue Convert(object? value)
    {
        if (value is null)
            return ExpressionValue.Null;

#if Newton
        if (value is Newtonsoft.Json.Linq.JArray array)
            return ExpressionValue.FromArray(array);
        if (value is JsonObject obj)
            return ExpressionValue.FromObject(obj);

        var token = (Newtonsoft.Json.Linq.JValue)value;
        return token.Type switch
        {
            JsonTokenType.Null => ExpressionValue.Null,
            JsonTokenType.Boolean => ExpressionValue.From(token.Value is true),
            JsonTokenType.Integer or JsonTokenType.Float => ExpressionValue.From(GetNumber(token)),
            JsonTokenType.String => ExpressionValue.From((string)token.Value!),
            _ => ExpressionValue.Null,
        };
#else
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
#endif
    }

#if Newton
    private static double GetNumber(Newtonsoft.Json.Linq.JValue value) => value.ToObject<double>();
#else
    private static double GetNumber(System.Text.Json.Nodes.JsonValue value)
    {
        if (value.TryGetValue<double>(out var doubleValue)) return doubleValue;
        if (value.TryGetValue<decimal>(out var decimalValue)) return (double)decimalValue;
        if (value.TryGetValue<long>(out var longValue)) return longValue;
        if (value.TryGetValue<ulong>(out var ulongValue)) return ulongValue;
        return value.GetValue<int>();
    }
#endif
}
