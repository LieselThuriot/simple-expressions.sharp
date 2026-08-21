using Simple.Expressions;

var model = new
{
    User = new { Name = "Ada", Role = "engineer" },
    Score = 8,
    AllowedRoles = new[] { "editor", "engineer" }
};

bool isReady = SimpleExpressions.ExecuteExpression(model, "and(eq(#User.Name, \"Ada\"), gt(#Score, 5))");
object? greeting = SimpleExpressions.ParseExpression("concat(\"Hello, \", #User.Name, \"!\")")(model);
object? status = SimpleExpressions.ParseExpression("if(and(gt(#Score, 5), lt(#Score, 10)), \"ready\", \"keep practicing\")")(model);
bool hasAccess = SimpleExpressions.ExecuteExpression(model, "in(#User.Role, #AllowedRoles)");
object? addition = SimpleExpressions.ParseExpression("add(#Score, 3)")(model);

Console.WriteLine($"Ready: {isReady}");
Console.WriteLine($"Greeting: {greeting}");
Console.WriteLine($"Status: {status}");
Console.WriteLine($"Role allowed: {hasAccess}");
Console.WriteLine($"Addition: {addition}");