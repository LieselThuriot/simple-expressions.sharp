# simple-expressions

![simple-expressions logo](https://raw.githubusercontent.com/LieselThuriot/simple-expressions.sharp/main/simple-expressions.png)

[![NuGet](https://img.shields.io/nuget/v/simple-expressions.svg)](https://www.nuget.org/packages/simple-expressions)
[![license](https://img.shields.io/nuget/l/simple-expressions.svg)](https://www.nuget.org/packages/simple-expressions)

C# port of [simple-expressions](https://github.com/LieselThuriot/simple-expressions). It follows the reference project's current grammar and evaluation semantics.

## Install

```bash
dotnet add package simple-expressions
```

## Usage

```csharp
using Simple.Expressions;

var model = new { User = new { Name = "Ada" }, Score = 8 };

SimpleExpressions.ExecuteExpression(model,
    "and(eq(#User.Name, \"Ada\"), gt(#Score, 5))");
// true

var evaluator = SimpleExpressions.ParseExpression("concat(\"Hello, \", #User.Name)");
evaluator(model);
// "Hello, Ada"
```

`ExecuteExpression` and `SimpleExpression.Evaluate` return the expression's truthiness. `ParseExpression` returns a reusable evaluator and keeps scalar results intact. Missing and null values appear as `null` at the public API boundary.

## Demo

The interactive playground is available at [simple-expressions.liesel.dev](https://simple-expressions.liesel.dev/). It uses the same expression grammar and evaluation behavior.

To run the C# console sample from this repository:

```bash
dotnet run --project Simple.Expressions.Sharp.Sample
```

## Syntax

Constants support case-insensitive Booleans, signed, decimal, and exponent numeric literals, single-, double-, or backtick-quoted strings, and recursive list literals in square brackets. References begin with `#`. For a dotted reference, the evaluator first checks for an exact dotted key and then traverses nested object properties. References cannot contain empty segments or the reserved segments `__proto__`, `constructor`, and `prototype`; these restrictions are retained for compatibility with the TypeScript reference.

Operators are case-insensitive:

`not`, `empty`, `len`, `lower`, `upper`, `trim`, `defined`, `eq`, `contains`, `startswith`, `endswith`, `in`, `gt`, `lt`, `match`, variadic `and`/`or`/`concat`, and lazy `if`.

`in` requires a list and uses the same loose equality as `eq`. `trim` preserves null and missing values; other falsey values become empty strings before trimming. `defined` is false only for null and missing values. Lists may contain nested lists, model references, and operator expressions.

The evaluator uses JavaScript-style truthiness, loose equality, and value conversion where those rules can be represented by .NET values. `match` uses .NET regular expressions, so patterns that rely on JavaScript-specific regex syntax are not portable.

## Differences from the TypeScript reference

This port follows the [TypeScript reference](https://github.com/LieselThuriot/simple-expressions) for grammar, operators, and evaluation order. It does not reproduce every JavaScript coercion quirk. Cross-language compatibility is tested separately in [simple-expressions-conformance](https://github.com/LieselThuriot/simple-expressions-conformance).

| Behaviour | TypeScript | This port |
| --- | --- | --- |
| String to number | `Number("")` is `0` | A string converts only if it parses as a number; `""` and `"   "` are `NaN` |
| `eq` on two arrays or objects | Reference equality, so `eq(#a, #b)` on equal-looking arrays is false | Structural equality: same length or key set, and every pair loosely equal, to a depth of 64 |

Numbers use the same formatting rule as the TypeScript reference: the shortest round-trip decimal, plain notation for magnitudes in `[1e-6, 1e21)`, and lowercase exponent notation outside that range.

## Model access

Models are read by reflection when the expression asks for a value. Nothing is copied or converted up front. A small expression against a large model does not visit the parts it never references. Property getters are compiled and cached per type on first use.

Models can be POCOs, anonymous types, `Dictionary<string, object?>` values, read-only string-keyed dictionaries, non-generic dictionaries, or other objects with public instance properties. A dictionary's string keys are its members; every other object's public instance properties are.

Keep these details in mind:

- **Property names are CLR names**, matched ordinally and case-sensitively. This port reads them directly and does not apply `[JsonPropertyName]`, `[JsonIgnore]`, or naming policies. It has no JSON dependency.
- **Models are read live**, at evaluation time. Mutating a model between evaluations is visible to the next one.
- **Values use the port's established coercions**: enums become their numeric value, `DateTime`/`DateTimeOffset` become round-trip (`"O"`) strings, `Guid` becomes its `"D"` form, and `byte[]` becomes base64.

A non-object model root (an array or a primitive) resolves every reference to a missing value.

Sequences are read lazily, so `in(...)` can stop at the first match. A single-pass `IEnumerable` may still be enumerated more than once during one evaluation. Use a materialised collection when the sequence cannot be re-enumerated.

## Caching

Parsed evaluators and expressions returned by `SimpleExpressions.Get` are cached by default. Both caches have a FIFO limit of 1,000 entries. Use `SetCacheLimit`, `Clear`, `DisableCaches`, and `EnableCaches` to control them. Disabling caches also clears existing entries.

## Security

Expressions and regex patterns are trusted input. Regex evaluation can be vulnerable to catastrophic backtracking. Do not use untrusted expressions or patterns as an authorization boundary.
