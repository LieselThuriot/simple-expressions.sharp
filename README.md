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

`ExecuteExpression` and `SimpleExpression.Evaluate` return expression truthiness. `ParseExpression` returns a reusable evaluator and preserves scalar results. A missing or null value is represented as `null` at the public API boundary.

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

## Differences from the TypeScript reference

This port tracks the [TypeScript reference](https://github.com/LieselThuriot/simple-expressions) for grammar, operators, and evaluation order. It deliberately does not reproduce every JavaScript coercion quirk. The differences below are intentional and covered by the shared parity corpus in `parity/cases.json`.

| Behaviour | TypeScript | This port |
| --- | --- | --- |
| String to number | `Number("")` is `0` | A string converts only if it parses as a number; `""` and `"   "` are `NaN` |
| `eq` on two arrays or objects | Reference equality, so `eq(#a, #b)` on equal-looking arrays is false | Structural equality: same length or key set, and every pair loosely equal, to a depth of 64 |

Numbers render as their shortest round-trip decimal, in plain notation for magnitudes in `[1e-6, 1e21)` and lowercase exponent notation outside it — the same rule the Dart port uses, and the same output JavaScript produces for these values.

## Model access

Models are read by reflection, on demand. Nothing is copied or converted up front, so an evaluation costs only what the expression actually reads — a small expression against a large model does not walk the parts it never references. Property getters are compiled and cached per type on first use.

Anything can be a model: a POCO, an anonymous type, a `Dictionary<string, object?>`, or any other `IDictionary`. A dictionary's keys are its members; every other object's public instance properties are.

Three things worth knowing:

- **Property names are CLR names**, matched ordinally and case-sensitively. `[JsonPropertyName]`, `[JsonIgnore]`, and naming policies are not honoured — this port has no JSON dependency. The TypeScript reference reads raw property names too.
- **Models are read live**, at evaluation time. Mutating a model between evaluations is visible to the next one.
- **Values are coerced the way JSON serialization used to render them**: enums become their numeric value, `DateTime`/`DateTimeOffset` become round-trip (`"O"`) strings, `Guid` becomes its `"D"` form, `byte[]` becomes base64.

A non-object model root (an array or a primitive) resolves every reference to a missing value.

Sequences are read lazily, so `in(...)` can stop at the first match. A consequence: a single-pass `IEnumerable` may be enumerated more than once within an evaluation. Pass a materialised collection if the sequence cannot be re-enumerated.

## Caching

Parsed evaluators and `SimpleExpression` instances are cached by default. Both caches have a FIFO limit of 1,000 entries. Use `SetCacheLimit`, `Clear`, `DisableCaches`, and `EnableCaches` to control them. Disabling caches also clears existing entries.

## Security

Expressions and regex patterns are trusted input. Regex evaluation can be vulnerable to catastrophic backtracking; do not use untrusted expressions or patterns as an authorization boundary.
