# SuperNumbers

A small C# project about **exact numbers**, built as a tower:

```
BigInteger -> BigRational -> BigAlgebraic -> BigIrrational -> BigComplex
 (exact)      (exact ==)    (decidable ==)   (symbolic tree)   (complex)
```

It starts from a real question — *C# has `BigInteger`, why not a `BigFloat`?* — and
follows the answer all the way to closed-form reals. The full reasoning and the
relevant papers are in [DOCS.md](DOCS.md).

## The types

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

4. **`BigAlgebraic`** — the **decidable middle ground**: a root of an integer
   polynomial, stored as a squarefree annihilating `Polynomial` + an isolating
   rational interval. Here `==` and `<` are **exact and decidable** (`sqrt(2)*sqrt(2)
   == 2` is *decided* true; `sqrt(2) < sqrt(3)` is decided). Arithmetic builds the
   minimal polynomial of the result via the regular representation. The price:
   `pi` and `e` (transcendental) do not live here.

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

- `BigRational` and `BigAlgebraic` have exact, **decidable** equality.
- `BigIrrational`'s simplifier reaches exact answers *when the structure cancels*;
  otherwise it compares only up to a chosen precision
  (`CompareApprox` / `ApproximatelyEquals`).

## Running

Requires the .NET 8 SDK or later.

```bash
dotnet run                 # the demo
dotnet test                # the regression suite (xUnit)
```

## Files

- `BigRational.cs` — exact rationals (the floor of the tower)
- `BigAlgebraic.cs` — algebraic numbers with decidable `==`/`<`
- `Polynomial.cs` — rational-coefficient polynomials + Sturm machinery
- `BigIrrational.cs` — the symbolic tree + the numeric engine
- `RealMath.cs` — fixed-point exp / ln / sin / cos / atan
- `BigComplex.cs` — complex numbers over `BigIrrational`
- `Expr.cs` — the universal expression tree (three evaluators)
- `ExpressionParser.cs` — the Sprache grammar (string -> tree)
- `Program.cs` — demo of all of the above
- `DOCS.md` — design notes and references to the relevant papers
- `tests/SuperNumbers.Tests` — xUnit regression tests
```
