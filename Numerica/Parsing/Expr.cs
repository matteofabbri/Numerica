using System.Numerics;

namespace Numerica.Parsing;

/// <summary>
/// A universal expression tree, decoupled from any single numeric domain.
///
/// The same tree evaluates at all THREE levels of the tower:
///   - <see cref="ToRational"/>   -> <see cref="BigRational"/>   (fails on anything irrational)
///   - <see cref="ToIrrational"/> -> <see cref="BigIrrational"/> (roots, pi, e, exp/ln/trig)
///   - <see cref="ToComplex"/>    -> <see cref="BigComplex"/>    (adds the imaginary unit i)
///
/// Strings map to this tree automatically via <see cref="Parse"/> (built on the
/// Sprache parser-combinator library), closing the loop string -> expression ->
/// value at whichever level you ask for.
/// </summary>
internal abstract class Expr
{
    /// <summary>Evaluate as an exact rational. Throws if the tree is not rational.</summary>
    public abstract BigRational ToRational();

    /// <summary>Evaluate as a closed-form real (symbolic tree).</summary>
    public abstract BigIrrational ToIrrational();

    /// <summary>Evaluate as a complex number.</summary>
    public abstract BigComplex ToComplex();

    /// <summary>Parse a string such as "sqrt(2) + 1/3" or "exp(i*pi)" into a tree.</summary>
    public static Expr Parse(string text) => ExpressionParser.ParseString(text);

    // ---------- Nodes ----------

    public sealed class Number : Expr
    {
        public BigRational Value { get; }
        public Number(BigRational value) => Value = value;

        public override BigRational ToRational() => Value;
        public override BigIrrational ToIrrational() => BigIrrational.FromRational(Value);
        public override BigComplex ToComplex() => BigComplex.FromRational(Value);
        public override string ToString() => Value.ToString();
    }

    public sealed class Constant : Expr
    {
        public const string Pi = "pi";
        public const string Tau = "tau";
        public const string E = "e";
        public const string I = "i";
        public const string Omega = "omega";
        public const string Phi = "phi";
        public const string Catalan = "catalan";

        public string Name { get; }
        public Constant(string name) => Name = name;

        // The golden ratio (1 + sqrt(5)) / 2, kept symbolic so identities stay exact.
        private static BigIrrational GoldenRatio
            => (BigIrrational.One + BigIrrational.Sqrt(5)) / 2;

        public override BigRational ToRational()
            => throw new NotSupportedException($"'{Name}' is not rational.");

        public override BigIrrational ToIrrational() => Name switch
        {
            Pi => BigIrrational.Pi,
            Tau => BigIrrational.Pi * 2,
            E => BigIrrational.E,
            Omega => BigIrrational.Omega,
            Phi => GoldenRatio,
            Catalan => BigIrrational.Catalan,
            I => throw new NotSupportedException("The imaginary unit 'i' is not real."),
            _ => throw new NotSupportedException($"Unknown constant '{Name}'."),
        };

        public override BigComplex ToComplex() => Name switch
        {
            Pi => BigComplex.FromReal(BigIrrational.Pi),
            Tau => BigComplex.FromReal(BigIrrational.Pi * 2),
            E => BigComplex.FromReal(BigIrrational.E),
            Omega => BigComplex.FromReal(BigIrrational.Omega),
            Phi => BigComplex.FromReal(GoldenRatio),
            Catalan => BigComplex.FromReal(BigIrrational.Catalan),
            I => BigComplex.ImaginaryUnit,
            _ => throw new NotSupportedException($"Unknown constant '{Name}'."),
        };

        public override string ToString() => Name;
    }

    public sealed class Negate : Expr
    {
        public Expr Operand { get; }
        public Negate(Expr operand) => Operand = operand;

        public override BigRational ToRational() => -Operand.ToRational();
        public override BigIrrational ToIrrational() => -Operand.ToIrrational();
        public override BigComplex ToComplex() => -Operand.ToComplex();
        public override string ToString() => $"-{Operand}";
    }

    public sealed class Binary : Expr
    {
        public char Operator { get; }
        public Expr Left { get; }
        public Expr Right { get; }

        public Binary(char op, Expr left, Expr right)
        {
            Operator = op;
            Left = left;
            Right = right;
        }

        public override BigRational ToRational() => Operator switch
        {
            '+' => Left.ToRational() + Right.ToRational(),
            '-' => Left.ToRational() - Right.ToRational(),
            '*' => Left.ToRational() * Right.ToRational(),
            '/' => Left.ToRational() / Right.ToRational(),
            '%' => BigRational.Mod(Left.ToRational(), Right.ToRational()),
            '^' => RationalPower(),
            _ => throw new NotSupportedException($"Operator '{Operator}'."),
        };

        public override BigIrrational ToIrrational() => Operator switch
        {
            '+' => Left.ToIrrational() + Right.ToIrrational(),
            '-' => Left.ToIrrational() - Right.ToIrrational(),
            '*' => Left.ToIrrational() * Right.ToIrrational(),
            '/' => Left.ToIrrational() / Right.ToIrrational(),
            // Modulo is only meaningful for rationals; evaluate exactly there.
            '%' => BigIrrational.FromRational(BigRational.Mod(Left.ToRational(), Right.ToRational())),
            '^' => IrrationalPower(),
            _ => throw new NotSupportedException($"Operator '{Operator}'."),
        };

        public override BigComplex ToComplex() => Operator switch
        {
            '+' => Left.ToComplex() + Right.ToComplex(),
            '-' => Left.ToComplex() - Right.ToComplex(),
            '*' => Left.ToComplex() * Right.ToComplex(),
            '/' => Left.ToComplex() / Right.ToComplex(),
            '%' => throw new NotSupportedException("Modulo is not defined on complex numbers."),
            '^' => ComplexPower(),
            _ => throw new NotSupportedException($"Operator '{Operator}'."),
        };

        private BigRational RationalPower()
        {
            BigRational exponent = Right.ToRational();
            if (!exponent.IsInteger)
                throw new NotSupportedException("Rational power requires an integer exponent.");
            return BigRational.Pow(Left.ToRational(), (int)exponent.Numerator);
        }

        private BigIrrational IrrationalPower()
        {
            // A rational exponent keeps the result algebraic and exact; otherwise a^b = exp(b ln a).
            if (TryRationalExponent(out BigRational exponent))
                return BigIrrational.Power(Left.ToIrrational(), exponent);
            return BigIrrational.Exp(Right.ToIrrational() * BigIrrational.Ln(Left.ToIrrational()));
        }

        private BigComplex ComplexPower()
        {
            if (TryRationalExponent(out BigRational exponent) && exponent.IsInteger)
                return BigComplex.Power(Left.ToComplex(), (int)exponent.Numerator);
            return BigComplex.Power(Left.ToComplex(), Right.ToComplex());
        }

        private bool TryRationalExponent(out BigRational exponent)
        {
            try { exponent = Right.ToRational(); return true; }
            catch (NotSupportedException) { exponent = BigRational.Zero; return false; }
            catch (DivideByZeroException) { exponent = BigRational.Zero; return false; }
        }

        public override string ToString() => $"({Left} {Operator} {Right})";
    }

    public sealed class Function : Expr
    {
        public string Name { get; }
        public IReadOnlyList<Expr> Arguments { get; }

        public Function(string name, IReadOnlyList<Expr> arguments)
        {
            Name = name;
            Arguments = arguments;
        }

        /// <summary>Convenience accessor for the (sole) argument of a unary function.</summary>
        public Expr Argument => Arguments[0];

        public override BigRational ToRational()
        {
            switch (Name)
            {
                case "abs" when Arguments.Count == 1: return Arguments[0].ToRational().Abs;
                // floor/ceil/round/trunc/sign always yield an integer, so they are
                // rational even when the argument is irrational (floor(pi) -> 3).
                case "floor": return new BigRational(RoundToInteger(r => r.Floor()));
                case "ceil": return new BigRational(RoundToInteger(r => r.Ceiling()));
                case "round": return new BigRational(RoundToInteger(r => r.Round()));
                case "trunc": return new BigRational(RoundToInteger(r => r.Truncate()));
                case "sign": return new BigRational(SignOfArgument());
                case "fact":
                case "factorial": return FactorialOfArg();
                case "min": return MinMaxRational(wantMax: false);
                case "max": return MinMaxRational(wantMax: true);
                case "gcd": return GcdOfArgs();
                case "lcm": return LcmOfArgs();
                case "mod": return ModOfArgs();
                default: throw new NotSupportedException($"'{Name}' does not evaluate to a rational.");
            }
        }

        public override BigIrrational ToIrrational()
        {
            BigIrrational a = Arguments[0].ToIrrational();
            switch (Name)
            {
                case "sqrt": return BigIrrational.Sqrt(a);
                case "cbrt": return BigIrrational.Root(a, 3);
                case "root": return BigIrrational.Root(a, RootDegree());
                case "exp": return BigIrrational.Exp(a);
                case "ln":
                case "log":
                    return Arguments.Count == 2
                        ? BigIrrational.Ln(a) / BigIrrational.Ln(Arguments[1].ToIrrational())
                        : BigIrrational.Ln(a);
                case "logb": return BigIrrational.Ln(a) / BigIrrational.Ln(Arguments[1].ToIrrational());
                case "log10": return BigIrrational.Ln(a) / BigIrrational.Ln(10);
                case "log2": return BigIrrational.Ln(a) / BigIrrational.Ln(2);
                case "sin": return BigIrrational.Sin(a);
                case "cos": return BigIrrational.Cos(a);
                case "tan": return BigIrrational.Tan(a);
                case "asin": return Asin(a);
                case "acos": return BigIrrational.Pi / 2 - Asin(a);
                case "atan": return BigIrrational.Atan(a);
                case "atan2": return BigIrrational.Atan2(a, Arguments[1].ToIrrational());
                case "sinh": return (BigIrrational.Exp(a) - BigIrrational.Exp(-a)) / 2;
                case "cosh": return (BigIrrational.Exp(a) + BigIrrational.Exp(-a)) / 2;
                case "tanh":
                {
                    BigIrrational ePos = BigIrrational.Exp(a), eNeg = BigIrrational.Exp(-a);
                    return (ePos - eNeg) / (ePos + eNeg);
                }
                case "asinh": return BigIrrational.Ln(a + BigIrrational.Sqrt(a * a + 1));
                case "acosh": return BigIrrational.Ln(a + BigIrrational.Sqrt(a * a - 1));
                case "atanh": return BigIrrational.Ln((1 + a) / (1 - a)) / 2;
                case "floor": return BigIrrational.FromInteger(RoundToInteger(r => r.Floor()));
                case "ceil": return BigIrrational.FromInteger(RoundToInteger(r => r.Ceiling()));
                case "round": return BigIrrational.FromInteger(RoundToInteger(r => r.Round()));
                case "trunc": return BigIrrational.FromInteger(RoundToInteger(r => r.Truncate()));
                case "sign": return BigIrrational.FromInteger(SignOfArgument());
                case "fact":
                case "factorial": return BigIrrational.FromRational(FactorialOfArg());
                case "min": return MinMaxIrrational(wantMax: false);
                case "max": return MinMaxIrrational(wantMax: true);
                case "gcd": return BigIrrational.FromRational(GcdOfArgs());
                case "lcm": return BigIrrational.FromRational(LcmOfArgs());
                case "mod": return BigIrrational.FromRational(ModOfArgs());
                case "abs": return a.SignApprox() < 0 ? -a : a;
                default: throw new NotSupportedException($"Unknown function '{Name}'.");
            }
        }

        public override BigComplex ToComplex()
        {
            BigComplex a = Arguments[0].ToComplex();
            switch (Name)
            {
                case "sqrt": return BigComplex.Sqrt(a);
                case "cbrt": return BigComplex.Power(a, BigComplex.FromRational(new BigRational(1, 3)));
                case "root": return BigComplex.Power(a, BigComplex.FromRational(new BigRational(1, RootDegree())));
                case "exp": return BigComplex.Exp(a);
                case "ln":
                case "log":
                    return Arguments.Count == 2
                        ? BigComplex.Ln(a) / BigComplex.Ln(Arguments[1].ToComplex())
                        : BigComplex.Ln(a);
                case "logb": return BigComplex.Ln(a) / BigComplex.Ln(Arguments[1].ToComplex());
                case "log10": return BigComplex.Ln(a) / BigComplex.Ln(BigComplex.FromInteger(10));
                case "log2": return BigComplex.Ln(a) / BigComplex.Ln(BigComplex.FromInteger(2));
                case "sin": return BigComplex.Sin(a);
                case "cos": return BigComplex.Cos(a);
                case "tan": return BigComplex.Sin(a) / BigComplex.Cos(a);
                // Principal inverse trig via logs: asin(z) = -i·ln(iz + sqrt(1 - z²)),
                // acos(z) = -i·ln(z + i·sqrt(1 - z²)), atan(z) = (i/2)·(ln(1 - iz) - ln(1 + iz)).
                case "asin": return ComplexAsin(a);
                case "acos":
                {
                    BigComplex i = BigComplex.ImaginaryUnit;
                    return -i * BigComplex.Ln(a + i * BigComplex.Sqrt(BigComplex.One - a * a));
                }
                case "atan":
                {
                    BigComplex i = BigComplex.ImaginaryUnit;
                    return i / BigComplex.FromInteger(2)
                        * (BigComplex.Ln(BigComplex.One - i * a) - BigComplex.Ln(BigComplex.One + i * a));
                }
                case "sinh": return (BigComplex.Exp(a) - BigComplex.Exp(-a)) / BigComplex.FromInteger(2);
                case "cosh": return (BigComplex.Exp(a) + BigComplex.Exp(-a)) / BigComplex.FromInteger(2);
                case "tanh":
                {
                    BigComplex ePos = BigComplex.Exp(a), eNeg = BigComplex.Exp(-a);
                    return (ePos - eNeg) / (ePos + eNeg);
                }
                case "asinh": return BigComplex.Ln(a + BigComplex.Sqrt(a * a + BigComplex.One));
                case "acosh": return BigComplex.Ln(a + BigComplex.Sqrt(a * a - BigComplex.One));
                case "atanh":
                    return BigComplex.Ln((BigComplex.One + a) / (BigComplex.One - a)) / BigComplex.FromInteger(2);
                case "fact":
                case "factorial": return BigComplex.FromRational(FactorialOfArg());
                case "abs": return BigComplex.FromReal(a.Magnitude());
                default: throw new NotSupportedException($"Function '{Name}' is not supported on complex numbers.");
            }
        }

        // asin(x) = atan2(x, sqrt(1 - x^2)) -- robust at the endpoints x = +/-1.
        private static BigIrrational Asin(BigIrrational x)
            => BigIrrational.Atan2(x, BigIrrational.Sqrt(1 - x * x));

        // asin(z) = -i·ln(iz + sqrt(1 - z²)) -- the complex principal value.
        private static BigComplex ComplexAsin(BigComplex z)
        {
            BigComplex i = BigComplex.ImaginaryUnit;
            return -i * BigComplex.Ln(i * z + BigComplex.Sqrt(BigComplex.One - z * z));
        }

        // Binary precision used to decide floor/ceil/round/trunc on a non-rational real.
        private const int RoundingBits = 256;

        // Round the argument's real value to an integer. Exact when it already folds to a
        // rational; otherwise it is decided from a 2^-RoundingBits approximation, which is
        // wrong only for a value that sits within that tolerance of an integer without
        // equalling it -- a case that is undecidable in general (Richardson's theorem).
        private BigInteger RoundToInteger(Func<BigRational, BigInteger> toInteger)
        {
            BigIrrational a = Arguments[0].ToIrrational();
            BigRational value = a.TryGetRational(out BigRational exact) ? exact : a.Approximate(RoundingBits);
            return toInteger(value);
        }

        // Sign of the argument's real value (-1, 0, +1): exact for a rational, numeric
        // for a closed-form real.
        private int SignOfArgument()
        {
            BigIrrational a = Arguments[0].ToIrrational();
            return a.TryGetRational(out BigRational exact) ? exact.Sign : a.SignApprox();
        }

        // The integer degree n of root(x, n), taken from the second argument.
        private int RootDegree()
        {
            BigRational n = Arguments[1].ToRational();
            if (!n.IsInteger)
                throw new NotSupportedException("root(x, n) requires an integer degree n.");
            return (int)n.Numerator;
        }

        // Factorial n! -- exact, defined only for a non-negative integer argument.
        private BigRational FactorialOfArg()
        {
            if (Arguments.Count != 1)
                throw new NotSupportedException("factorial takes exactly one argument.");
            BigRational r = Arguments[0].ToRational();
            if (!r.IsInteger || r.Sign < 0)
                throw new NotSupportedException("factorial is defined only for non-negative integers.");

            BigInteger result = BigInteger.One;
            for (BigInteger k = 2; k <= r.Numerator; k++) result *= k;
            return new BigRational(result);
        }

        // ----- variadic reductions: min / max / gcd / lcm / mod -----

        // min/max over exact rationals (every argument must be rational).
        private BigRational MinMaxRational(bool wantMax)
        {
            BigRational best = Arguments[0].ToRational();
            for (int i = 1; i < Arguments.Count; i++)
            {
                BigRational c = Arguments[i].ToRational();
                if (wantMax ? c > best : c < best) best = c;
            }
            return best;
        }

        // min/max over closed-form reals: the choice is numeric, but the value returned
        // is one of the arguments unchanged, so no precision is invented.
        private BigIrrational MinMaxIrrational(bool wantMax)
        {
            BigIrrational best = Arguments[0].ToIrrational();
            for (int i = 1; i < Arguments.Count; i++)
            {
                BigIrrational c = Arguments[i].ToIrrational();
                int cmp = c.CompareApprox(best);
                if (wantMax ? cmp > 0 : cmp < 0) best = c;
            }
            return best;
        }

        // The i-th argument as an exact integer (gcd/lcm are integer-only).
        private BigInteger IntegerArg(int index)
        {
            BigRational r = Arguments[index].ToRational();
            if (!r.IsInteger)
                throw new NotSupportedException($"'{Name}' requires integer arguments.");
            return r.Numerator;
        }

        private BigRational GcdOfArgs()
        {
            BigInteger g = IntegerArg(0);
            for (int i = 1; i < Arguments.Count; i++)
                g = BigInteger.GreatestCommonDivisor(g, IntegerArg(i));
            return new BigRational(BigInteger.Abs(g));
        }

        private BigRational LcmOfArgs()
        {
            BigInteger l = IntegerArg(0);
            for (int i = 1; i < Arguments.Count; i++)
            {
                BigInteger n = IntegerArg(i);
                if (l.IsZero || n.IsZero) { l = BigInteger.Zero; continue; }
                l = l / BigInteger.GreatestCommonDivisor(l, n) * n;
            }
            return new BigRational(BigInteger.Abs(l));
        }

        private BigRational ModOfArgs()
        {
            if (Arguments.Count != 2)
                throw new NotSupportedException("mod(a, b) takes exactly two arguments.");
            return BigRational.Mod(Arguments[0].ToRational(), Arguments[1].ToRational());
        }

        public override string ToString()
        {
            // Render factorial with its postfix spelling; wrap a bare unary minus so
            // "(-3)!" never collapses to "-3!" (which parses as -(3!)).
            if ((Name == "fact" || Name == "factorial") && Arguments.Count == 1)
            {
                string baseText = Arguments[0] is Negate ? $"({Arguments[0]})" : Arguments[0].ToString()!;
                return $"{baseText}!";
            }
            return $"{Name}({string.Join(", ", Arguments)})";
        }
    }
}
