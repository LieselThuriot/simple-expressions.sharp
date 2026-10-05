#nullable enable

namespace Simple.Expressions;

/// <summary>
/// The error raised when a parsed expression fails while it is evaluated.
/// </summary>
#pragma warning disable RCS1194 // Implement exception constructors
public class ExpressionEvaluationException(string message) : InvalidOperationException(message);
#pragma warning restore RCS1194 // Implement exception constructors
