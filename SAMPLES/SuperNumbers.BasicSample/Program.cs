using SuperNumbers;
using SuperNumbers.Parsing;

public class Program
{
    public static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("=== The exact-number tower ===\n");

        // ---------------------------------------------------------------------------
        // 1) BigRational -- exact rationals, with a real == operator.
        // ---------------------------------------------------------------------------
        Console.WriteLine("--- BigRational (exact) ---");
        Console.WriteLine($"1/3 + 1/6        = {new BigRational(1, 3) + new BigRational(1, 6)}");
        Console.WriteLine($"(2/3)^3          = {BigRational.Pow(new BigRational(2, 3), 3)}");
        Console.WriteLine($"1/3 == 2/6       = {new BigRational(1, 3) == new BigRational(2, 6)}\n");

        // ---------------------------------------------------------------------------
        // 2) BigIrrational -- symbolic tree: simplifies, then evaluates (now with exp/ln/trig).
        // ---------------------------------------------------------------------------
        Console.WriteLine("--- BigIrrational (symbolic tree) ---");
        BigIrrational root2 = BigIrrational.Sqrt(2);
        Console.WriteLine($"sqrt(2)*sqrt(2)  = {root2 * root2}              (exact symbolic)");
        BigIrrational phi = (BigIrrational.One + BigIrrational.Sqrt(5)) / 2;
        Console.WriteLine($"phi^2 - phi - 1  = {BigIrrational.Power(phi, 2) - phi - BigIrrational.One}              (exact symbolic zero)");
        Console.WriteLine($"exp(1)           = {BigIrrational.E.ToDecimalString(30)}");
        Console.WriteLine($"ln(2)            = {BigIrrational.Ln(BigIrrational.FromInteger(2)).ToDecimalString(30)}");
        BigIrrational x = new BigRational(7, 5);
        BigIrrational pythag = BigIrrational.Power(BigIrrational.Sin(x), 2) + BigIrrational.Power(BigIrrational.Cos(x), 2);
        Console.WriteLine($"sin^2 + cos^2    = {pythag.ToDecimalString(30)}              (== 1)\n");

        // ---------------------------------------------------------------------------
        // 3) BigComplex -- now with complex exp/ln/sin/cos and Euler's identity.
        // ---------------------------------------------------------------------------
        Console.WriteLine("--- BigComplex ---");
        BigComplex z = new(BigIrrational.FromInteger(3), BigIrrational.FromInteger(4));
        Console.WriteLine($"|3 + 4i|         = {z.Magnitude()}              (exactly 5)");
        Console.WriteLine($"i^2              = {BigComplex.ImaginaryUnit * BigComplex.ImaginaryUnit}");
        BigComplex euler = BigComplex.Exp(BigComplex.ImaginaryUnit * BigComplex.FromReal(BigIrrational.Pi)) + BigComplex.One;
        Console.WriteLine($"exp(i*pi) + 1    = {euler.Real.ToDecimalString(20)} + {euler.Imaginary.ToDecimalString(20)}i   (== 0)\n");

        // ---------------------------------------------------------------------------
        // 4) Expr + Sprache -- one tree, three levels, parsed from a string.
        // ---------------------------------------------------------------------------
        Console.WriteLine("--- Expr (string -> tree -> value, via Sprache) ---");
        Console.WriteLine($"\"1/3 + 1/6\"      rational => {Expr.Parse("1/3 + 1/6").ToRational()}");
        Console.WriteLine($"\"sqrt(2)*sqrt(2)\" irrational => {Expr.Parse("sqrt(2)*sqrt(2)").ToIrrational()}");
        Console.WriteLine($"\"exp(i*pi)+1\"    complex => approximately {Expr.Parse("exp(i*pi)+1").ToComplex().Real.ToDecimalString(15)} + {Expr.Parse("exp(i*pi)+1").ToComplex().Imaginary.ToDecimalString(15)}i\n");

        // ---------------------------------------------------------------------------
        // 5) BigAlgebraic -- the decidable middle ground: exact == and <.
        // ---------------------------------------------------------------------------
        Console.WriteLine("--- BigAlgebraic (decidable == and <) ---");
        BigAlgebraic a2 = BigAlgebraic.Sqrt(2);
        Console.WriteLine($"sqrt(2)*sqrt(2) == 2   -> {a2 * a2 == BigAlgebraic.FromInteger(2)}   (DECIDED, exactly true)");
        Console.WriteLine($"sqrt(2) < sqrt(3)      -> {a2 < BigAlgebraic.Sqrt(3)}");
        BigAlgebraic gold = (BigAlgebraic.FromInteger(1) + BigAlgebraic.Sqrt(5)) / BigAlgebraic.FromInteger(2);
        Console.WriteLine($"phi^2 == phi + 1       -> {gold * gold == gold + BigAlgebraic.FromInteger(1)}");
        Console.WriteLine($"sqrt(2) value          = {a2.ToDecimalString(30)}\n");

        Console.WriteLine("Note: BigRational and BigAlgebraic have exact, decidable equality; BigIrrational's");
        Console.WriteLine("simplifier reaches exact answers when the structure cancels, otherwise it compares");
        Console.WriteLine("only up to a chosen precision (Richardson's theorem). See DOCS.md.");
    }
}