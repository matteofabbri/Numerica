# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to
follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Multi-argument functions** in the formula language: function calls now take a
  comma-separated argument list, e.g. `atan2(y, x)`, `root(x, n)`, `logb(x, base)`
  and `log(x, base)`.
- New functions: `cbrt`, `root`, `log10`, `log2`, `logb`, `asin`, `acos`, `atan2`,
  `sinh`, `cosh`, `tanh`, `asinh`, `acosh`, `atanh`. The inverse-hyperbolic and the
  rest also evaluate on the complex level; the inverse-trig (`asin`/`acos`/`atan2`)
  are real-only for now.
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
