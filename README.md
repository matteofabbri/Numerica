# SuperNumbers

[![CI](https://github.com/OWNER/SuperNumbers/actions/workflows/ci.yml/badge.svg)](https://github.com/OWNER/SuperNumbers/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com)

**Exact, lazy, expression-backed numbers for .NET** — built from a formula string,
evaluated only when asked, behind a single public type (`Numeric`) that implements
`INumber<T>`.

A small C# project about **exact numbers**, built as a tower:

```
BigInteger -> BigRational -> BigIrrational -> BigComplex
 (exact)      (exact ==)     (symbolic tree)   (complex)

         Numeric  (front door: build from a formula string, evaluate lazily,
                   INumber<T>; == and < exact & decidable for algebraic formulas)
```

It starts from a real question — *C# has `BigInteger`, why not a `BigFloat`?* — and
follows the answer all the way to closed-form reals. The full reasoning and the
relevant papers are in [DOCS.md](DOCS.md).

## Under the hood

`Numeric` (below) is the **only public type** — everything in this section is
`internal`, the machinery it drives.

1. **`BigRational`** — exact `num/den` in lowest terms. Closed under `+ - * /` and
   integer powers, so `==` is *genuinely exact*: `1/3 + 1/6` is exactly `1/2`.

2. **`BigIrrational`** — a rational pair cannot capture `sqrt(2)` (an irrational is,
   by definition, not a ratio of integers). So we store the **operation**, not a
   value: an exact **symbolic tree** of sums, products and powers with rational
   exponents, plus `pi`, `e` and the transcendental functions `exp`, `ln`, `sin`,
   `cos`, `tan`, `atan`. Smart constructors simplify as they build, so identities
   cancel **symbolically** (`sqrt(2)*sqrt(2) -> 2`, `phi^2 - phi - 1 -> 0`). The
   value is materialized only on demand: `Approximate(bits)` returns a rational
   within `2^-bits`.

3. **`BigComplex`** — real and imaginary parts are each a `BigIrrational`, so
   identities stay exact (`i^2 = -1`, `|3+4i| = 5`) and the usual complex functions
   (`Exp`, `Ln`, `Sin`, `Cos`, `Sqrt`, powers) are available — including Euler's
   `exp(i*pi) + 1 = 0`.

There is also a **decidable middle ground** — algebraic numbers (roots of integer
polynomials), represented as a squarefree annihilating polynomial + an isolating
rational interval, where `==` and `<` are exact and decidable. That engine is
internal (`Parsing/AlgebraicReal`); you reach it through `Numeric` (below), whose
comparisons it makes exact for algebraic formulas. `pi` and `e` (transcendental) do
not live there.

## `Numeric` — the front door

`Numeric` is the type you reach for first. You build it from a **formula string**;
it stays a suspended calculation (it holds the expression tree) and only **becomes a
value when asked**. It implements .NET's generic-math `INumber<Numeric>`, so it drops
into standard operators and algorithms, and converts to whichever level fits:

```csharp
var n = new Numeric("sqrt(2) * sqrt(2)");
n.ToDecimalString(30);   // "2.000000000000000000000000000000"
n.IsRational;            // true  (decided exactly: the value is 2)
n.IsIrrational;          // false
n.IsComplex;             // false

new Numeric("2 + 3*i").IsComplex;                  // true
new Numeric("(1 + sqrt(5)) / 2").IsIrrational;     // true
new Numeric("sqrt(2)") < new Numeric("sqrt(3)");   // true
Numeric total = Numeric.One + new Numeric("1/2");  // composes via INumber<T>
```

`IsRational` / `IsIrrational` / `IsComplex` are the public way to ask what kind of
number you have. (The concrete `BigRational` / `BigIrrational` / `BigComplex` values
stay internal.)

`==` and `<` are **exact and decidable** when both formulas are algebraic
(rationals, `+ - * /`, integer powers, roots of rationals): the comparison drops to
an internal minimal-polynomial + isolating-interval engine, so `sqrt(2)*sqrt(2) == 2`
is *decided* true. For transcendental formulas (`pi`, `e`, `exp`, `ln`, trig), where
exact equality is undecidable, it falls back to a high-precision numeric comparison.

## One tree, three levels, parsed from strings

`Expr` is a single expression tree that evaluates at **all three** value levels —
`ToRational()`, `ToIrrational()`, `ToComplex()` — and strings map to it
automatically via [Sprache](https://github.com/sprache/Sprache):

```csharp
Expr.Parse("1/3 + 1/6").ToRational();        // 1/2
Expr.Parse("sqrt(2) * sqrt(2)").ToIrrational(); // "2"
Expr.Parse("exp(i*pi) + 1").ToComplex();      // ~ 0
```

## The theoretical limit

**Exact** `==` on closed-form reals is undecidable (Richardson's theorem): if two
values were equal, a bit-by-bit comparison would never terminate. So:

- `BigRational` has exact, **decidable** equality, and `Numeric` extends that to all
  **algebraic** formulas (via the internal `AlgebraicReal` engine).
- `BigIrrational`'s simplifier reaches exact answers *when the structure cancels*;
  otherwise it compares only up to a chosen precision
  (`CompareApprox` / `ApproximatelyEquals`).

## Running

Requires the .NET 10 SDK or later.

```bash
dotnet test                                         # the regression suite (xUnit)
dotnet run --project SAMPLES/SuperNumbers.BasicSample
```

## Layout

- `SuperNumbers/`
  - `Numeric.cs` — the front-door type (formula string, lazy, `INumber<Numeric>`)
  - `BigRational.cs` — exact rationals (the floor of the tower)
  - `BigIrrational.cs` — the symbolic tree + the numeric engine
  - `BigComplex.cs` — complex numbers over `BigIrrational`
  - `Parsing/`
    - `Expr.cs` — the universal expression tree (three evaluators)
    - `ExpressionParser.cs` — the Sprache grammar (string -> tree)
    - `AlgebraicReal.cs` — internal algebraic-number engine (decidable `==`/`<`)
    - `Polynomial.cs` — rational-coefficient polynomials + Sturm machinery
    - `RealMath.cs` — fixed-point exp / ln / sin / cos / atan
- `SAMPLES/SuperNumbers.BasicSample/` — a short `Numeric` demo
- `TEST/SuperNumbers.Tests/` — the xUnit regression suite
- `DOCS/DOCS.md` — design notes and references to the relevant papers

## Documentation

- [Expression language reference](DOCS/expression-language.md) — every operator,
  function, constant and literal form you can put in a formula string.
- [Design notes & references](DOCS/DOCS.md) — the theory and the papers behind it.
- [Roadmap](ROADMAP.md) — where this could go next.

## Contributing

Contributions are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md). Bug reports and
ideas go through the GitHub issue templates.

## License

[MIT](LICENSE).
