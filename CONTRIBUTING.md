# Contributing to Numerica

Thanks for your interest! Contributions of all sizes are welcome — bug reports,
docs, tests, and code.

## Getting started

You need the **.NET 10 SDK** (or newer). The repo pins it via `global.json`.

```bash
git clone https://github.com/matteofabbri/Numerica.git
cd Numerica
dotnet build Numerica.slnx -c Release
dotnet test  Numerica.slnx -c Release
```

Run the sample:

```bash
dotnet run --project SAMPLES/Numerica.BasicSample
```

## Project shape

- `Numerica/` — the library. `Numeric` is the **only public type**; everything
  else (`BigRational`, `BigIrrational`, `BigComplex`, `Expr`, `Polynomial`,
  `AlgebraicReal`, `RealMath`) is `internal` and exposed to the tests via
  `InternalsVisibleTo`.
- `TEST/Numerica.Tests/` — xUnit regression tests.
- `SAMPLES/` — runnable examples.
- `DOCS/` — design notes and references.

## Guidelines

- **Code, comments and strings are written in English.**
- Keep the public surface small: prefer adding capability behind `Numeric` (e.g. a
  new typed literal or function) rather than exposing internal types.
- **Add tests** for every behaviour change. The identities the library promises
  (`sqrt(2)*sqrt(2) == 2`, `phi^2 == phi + 1`, `sin^2 + cos^2 == 1`, …) are encoded as
  tests and must stay green.
- Match the existing style; `.editorconfig` captures the conventions and analyzers are
  enabled. Build with `-warnaserror` locally if you want to be strict.
- Watch out for **non-termination**: arbitrary-precision series and Newton iterations
  must provably converge (see the `atan` argument reduction for a cautionary tale).

## Pull requests

1. Fork and branch from `main` (`feature/...` or `fix/...`).
2. Make focused commits with clear messages.
3. Ensure `dotnet build` and `dotnet test` pass.
4. Open the PR against `main` and fill in the template.

By contributing you agree that your work is licensed under the
[MIT License](LICENSE).
