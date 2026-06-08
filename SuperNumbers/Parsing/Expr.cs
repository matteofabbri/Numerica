using System.Numerics;

namespace SuperNumbers.Parsing;

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
public abstract class Expr
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
        public const string E = "e";
        public const string I = "i";

        public string Name { get; }
        public Constant(string name) => Name = name;

        public override BigRational ToRational()
            => throw new NotSupportedException($"'{Name}' is not rational.");

        public override BigIrrational ToIrrational() => Name switch
        {
            Pi => BigIrrational.Pi,
            E => BigIrrational.E,
            I => throw new NotSupportedException("The imaginary unit 'i' is not real."),
            _ => throw new NotSupportedException($"Unknown constant '{Name}'."),
        };

        public override BigComplex ToComplex() => Name switch
        {
            Pi => BigComplex.FromReal(BigIrrational.Pi),
            E => BigComplex.FromReal(BigIrrational.E),
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
        public Expr Argument { get; }

        public Function(string name, Expr argument)
        {
            Name = name;
            Argument = argument;
        }

        public override BigRational ToRational()
            => throw new NotSupportedException($"'{Name}' does not evaluate to a rational.");

        public override BigIrrational ToIrrational()
        {
            BigIrrational a = Argument.ToIrrational();
            return Name switch
            {
                "sqrt" => BigIrrational.Sqrt(a),
                "exp" => BigIrrational.Exp(a),
                "ln" or "log" => BigIrrational.Ln(a),
                "sin" => BigIrrational.Sin(a),
                "cos" => BigIrrational.Cos(a),
                "tan" => BigIrrational.Tan(a),
                "atan" => BigIrrational.Atan(a),
                _ => throw new NotSupportedException($"Unknown function '{Name}'."),
            };
        }

        public override BigComplex ToComplex()
        {
            BigComplex a = Argument.ToComplex();
            return Name switch
            {
                "sqrt" => BigComplex.Sqrt(a),
                "exp" => BigComplex.Exp(a),
                "ln" or "log" => BigComplex.Ln(a),
                "sin" => BigComplex.Sin(a),
                "cos" => BigComplex.Cos(a),
                "tan" => BigComplex.Sin(a) / BigComplex.Cos(a),
                _ => throw new NotSupportedException($"Function '{Name}' is not supported on complex numbers."),
            };
        }

        public override string ToString() => $"{Name}({Argument})";
    }
}
