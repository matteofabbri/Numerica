using Xunit;

// Run tests sequentially: the library is deterministic and some suites are CPU-heavy.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
