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

    public SimpleExpression(ExpressionType expression)
    {
        if (expression.IsFirst)
        {
            _parsedExpression = model => expression.First;
        }
        else if (expression.IsSecond)
        {
            if (string.IsNullOrEmpty(expression.Second))
            {
                throw new Exception("Invalid Expression: empty");
            }

            var strExpression = expression.Second.Replace("\r\n", "").Replace("\n", "").Replace("\r", "").Trim();

            if (string.IsNullOrEmpty(strExpression))
            {
                throw new Exception("Invalid Expression: whitespace");
            }

            if (strExpression.Contains('.'))
            {
                _needsFlattening = true;
            }

            _parsedExpression = ExpressionParser.ParseExpression(strExpression);
        }
        else
        {
            throw new Exception("Invalid Expression: unsupported type " + expression.CurrentType);
        }
    }

    public bool Evaluate(ModelType model)
    {
        JsonObject m = model.IsFirst ? model.First :

#if Newton
           JsonObject.FromObject(model.Second);
#else
           (JsonObject)System.Text.Json.JsonSerializer.SerializeToNode(model.Second);
#endif

        if (_needsFlattening)
        {
            m = FlattenObject(m);
        }

        return _parsedExpression(m).Falsy();
    }
}