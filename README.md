# simple-expressions

![simple-expressions logo](https://raw.githubusercontent.com/LieselThuriot/simple-expressions.sharp/main/simple-expressions.png)

[![NuGet](https://img.shields.io/nuget/v/simple-expressions.svg)](https://www.nuget.org/packages/simple-expressions)
[![license](https://img.shields.io/nuget/l/simple-expressions.svg)](https://www.nuget.org/packages/simple-expressions)

C# port of [simple-expressions](https://github.com/LieselThuriot/simple-expressions), aligned with its current expression grammar and evaluation semantics.

## Install

```bash
dotnet add package simple-expressions
```

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

## Demo

Try the interactive playground at [lieselthuriot.github.io/simple-expressions](https://lieselthuriot.github.io/simple-expressions/). The demo showcases the shared expression grammar and evaluation behavior.

To run the C# console sample from this repository:

```bash
dotnet run --project Simple.Expressions.Sharp.Sample
```

## Syntax

Constants support case-insensitive Booleans, signed/decimal/exponent numeric literals, single-, double-, or backtick-quoted strings, and recursive list literals in square brackets. References begin with `#`; dotted references first check for an exact dotted key, then traverse nested object properties. Empty segments and `__proto__`, `constructor`, and `prototype` are rejected.

Operators are case-insensitive:

`not`, `empty`, `len`, `lower`, `upper`, `trim`, `defined`, `eq`, `contains`, `startswith`, `endswith`, `in`, `gt`, `lt`, `match`, variadic `and`/`or`/`concat`, and lazy `if`.

`in` requires a list and uses the same loose equality as `eq`. `trim` preserves null and missing values and converts other falsey values to empty strings before trimming. `defined` is false only for null and missing values. Lists can contain nested lists, model references, and operator expressions.

The evaluator follows JavaScript truthiness, loose equality, and value conversion semantics where the .NET value model permits. `match` uses .NET regular expressions, so patterns that rely on JavaScript-specific regex syntax are not portable.

## JSON Backends

System.Text.Json is the default build. Build with `-p:Newton=true` for Newtonsoft.Json. Both variants support direct JSON objects and POCO input.

## Caching

Parsed evaluators and `SimpleExpression` instances are cached by default. Both caches have a FIFO limit of 1,000 entries. Use `SetCacheLimit`, `Clear`, `DisableCaches`, and `EnableCaches` to control them. Disabling caches also clears existing entries.

## Security

Expressions and regex patterns are trusted input. Regex evaluation can be vulnerable to catastrophic backtracking; do not use untrusted expressions or patterns as an authorization boundary.
