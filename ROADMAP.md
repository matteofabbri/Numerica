# Roadmap

This is a wish list, not a promise. It captures where Numerica could go and
roughly in what order. Issues and PRs that move any of these forward are welcome —
see [CONTRIBUTING.md](CONTRIBUTING.md).

## Near term (0.x)

- **More transcendental coverage**: ✅ `sinh/cosh/tanh`, `asin/acos`, inverse
  hyperbolics (`asinh/acosh/atanh`), `log` with an arbitrary base (`logb`, `log10`,
  `log2`), `cbrt`/`root(x, n)`, `atan2(y, x)`, and the constants `tau` and the golden
  ratio `phi`, complex-level `asin/acos/atan`, `pow`/`^` for arbitrary real and complex
  exponents (incl. `pow(x, y)` sugar), and the constants **Catalan** `G` and
  **Euler–Mascheroni** `γ` all landed. Still open: a complex `atan2`. (Effectively done.)
- **More parser grammar**: ✅ a modulo operator `%`, a postfix factorial `n!`, the
  reductions `min`/`max`/`gcd`/`lcm`/`mod`, the rounding family
  (`floor`/`ceil`/`round`/`trunc`/`sign`), and implicit multiplication (`2pi`, `3(x+1)`)
  all landed. (Item complete.)
- **Wider algebraic closure**: today `BigAlgebric`/`AlgebraicReal` builds roots of
  *rationals* and does exact arithmetic on them. Extend to roots of arbitrary
  algebraic numbers (e.g. `sqrt(1 + sqrt(2))`) so nested radicals are decided too.
- **More typed literals**: ✅ `rational("3/7")`, `complex("3+4i")`, `hex/bin/oct(...)`,
  `timespan(...)` and `guid(...)` landed. Still open: `long`/`short`/`byte`/`decimal`/
  `double` aliases (with optional range checks) and `ipaddress(...)`.
- **Variables & bindings** in the formula language: `let x = sqrt(2) in x*x`, plus
  user-supplied parameters for reusable expressions.
- **Performance**: ✅ evaluated constants (`pi`, `e`, omega, `catalan`, `egamma`) are
  memoised with a lock-free precision cache, against the new benchmark baseline. Still
  open: binary-splitting for the series (e, π, exp, ln) and caching compiled closures.

## Medium term (1.0)

- **API polish**: ✅ public value accessors that don't leak the internal types
  (`TryGetRational(out BigInteger num, out BigInteger den)`, `TryGetInteger`). Still
  open: round out `INumber<Numeric>` interop, culture/format options to `ToString`, and
  span-based fast paths.
- **Packaging**: ✅ SourceLink (SDK-native), deterministic builds, a `.snupkg` symbol
  package and XML docs are all wired up — `dotnet pack -c Release` produces the `.nupkg`
  and `.snupkg`. Still open: the actual push to NuGet.org (a CI/credentials step).
- **Benchmarks**: ✅ a BenchmarkDotNet suite (`BENCH/Numerica.Benchmarks`) tracking the
  cost of parsing, evaluation and comparison across precisions. Run with
  `dotnet run -c Release --project BENCH/Numerica.Benchmarks`. (This is the measurement
  baseline for the performance work — memoising constants, caching closures.)
- **Docs site**: expand `DOCS/` into a small documentation site (concepts, the
  expression language, the theory, API reference).

## Exploratory (beyond the scalar core)

`Numeric` is deliberately a **scalar**: it implements `INumber<T>`, which assumes a
single totally-ordered value. The following live *outside* that contract and would be
their own types, not crammed into `Numeric`:

- **Index keys**: an `IIndexKey` abstraction so scalars, strings, dates and hashes
  share one orderable/encodable key surface for building value indices.
- **Spatial**: a `GeoPoint` type plus space-filling-curve encoders (geohash, Hilbert,
  Z-order) that map a coordinate to a single locality-preserving `Numeric` key.
- **Vectors / tensors**: an N-dimensional `Tensor`/`Vector` type with its *own*
  algebra (componentwise ops, dot product, norms) — not `INumber`. Useful for
  similarity indexes (which need their own structures: R-tree, k-d tree, HNSW).

See the discussion in [DOCS/DOCS.md](DOCS/DOCS.md) for why a tensor is a *container of
numbers*, not a single number, and where the "encode to a key" trick does and does not
help.
