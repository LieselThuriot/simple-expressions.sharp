#nullable enable

namespace Simple.Expressions;

#pragma warning disable RCS1194 // Implement exception constructors
public class ExpressionParseException : Exception
#pragma warning restore RCS1194 // Implement exception constructors
{
    public ExpressionParseException(string message, int position)
        : base($"Invalid Expression at {position}: {message}")
    {
        Position = position;
    }

    private ExpressionParseException(string message)
        : base($"Invalid Expression: {message}")
    {
        Position = -1;
    }

    /// <summary>
    /// Creates an exception for a failure that has no meaningful source position,
    /// matching the upstream <c>Invalid Expression: {message}</c> format.
    /// </summary>
    public static ExpressionParseException WithoutPosition(string message) => new(message);

    /// <summary>
    /// The zero-based source position of the failure, or <c>-1</c> when unknown.
    /// </summary>
    public int Position { get; }
}