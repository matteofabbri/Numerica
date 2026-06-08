# Numerica — design notes & references

This document records *why* the library is shaped the way it is, and points to the
literature behind each decision. The story follows the original question: **C# has
`BigInteger`, why not a `BigFloat`?**

## The chain of reasoning

1. **`BigInteger` is conceptually free.** Integers are closed under `+ - *`: the
   result is always another integer and only the storage grows. No design choice is
   forced on you.

2. **A `BigFloat` is not free.** Division explodes immediately: `1/3 = 0.3333…`
   forever. Any arbitrary-precision *float* must decide where to stop and how to
   round — base 2 vs base 10, fixed vs per-operation vs global precision, rounding
   policy. Those choices freeze into an API forever, which is why neither `BigFloat`
   nor `BigDecimal` ever entered the .NET BCL.

3. **The clean exact type is the rational, not the float.** `BigRational`
   (numerator/denominator, both `BigInteger`) never rounds — but it only ever
   represents *rationals*. The moment you take a root or a logarithm you leave the
   field of rationals.

4. **Capturing `sqrt(2)` exactly needs more than a pair of integers.** An irrational
   is *by definition* not a ratio of integers (see the classic parity proof that
   `sqrt(2) = p/q` is impossible). So you must store either:
   - the **operation** — a symbolic expression tree (the CAS approach), or
   - a **computation** — a function `precision -> rational approximation` (the
     computable-reals approach).

   This library takes the **symbolic tree** route in `BigIrrational`, evaluating the
   tree to a rational on demand (`Approximate(bits)`).

5. **The real wall is equality, not printing.** On closed-form reals, `a == b` is
   undecidable and `a < b` is only semi-decidable: if `a == b`, a bit-by-bit
   comparison never terminates. Printing usually works (ask for N digits, get N
   digits) — except on an exact rounding boundary, where it inherits the same
   undecidability. This is **Richardson's theorem**.

6. **The escape hatch is algebraic numbers.** Restricting to roots of integer
   polynomials (minimal polynomial + isolating interval) makes `==` and `<` decidable
   again — you regain `sqrt(2)`, `sqrt(3)`, the golden ratio, but lose `pi` and `e`
   (they are transcendental). This is the decidable middle ground: an internal
   `AlgebraicReal` engine, surfaced through `Numeric`'s exact `==` / `<`.

## Where each idea lives in the code

| Concept | File | Notes |
|---|---|---|
| Exact rationals, decidable `==` | `BigRational.cs` | reduced via GCD, positive denominator |
| Algebraic numbers, decidable `==`/`<` | `Parsing/AlgebraicReal.cs` | annihilating polynomial + isolating interval; Sturm; backs `Numeric` |
| Symbolic tree + simplification | `BigIrrational.cs` | smart constructors; `sqrt(2)*sqrt(2) -> 2` |
| On-demand numeric evaluation | `BigIrrational.cs` | `Approximate(bits)`, precision-driven closures |
| Complex over closed-form reals | `BigComplex.cs` | `i^2 = -1`, `|3+4i| = 5` exactly |
| Lazy formula-backed facade | `Numeric.cs` | `new Numeric("...")`, evaluated on demand, `INumber<T>` |

## References

### Computability and the undecidability of equality
- **A. M. Turing (1936).** *On Computable Numbers, with an Application to the
  Entscheidungsproblem.* Proc. London Math. Soc. 42, 230–265. The origin of
  "computable real". <https://doi.org/10.1112/plms/s2-42.1.230>
- **D. Richardson (1968).** *Some Undecidable Problems Involving Elementary
  Functions of a Real Variable.* J. Symbolic Logic 33(4), 514–520. Why "is this
  expression with `pi`, `exp`, `sin` equal to zero?" is undecidable.
  <https://doi.org/10.2307/2271358>
- **K. Weihrauch (2000).** *Computable Analysis: An Introduction.* Springer.
  Type-Two Theory of Effectivity (TTE), the standard framework for computable reals.
- **K.-I. Ko (1991).** *Complexity Theory of Real Functions.* Birkhäuser. Complexity
  of the precision-as-a-function representation used here.

### Exact real arithmetic (the "computation" approach)
- **H.-J. Boehm, R. Cartwright, M. Riggle, M. J. O'Donnell (1986).** *Exact real
  arithmetic: a case study in higher order programming.* ACM LISP & Functional
  Programming. The closure-of-approximations design.
- **H.-J. Boehm (2005).** *The Constructive Reals as a Java Library.* J. Logic and
  Algebraic Programming 64(1), 3–11. The library behind the **Android calculator**,
  which makes `sqrt(2) * sqrt(2)` display exactly `2`.
  <https://doi.org/10.1016/j.jlap.2004.07.002>
- **H.-J. Boehm (2020).** *Towards an API for the Real Numbers.* PLDI 2020, 562–576.
  Modern treatment of comparison, hashing and the equality problem in practice.
  <https://doi.org/10.1145/3385412.3386037>
- **J. Vuillemin (1990).** *Exact real computer arithmetic with continued fractions.*
  IEEE Trans. Computers 39(8), 1087–1105. <https://doi.org/10.1109/12.57047>
- **A. Edalat, P. J. Potts (1997).** *A new representation for exact real numbers.*
  ENTCS 6, 119–132. Möbius-transformation / linear-fractional approach.
- **N. Müller.** *The iRRAM library* — exact real arithmetic in C++.
  <https://irram.uni-trier.de/>
- **B. Lambov (2007).** *RealLib: An efficient implementation of exact real
  arithmetic.* Math. Structures in Comp. Sci. 17(1), 81–98.

### Symbolic computation (the "expression" approach)
- **J. von zur Gathen, J. Gerhard (2013).** *Modern Computer Algebra*, 3rd ed.
  Cambridge UP. Canonical/normal forms and the limits of simplification.
- **J. S. Cohen (2003).** *Computer Algebra and Symbolic Computation:
  Mathematical Methods.* A K Peters. Practical expression-tree simplification, the
  model for the smart constructors in `BigIrrational`.
- **SymPy / Mathematica** documentation — production CAS that manipulate the tree
  (simplify, differentiate, recognize closed forms) rather than the value.

### Algebraic numbers (the decidable middle ground)
- **H. Cohen (1993).** *A Course in Computational Algebraic Number Theory.* Springer.
  Minimal polynomial + isolating interval; exact comparison of algebraic reals.
- **R. Loos (1983).** *Computing in algebraic extensions*, in *Computer Algebra:
  Symbolic and Algebraic Computation*. Real root isolation.

### Underlying integer/numeric algorithms
- **D. E. Knuth (1997).** *The Art of Computer Programming, Vol. 2: Seminumerical
  Algorithms*, 3rd ed. GCD, integer roots, series evaluation.
- **R. P. Brent, P. Zimmermann (2010).** *Modern Computer Arithmetic.* Cambridge UP.
  Newton iteration for n-th roots, scaling and guard digits.
- **J. Machin (1706).** `pi = 16·arctan(1/5) − 4·arctan(1/239)`, used by `Pi`.

### .NET background
- dotnet/runtime discussions on `BigRational` / `BigDecimal` proposals — why they
  never shipped in the BCL. <https://github.com/dotnet/runtime/issues/20619>
- **Generic math** (`INumber<T>`, `IFloatingPoint<T>`, .NET 7+) — the interfaces a
  custom arbitrary-precision type can implement to integrate with standard operators.
  <https://learn.microsoft.com/dotnet/standard/generics/math>
- **Sprache** — the parser-combinator library used by `ExpressionParser` to map
  strings to `Expr` trees. <https://github.com/sprache/Sprache>
