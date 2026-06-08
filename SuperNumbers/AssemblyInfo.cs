using System.Runtime.CompilerServices;

// The concrete number types (BigRational, BigIrrational, BigComplex, ...) are internal:
// Numeric is the only public type. The test project sees the internals as a white-box.
[assembly: InternalsVisibleTo("SuperNumbers.Tests")]
