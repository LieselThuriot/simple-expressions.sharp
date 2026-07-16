# simple-expressions.sharp

C# port of [simple-expressions](https://github.com/LieselThuriot/simple-expressions), aligned with its current expression grammar and evaluation semantics.

## Usage

```csharp
using Simple.Expressions.Sharp;

var model = new { User = new { Name = "Ada" }, Score = 8 };

SimpleExpressions.ExecuteExpression(model,
    "and(eq(#User.Name, \"Ada\"), gt(#Score, 5))");
// true

var evaluator = SimpleExpressions.ParseExpression("concat(\"Hello, \", #User.Name)");
evaluator(model);
// "Hello, Ada"
```

`ExecuteExpression` and `SimpleExpression.Evaluate` return expression truthiness. `ParseExpression` returns a reusable evaluator and preserves scalar results. A missing or JSON-null value is represented as `null` at the public API boundary.

## Syntax

Constants support case-insensitive Booleans, signed/decimal/exponent numeric literals, and single-, double-, or backtick-quoted strings. References begin with `#`; dotted references first check for an exact dotted key, then traverse nested object properties. Empty segments and `__proto__`, `constructor`, and `prototype` are rejected.

Operators are case-insensitive:

`not`, `empty`, `len`, `lower`, `upper`, `eq`, `contains`, `startswith`, `endswith`, `gt`, `lt`, `match`, variadic `and`/`or`/`concat`, and lazy `if`.

The evaluator follows JavaScript truthiness, loose equality, and value conversion semantics where the .NET value model permits. `match` uses .NET regular expressions, so patterns that rely on JavaScript-specific regex syntax are not portable.

## JSON

System.Text.Json supports direct `JsonObject` values and POCO input.

## Caching

Parsed evaluators and `SimpleExpression` instances are cached by default. Both caches have a FIFO limit of 1,000 entries. Use `SetCacheLimit`, `Clear`, `DisableCaches`, and `EnableCaches` to control them. Disabling caches also clears existing entries.

## Security

Expressions and regex patterns are trusted input. Regex evaluation can be vulnerable to catastrophic backtracking; do not use untrusted expressions or patterns as an authorization boundary.
