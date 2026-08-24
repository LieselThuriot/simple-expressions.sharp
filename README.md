# simple-expressions

![simple-expressions logo](https://raw.githubusercontent.com/LieselThuriot/simple-expressions.sharp/main/simple-expressions.png)

[![NuGet](https://img.shields.io/nuget/v/simple-expressions.svg)](https://www.nuget.org/packages/simple-expressions)
[![license](https://img.shields.io/github/license/LieselThuriot/simple-expressions.sharp.svg)](https://github.com/LieselThuriot/simple-expressions.sharp/blob/main/LICENSE)

C# port of [simple-expressions](https://github.com/LieselThuriot/simple-expressions). It implements the shared v1 contract with documented CLR and .NET regular-expression differences.

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

`ExecuteExpression` and `SimpleExpression.Evaluate` return the expression's truthiness. `ParseExpression` returns a reusable evaluator and keeps scalar results intact. `new SimpleExpression(string or bool)` is also available for direct use; call `EvaluateValue` for the raw result. `SimpleExpressions.Get` returns a cached reusable expression. Missing and null values appear as `null` at the public API boundary.

The library targets `netstandard2.0`.

## Demo

The interactive playground is available at [simple-expressions.liesel.dev](https://simple-expressions.liesel.dev/). It demonstrates the TypeScript reference implementation; the C# port follows the shared grammar with documented CLR and .NET differences.

To run the C# console sample from this repository:

```bash
dotnet run --project Simple.Expressions.Sharp.Sample
```

## Syntax

Constants support case-insensitive Booleans, signed, decimal, and exponent numeric literals, single-, double-, or backtick-quoted strings, and recursive list literals in square brackets. References begin with `#`. For a dotted reference, the evaluator first checks for an exact dotted key and then traverses nested object properties. References cannot contain empty segments or the reserved segments `__proto__`, `constructor`, and `prototype`; these restrictions are retained for compatibility with the TypeScript reference.

Operators are case-insensitive. `and` and `or` are variadic and short-circuit. `if` is lazy and evaluates only its selected branch. `in` requires a list; `contains`, `startswith`, and `endswith` require a string left operand. `len` returns `0` for nullish values, a string/array length, or an exact `length` property, and throws otherwise.

`not`, `empty`, `defined`, `lower`, `upper`, `trim`, `int`, `num`, `abs`, `floor`, `ceil`, and `sqrt` take one argument. `num` accepts scalar values only and rejects lists. `eq`, `contains`, `startswith`, `endswith`, `in`, `gt`, `lt`, `match`, `round`, and `pow` take two. `clamp` and `if` take three. `concat` takes two or more. Each of `add`, `sub`, `mul`, `div`, `mod`, `min`, and `max` accepts a single list expression and recursively flattens nested lists depth-first. `sub`, `div`, and `mod` require at least two flattened values; `min` and `max` require one.

`in` requires a list and uses the same loose equality as `eq`. `trim` preserves null and missing values; other falsey values become empty strings before trimming. `defined` is false only for null and missing values. Lists may contain nested lists, model references, and operator expressions.

Numeric operators accept finite numeric values and finite numeric strings as scalar leaves. They throw `Invalid numeric value` for booleans, null or missing values, objects, scalar lists, blank or invalid strings, nonfinite inputs, and active enumerable cycles. Arithmetic results may be nonfinite. `add([])` is zero and `mul([])` is one. `div` and `mod` throw `Division by zero` for zero divisors. `round` requires a non-negative safe integer precision.

The evaluator uses JavaScript-style truthiness, loose equality, and value conversion where those rules can be represented by .NET values. `match` uses .NET regular expressions, so patterns that rely on JavaScript-specific regex syntax are not portable. Empty or non-string patterns return `false`.

## Differences from the TypeScript reference

This port follows the [TypeScript reference](https://github.com/LieselThuriot/simple-expressions) for grammar, operators, and evaluation order. The main portability difference is the .NET regex engine and CLR model representation. Cross-language compatibility is tested separately in [simple-expressions-conformance](https://github.com/LieselThuriot/simple-expressions-conformance).

| Behaviour | TypeScript | This port |
| --- | --- | --- |
| Regex engine | JavaScript regular expressions | .NET regular expressions |
| Unicode casing | JavaScript Unicode casing | .NET invariant casing |
| Numeric strings | JavaScript `Number` grammar | Invariant .NET floating-point grammar; prefixes such as `0x`, `0b`, and `0o` are not supported |

Numbers use the same formatting rule as the TypeScript reference: the shortest round-trip decimal, plain notation for magnitudes in `[1e-6, 1e21)`, and lowercase exponent notation outside that range.

Structural equality applies to lists and objects and is cycle-safe. Loose equality remains non-throwing; strict numeric conversion is used by numeric operators.

## Model access

Models are read by reflection when the expression asks for a value. Nothing is copied or converted up front. A small expression against a large model does not visit the parts it never references. Property access strategies are cached per type; getters are compiled when supported and otherwise use reflection.

Models can be POCOs, anonymous types, `Dictionary<string, object?>` values, read-only string-keyed dictionaries, non-generic dictionaries, or other objects with public instance properties. A dictionary's string keys are its members; every other object's public instance properties are.

Keep these details in mind:

- **Property names are CLR names**, matched ordinally and case-sensitively. This port reads them directly and does not apply `[JsonPropertyName]`, `[JsonIgnore]`, or naming policies. It has no JSON dependency.
- **Dictionary keys follow the dictionary's comparer.** Use `StringComparer.Ordinal` for TypeScript-like exact matching.
- **References do not traverse list indexes.** Numeric path segments are member names, not indexes, so expressions such as `#items.0` and `#items.length` are not portable to this implementation. Lists remain supported by `len`, `in`, structural equality, and numeric list expansion.
- **Models are read live**, at evaluation time. Mutating a model between evaluations is visible to the next one.
- **Values use the port's established coercions**: numeric primitives become `double`, enums become their numeric value, `char` becomes a string, `DateTime`/`DateTimeOffset` become round-trip (`"O"`) strings, `Guid` becomes its `"D"` form, `Uri` becomes its original string, and `byte[]` becomes base64.

A model can be a dictionary or any object with public instance properties. Arrays, strings, and other non-null CLR values may expose public properties when used as roots; missing property names still resolve as missing.

Sequences are read lazily, so `in(...)` can stop at the first match. A single-pass `IEnumerable` may still be enumerated more than once during one evaluation. Use a materialised collection when the sequence cannot be re-enumerated.

## Caching

Parsed evaluators, expressions returned by `SimpleExpressions.Get`, dynamic regular expressions, and reflection accessors are cached by default. All caches share the FIFO limit of 1,000 entries; cache hits do not refresh FIFO order. Concurrent misses for one key share a single factory result, while failed factories are retried. Use `Clear()` or `Clear` with `parsed` and `expression` option keys, `SetCacheLimit` to change the shared limit, and `DisableCaches`/`EnableCaches` to control cache contents and state. A full clear and disabling caches clear every runtime cache. Constant regex patterns are compiled eagerly into their parsed evaluator and therefore remain usable when runtime caches are cleared.

## Security

Expressions and regex patterns are trusted input. Regex evaluation can be vulnerable to catastrophic backtracking. Do not use untrusted expressions or patterns as an authorization boundary.

## Conformance

This package supports conformance specification v1. The pinned corpus is documented in `CONFORMANCE.md` and initialized at `conformance`.
