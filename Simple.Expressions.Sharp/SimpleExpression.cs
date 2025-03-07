using System.Text.RegularExpressions;

namespace Simple.Expressions.Sharp;

public sealed class SimpleExpression
{
    private readonly Func<JsonObject, SimpleType> _parsedExpression;

    private readonly bool _needsFlattening = false;

    private JsonObject FlattenObject(JsonObject ob)
    {
        JsonObject toReturn = new();

        foreach (var kvp in ob)
        {
            if (kvp.Value is JsonObject nestedDict)
            {
                foreach (var nestedKvp in FlattenObject(nestedDict))
                {
                    toReturn[kvp.Key + "." + nestedKvp.Key] = nestedKvp.Value.DeepClone();
                }
            }
            else
            {
                toReturn[kvp.Key] = kvp.Value.DeepClone();
            }
        }

        return toReturn;
    }

    private static readonly Regex s_newLineReplacer = new("\r\n|\n|\r", RegexOptions.Compiled);
    public SimpleExpression(ExpressionType expression)
    {
        (_parsedExpression, _needsFlattening) = expression.SwitchCase(
            static @bool => (model => @bool, false),
            static @string =>
            {
                if (string.IsNullOrEmpty(@string))
                {
                    throw new Exception("Invalid Expression: empty");
                }

                var strExpression = s_newLineReplacer.Replace(@string, "").Trim();

                if (string.IsNullOrEmpty(strExpression))
                {
                    throw new Exception("Invalid Expression: whitespace");
                }

                return (ExpressionParser.ParseExpression(strExpression), strExpression.Contains('.'));
            },
            onMismatch: () => new Exception("Invalid Expression: unsupported type " + expression.CurrentType)
        );
    }

    public bool Evaluate(ModelType model)
    {
        JsonObject m = model.SwitchCase(
            json => json,
            @object =>
#if Newton
           JsonObject.FromObject(@object);
#else
           (JsonObject)System.Text.Json.JsonSerializer.SerializeToNode(@object)
#endif
            );

        if (_needsFlattening)
        {
            m = FlattenObject(m);
        }

        return _parsedExpression(m).Falsy();
    }
}