using BenchmarkDotNet.Running;
using Numerica.Benchmarks;

// Run with: dotnet run -c Release --project BENCH/Numerica.Benchmarks
// Filter a subset with: dotnet run -c Release --project BENCH/Numerica.Benchmarks -- --filter *Catalan*
BenchmarkRunner.Run<NumericBenchmarks>();
