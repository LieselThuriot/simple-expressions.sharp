#nullable enable

namespace Simple.Expressions.Sharp;

public class ExpressionParseException : Exception
{
    public ExpressionParseException(string message, int position)
        : base($"Invalid Expression at {position}: {message}")
    {
        Position = position;
    }

    public int Position { get; }
}
