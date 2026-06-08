# The Numerica expression language

Everything starts from a string: `new Numeric("...")` parses a formula into an
expression tree and evaluates it lazily. This page is the reference for what you can
put inside that string.

```csharp
var n = new Numeric("(1 + sqrt(5)) / 2");
n.ToDecimalString(30);   // "1.618033988749894848204586834365"
n.IsIrrational;          // true
```

## Operators

| Operator | Meaning | Notes |
| --- | --- | --- |
| `+` `-` | add, subtract | |
| `*` `/` | multiply, divide | |
| `%` | remainder | rational, sign of the dividend: `7 % 3 == 1`, `-7 % 3 == -1` |
| `-x` | unary minus | |
| `x ^ y` | power | right-associative: `2^3^2 == 2^(3^2) == 512` |
| `n!` | factorial | postfix, exact, non-negative integers; `5! == 120`. Sugar for `fact(n)` |
| `( … )` | grouping | |

Precedence, lowest to highest: `+ -` → `* / %` → unary `-` → `^` → `!`.
Factorial binds tighter than power and unary minus, so `2^3! == 2^(3!) == 64` and
`-3! == -(3!) == -6`. There is no double-factorial: `n!!` means `(n!)!`.

## Functions

Functions are called with a parenthesised, comma-separated argument list, e.g.
`atan2(1, 1)`.

| Group | Functions |
| --- | --- |
| roots & powers | `sqrt(x)`, `cbrt(x)`, `root(x, n)` (integer `n`), `pow(x, y)` (sugar for `x ^ y`) |
| exponential & log | `exp(x)`, `ln(x)` (alias `log(x)`), `log10(x)`, `log2(x)`, `logb(x, base)`, `log(x, base)` |
| trigonometric | `sin(x)`, `cos(x)`, `tan(x)` |
| inverse trig | `asin(x)`, `acos(x)`, `atan(x)`, `atan2(y, x)` |
| hyperbolic | `sinh(x)`, `cosh(x)`, `tanh(x)` |
| inverse hyperbolic | `asinh(x)`, `acosh(x)`, `atanh(x)` |
| reductions | `min(a, …)`, `max(a, …)`, `gcd(a, …)`, `lcm(a, …)` (integer), `mod(a, b)` |
| rounding | `floor(x)`, `ceil(x)`, `round(x)`, `trunc(x)`, `sign(x)` |
| other | `abs(x)`, `fact(x)` / `factorial(x)` (also the postfix `x!`) |

```
sqrt(2) * sqrt(2)     -> 2          (exact)
cbrt(27)              -> 3          (exact)
root(81, 4)           -> 3          (exact)
sin(1)^2 + cos(1)^2   -> 1
4 * atan(1)           -> pi
atan2(1, 1)           -> pi/4
2 * asin(1)           -> pi
cosh(0)               -> 1
log10(1000)           -> 3
logb(8, 2)            -> 3
min(3, 1, 2)          -> 1
max(sqrt(2), 1.4)     -> sqrt(2)
gcd(24, 36)           -> 12
lcm(4, 6)             -> 12
mod(7, 3)             -> 1
abs(-3/4)             -> 3/4
floor(7/2)            -> 3
ceil(7/2)             -> 4
round(-5/2)           -> -3          (halves round away from zero)
trunc(-7/2)           -> -3          (toward zero)
floor(pi)             -> 3
sign(-sqrt(2))        -> -1
5!                    -> 120        (postfix factorial)
2^3!                  -> 64         (= 2^(3!))
```

`asin`, `acos` and `atan` are defined for complex arguments too; only `atan2` (a planar
angle) stays **real**-only. A formula that is written with real numbers but whose value
leaves the reals — `sqrt(-1)`, `ln(-1)`, `asin(2)` — is promoted to its complex value
(`i`, `i·pi`, `pi/2 - i·ln(2 + sqrt(3))`) instead of failing.

### Rounding and irrational arguments

`floor`, `ceil`, `round` and `trunc` are **exact** on a rational argument: the result
is computed from the reduced fraction, no evaluation involved. `round` rounds halves
away from zero (`round(1/2) == 1`, `round(-1/2) == -1`); `trunc` cuts toward zero;
`sign` returns `-1`, `0` or `1`.

On an argument that stays **irrational** (`floor(pi)`, `floor(sqrt(2))`), the integer
is decided from a high-precision (2⁻²⁵⁶) approximation of the value. This is the one
place where these functions are *not* provably exact: if a transcendental value sat
within 2⁻²⁵⁶ of an integer without actually equalling it, the result could be off by
one — and deciding that exactly is impossible in general (Richardson's theorem; the
same wall described under *Equality, honestly* below). For every value you are likely
to write (`pi`, `e`, `sqrt(2)`, …) the margin is astronomically larger than the
tolerance, so the answer is correct; the caveat matters only for adversarial inputs
deliberately constructed to hug an integer. Roots of rationals that happen to be whole
(`floor(sqrt(4))`) collapse to an exact rational first, so they are decided exactly.

## Constants

| Name | Value |
| --- | --- |
| `pi`  / `π` | the circle constant |
| `tau` / `τ` | `2·pi`, the full-turn constant |
| `e` | Euler's number |
| `phi` / `φ` | the golden ratio `(1 + sqrt(5)) / 2` (exact, stays symbolic) |
| `omega` / `Ω` | the omega constant `W(1)`, root of `x·e^x = 1` (~0.567) |
| `i` | the imaginary unit (only meaningful at the complex level) |
| `true` / `false` | `1` / `0` |

## Number literals

| Form | Example | Value |
| --- | --- | --- |
| integer / big integer | `123`, `2343454675675678567567546545345634534557` | exact |
| decimal | `1.5`, `0.025` | exact `BigRational` |
| scientific | `1.23e5`, `-1.23e4`, `6.02E23`, `1.5e-2` | exact |
| hexadecimal | `0xFF`, `0x10` | `255`, `16` |

## Typed literals

One uniform spelling for every primitive: `type(<content>)`, **with or without
double quotes** (only `"` is used — never `'`). Quotes are only needed when the
content has escapes or characters like `)`.

| Form | Examples | Result |
| --- | --- | --- |
| `bool(...)` | `bool(true)`, `bool("false")`, `bool(TRUE)` | `1` / `0` (case-insensitive) |
| `char(...)` | `char(A)`, `char("A")`, `char("Ω")`, `char("😀")` | the Unicode **code point** (`65`, `937`, `128512`) |
| `int(...)` | `int(34567)`, `int("-5")` | an integer of any size |
| `float(...)` | `float(2134,23)`, `float("2134.23")`, `float(1.5e3)` | exact `BigRational`; the decimal separator is culture-invariant (`.` and `,` both work) |
| `rational(...)` | `rational(3/7)`, `rational(-10/4)` | an exact fraction `a` or `a/b`, reduced to lowest terms |
| `complex(...)` | `complex(3+4i)`, `complex(4i)`, `complex(-i)`, `complex(2.5+0.5i)` | a complex number `a + bi` (decimals allowed; evaluates only at the complex level) |
| `hex(...)` / `bin(...)` / `oct(...)` | `hex(FF)`, `hex(0xFF)`, `bin(0b1111_1111)`, `oct(0o755)` | an integer in base 16 / 2 / 8 (optional `0x`/`0b`/`0o` prefix, `_` separators, leading sign) |
| `string(...)` | `string("ciao")`, `string(hello)` | UTF-8 bytes read big-endian as an unsigned integer |
| `base64(...)` | `base64("SGVsbG8=")`, `base64(__4)` | decoded bytes, same encoding as `string` (standard **and** URL-safe alphabets) |
| `timespan(...)` | `timespan(1:00:00)`, `timespan(1.00:00:00)` | tick count (100 ns), the duration counterpart of `datetime` |
| `datetime(...)` | `datetime(2024-01-15T10:30:00Z)`, `datetime(2024-01-15)` | UTC tick count (any .NET / ISO-8601 format) |
| `guid(...)` | `guid(00000000-0000-0000-0000-0000000000ff)` | the 128 bits as the unsigned integer spelled by the canonical text |

### char vs string

`char` gives the **code point**, `string` gives the **UTF-8 byte sequence** — so they
differ for non-ASCII:

```
char("Ω")    -> 937      (U+03A9)
string("Ω")  -> 52905    (UTF-8 bytes 0xCE 0xA9)
```

### String escapes

Inside `string("…")` (and `char("…")`) the usual C# escapes apply:
`\n \t \r \0 \a \b \f \v \\ \"`, `\uXXXX`, `\xH…` (1–4 hex) and `\UXXXXXXXX`
(a full code point, surrogate pairs included). Unicode escapes `\uXXXX` anywhere in
the input are decoded first, so `"π"` reads as `π`.

## Getting values out

Once built, ask `Numeric` what it is and for its digits:

```csharp
var z = new Numeric("datetime(2024-01-02) - datetime(2024-01-01)");
z.ToDecimalString(0);   // "864000000000"  (ticks in one day)
z.IsRational;           // true
```

- `ToDecimalString(int digits)` — rounded decimal expansion (complex values print as
  `a + bi`).
- `ToValueString()` — a compact, round-trippable string of the **computed** value (not
  the original formula): a reduced `numerator/denominator` when the value is rational
  (`2/6` → `"1/3"`, `sqrt(2)*sqrt(2)` → `"2"`), else the formula. Reload with
  `new Numeric(text)`. Handy for persisting a value as a stable, canonical key.
- `TryGetRational(out BigInteger num, out BigInteger den)` — the exact reduced fraction
  when the value is rational (denominator positive); also catches rationals hidden in an
  algebraic form, e.g. `sqrt(2)*sqrt(2)` → `2/1`. Returns BCL `BigInteger`s only.
- `TryGetInteger(out BigInteger value)` — the exact value when it is a whole number.
- `IsRational` / `IsIrrational` / `IsComplex` — classification.
- `== < > <= >=` — exact and decidable for algebraic formulas, high-precision numeric
  otherwise.
- `GetHashCode()` — hashes the **value**, so equal numbers written differently (`2/6`
  and `1/3`, `sqrt(2)` and `sqrt(8)/2`) share a bucket and work as dictionary/set keys.
- `Numeric` implements `INumber<Numeric>`, so it plugs into the standard operators and
  generic-math algorithms.

## Equality, honestly

For **algebraic** formulas (rationals, `+ - * /`, integer powers, roots of rationals)
`==` and `<` are exact and decidable. For **transcendental** ones (`pi`, `e`, `exp`,
`ln`, trig) exact equality is undecidable in general (Richardson's theorem), so the
comparison falls back to a high-precision numeric one. See [DOCS.md](DOCS.md).
