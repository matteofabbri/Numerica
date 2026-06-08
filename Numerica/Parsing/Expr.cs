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
            '^' => RationalPower(),
            _ => throw new NotSupportedException($"Operator '{Operator}'."),
        };

        public override BigIrrational ToIrrational() => Operator switch
        {
            '+' => Left.ToIrrational() + Right.ToIrrational(),
            '-' => Left.ToIrrational() - Right.ToIrrational(),
            '*' => Left.ToIrrational() * Right.ToIrrational(),
            '/' => Left.ToIrrational() / Right.ToIrrational(),
            '^' => IrrationalPower(),
            _ => throw new NotSupportedException($"Operator '{Operator}'."),
        };

        public override BigComplex ToComplex() => Operator switch
        {
            '+' => Left.ToComplex() + Right.ToComplex(),
            '-' => Left.ToComplex() - Right.ToComplex(),
            '*' => Left.ToComplex() * Right.ToComplex(),
            '/' => Left.ToComplex() / Right.ToComplex(),
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
            => Name == "abs" && Arguments.Count == 1
                ? Arguments[0].ToRational().Abs
                : throw new NotSupportedException($"'{Name}' does not evaluate to a rational.");

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
                case "abs": return BigComplex.FromReal(a.Magnitude());
                default: throw new NotSupportedException($"Function '{Name}' is not supported on complex numbers.");
            }
        }

        // asin(x) = atan2(x, sqrt(1 - x^2)) -- robust at the endpoints x = +/-1.
        private static BigIrrational Asin(BigIrrational x)
            => BigIrrational.Atan2(x, BigIrrational.Sqrt(1 - x * x));

        // The integer degree n of root(x, n), taken from the second argument.
        private int RootDegree()
        {
            BigRational n = Arguments[1].ToRational();
            if (!n.IsInteger)
                throw new NotSupportedException("root(x, n) requires an integer degree n.");
            return (int)n.Numerator;
        }

        public override string ToString() => $"{Name}({string.Join(", ", Arguments)})";
    }
}
