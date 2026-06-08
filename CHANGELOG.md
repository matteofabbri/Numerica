# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to
follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Euler–Mascheroni constant** `egamma` (also `gamma` / `γ`, `γ ≈ 0.57722`), evaluated
  to arbitrary precision via the Brent–McMillan algorithm — validated to 50 digits.
- **Catalan's constant** `catalan` (`G ≈ 0.91597`), via the geometrically-convergent
  series `G = (π/8)·ln(2+√3) + (3/8)·Σ 1/((2k+1)²·C(2k,k))` — arbitrary precision,
  validated to 50 digits.
- **Implicit multiplication** by juxtaposition: a factor next to one that begins with a
  letter or `(` multiplies — `2pi`, `2(x+1)`, `2sqrt(2)`, `(a)(b)`, `3pi^2`. It never
  triggers on a bare number or a leading `-`, so `2 2` is a syntax error and `2 - 3`
  stays subtraction; it is left-associative at the `*` level (`1/2pi == (1/2)·pi`).
- **`pow(x, y)`** function — sugar for `x ^ y`, sharing the same power evaluation across
  the tower. Real/irrational exponents go through `exp(y·ln x)`; a negative base with a
  real exponent (e.g. `(-2)^pi`) evaluates on the complex level, and `i^i == exp(-pi/2)`.
- **Complex-level `asin`/`acos`/`atan`** — the inverse-trig functions now evaluate for
  complex arguments (principal values via logarithms); `atan2` stays real-only. A real
  formula whose value leaves the reals (`sqrt(-1)`, `ln(-1)`, `asin(2)`) is now promoted
  to its complex value (`i`, `i·pi`, `pi/2 - i·ln(2 + sqrt(3))`) instead of throwing.
- **More typed literals**: `rational(3/7)` (exact fraction), `complex(3+4i)` (a complex
  number built from existing nodes, decimals allowed), `hex(FF)`/`bin(0b1010)`/`oct(17)`
  (integers in base 16/2/8, optional prefix and `_` separators), `timespan(1:00:00)`
  (tick count) and `guid(...)` (the 128 bits as a big-endian integer).
- **Public value accessors** `TryGetRational(out BigInteger num, out BigInteger den)`
  and `TryGetInteger(out BigInteger value)` — exact, reduced, BCL-typed (no internal
  number type leaks). Stronger than `IsRational`: they also recognise a rational hidden
  in an algebraic form, e.g. `sqrt(2)*sqrt(2)` → `2/1`.
- **Rounding family** `floor`, `ceil`, `round` (halves away from zero), `trunc` and
  `sign`. Exact on rationals; on an irrational argument the integer is decided from a
  2⁻²⁵⁶ approximation, so `floor(pi)` is `3` — exact for any non-adversarial value (a
  value within that tolerance of an integer is undecidable in general).
- **Postfix factorial `n!`** — exact, for non-negative integers; sugar for `fact(n)`
  (also spelled `factorial(n)`). Binds tighter than `^` and unary minus, so `2^3!` is
  `2^(3!)` and `-3!` is `-(3!)`. There is no double-factorial: `n!!` means `(n!)!`.
- **Value-based `GetHashCode`** — `Numeric` now hashes its *value* instead of returning
  a constant, so equal numbers written differently (`2/6` and `1/3`, `sqrt(2)` and
  `sqrt(8)/2`) share a bucket and work as dictionary/set keys. Consistent with `==` up
  to the same precision `==` itself uses (exact consistency is undecidable for
  transcendentals — Richardson's theorem).
- **`ToValueString()`** — a compact, round-trippable serialization of the *computed*
  value rather than the original formula: a reduced `numerator/denominator` when the
  value collapses to a rational (so `2/6` → `1/3`, `sqrt(2)*sqrt(2)` → `2`), falling
  back to the formula for irrational/complex values. Reload with `new Numeric(text)`.
- **Modulo operator `%`** (rational remainder, sign of the dividend) at the same
  precedence as `*` and `/`, and the **variadic reductions** `min`, `max`, `gcd`,
  `lcm` plus the function form `mod(a, b)`.
- **Rounding family** `floor`, `ceil`, `round` (to nearest), `trunc` (toward zero) and
  `sign` (`-1`/`0`/`1`), each returning an exact integer.
- **Multi-argument functions** in the formula language: function calls now take a
  comma-separated argument list, e.g. `atan2(y, x)`, `root(x, n)`, `logb(x, base)`
  and `log(x, base)`.
- New functions: `cbrt`, `root`, `log10`, `log2`, `logb`, `asin`, `acos`, `atan2`,
  `sinh`, `cosh`, `tanh`, `asinh`, `acosh`, `atanh`. The inverse-hyperbolic and the
  rest also evaluate on the complex level; `atan2` is real-only.
- New constants: `tau`/`τ` (`2·pi`) and `phi`/`φ` (the golden ratio, kept symbolic so
  identities such as `phi^2 - phi - 1 == 0` stay exact).

### Fixed

- Identifiers may now contain digits after the first letter, so names like `log2`,
  `log10` and `atan2` parse as a single token instead of being split.

## [0.1.0] - 2026-06-08

First public preview.

### Added

- **`Numeric`** — the single public type: a lazy, expression-backed number built
  from a formula string that becomes a value only when asked. Implements
  `INumber<Numeric>`.
- Classification properties `IsRational`, `IsIrrational`, `IsComplex`.
- Exact, **decidable** `==` and `<` for algebraic formulas (minimal polynomial +
  isolating interval), with a high-precision numeric fallback for transcendental ones.
- Exact number tower under the hood: `BigRational`, `BigIrrational` (a simplifying
  symbolic tree with arbitrary-precision evaluation), `BigComplex`, and the internal
  `AlgebraicReal` engine.
- Transcendental functions `exp`, `ln`, `sin`, `cos`, `tan`, `atan`, the constants
  `pi`, `e` and the omega constant `Ω`.
- A formula language (Sprache) with:
  - operators `+ - * / ^`, parentheses, unary minus, right-associative power;
  - functions `sqrt`, `exp`, `ln`/`log`, `sin`, `cos`, `tan`, `atan`, `abs`;
  - number literals: integers/big integers, decimals, scientific notation, hexadecimal;
  - Unicode symbols/escapes (`π`, `Ω`, `\uXXXX`);
  - uniform typed literals (quoted or not): `bool(...)`, `char(...)`, `int(...)`,
    `float(...)` (culture-invariant separator), `string(...)`, `base64(...)`
    (standard and URL-safe), `datetime(...)` (any .NET / ISO-8601 format → UTC ticks).

### Notes

- Exact equality of closed-form reals is undecidable in general (Richardson's
  theorem); `Numeric` is exact where it provably can be and numeric otherwise.

[Unreleased]: https://github.com/matteofabbri/Numerica/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/matteofabbri/Numerica/releases/tag/v0.1.0
