using BenchmarkDotNet.Attributes;
using Numerica;

namespace Numerica.Benchmarks;

// Tracks the cost of the three things that dominate real usage: parsing a formula,
// evaluating it to digits across precisions, and deciding equality (algebraic vs
// transcendental). The constant benchmarks (pi/catalan/egamma) are the obvious
// candidates for the memoisation/caching work on the roadmap.
[MemoryDiagnoser]
public class NumericBenchmarks
{
    [Params(50, 200, 1000)]
    public int Digits;

    private const string Algebraic = "(1 + sqrt(5)) / 2";

    [Benchmark]
    public Numeric Parse() => new(Algebraic);

    [Benchmark]
    public string EvaluateAlgebraic() => new Numeric(Algebraic).ToDecimalString(Digits);

    [Benchmark]
    public string EvaluatePi() => new Numeric("pi").ToDecimalString(Digits);

    [Benchmark]
    public string EvaluateCatalan() => new Numeric("catalan").ToDecimalString(Digits);

    [Benchmark]
    public string EvaluateEulerGamma() => new Numeric("egamma").ToDecimalString(Digits);

    [Benchmark]
    public bool CompareAlgebraic() => new Numeric("sqrt(2) * sqrt(2)") == new Numeric("2");

    [Benchmark]
    public bool CompareTranscendental() => new Numeric("exp(ln(5))") == new Numeric("5");
}
