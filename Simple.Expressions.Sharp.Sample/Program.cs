using Simple.Expressions;

var model = new
{
    User = new { Name = "Ada", Role = "engineer" },
    Score = 8,
    AllowedRoles = new[] { "editor", "engineer" }
};

var isReady = SimpleExpressions.ExecuteExpression(
    model,
    "and(eq(#User.Name, \"Ada\"), gt(#Score, 5))");
var greeting = SimpleExpressions.ParseExpression(
    "concat(\"Hello, \", #User.Name, \"!\")")(model);
var status = SimpleExpressions.ParseExpression(
    "if(and(gt(#Score, 5), lt(#Score, 10)), \"ready\", \"keep practicing\")")(model);
var hasAccess = SimpleExpressions.ExecuteExpression(
    model,
    "in(#User.Role, #AllowedRoles)");

Console.WriteLine($"Ready: {isReady}");
Console.WriteLine($"Greeting: {greeting}");
Console.WriteLine($"Status: {status}");
Console.WriteLine($"Role allowed: {hasAccess}");
