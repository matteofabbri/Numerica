using SuperNumbers;

public class Program
{
    public static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("=== Numeric: the only public type ===\n");

        // Build numbers from formula strings. They stay suspended calculations and only
        // become a value when asked (here, via ToDecimalString and the Is* properties).
        string[] formulas =
        {
            "1/3 + 1/6",
            "sqrt(2) * sqrt(2)",
            "(1 + sqrt(5)) / 2",
            "exp(1)",
            "2 + 3*i",
        };

        Console.WriteLine($"{"formula",-22}{"kind",-12}value");
        foreach (string formula in formulas)
        {
            var n = new Numeric(formula);
            string kind = n.IsComplex ? "complex" : n.IsRational ? "rational" : "irrational";
            Console.WriteLine($"{formula,-22}{kind,-12}{n.ToDecimalString(20)}");
        }

        Console.WriteLine();

        // == and < are exact and decidable for algebraic formulas.
        Console.WriteLine($"sqrt(2)*sqrt(2) == 2  -> {new Numeric("sqrt(2) * sqrt(2)") == new Numeric("2")}");
        Console.WriteLine($"sqrt(2) < sqrt(3)     -> {new Numeric("sqrt(2)") < new Numeric("sqrt(3)")}");

        // Numeric implements INumber<Numeric>, so it composes with generic-math code.
        Numeric total = Sum(new Numeric("1/2"), new Numeric("1/3"), new Numeric("1/6"));
        Console.WriteLine($"1/2 + 1/3 + 1/6 == 1  -> {total == Numeric.One}");
    }

    private static T Sum<T>(params T[] values) where T : System.Numerics.INumber<T>
    {
        T acc = T.Zero;
        foreach (T v in values) acc += v;
        return acc;
    }
}
