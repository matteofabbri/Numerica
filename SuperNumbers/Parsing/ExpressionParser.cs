using System.Numerics;
using Sprache;

namespace SuperNumbers.Parsing;

/// <summary>
/// Maps strings to <see cref="Expr"/> trees using the Sprache parser-combinator
/// library. Grammar (lowest to highest precedence):
///
///   expr   := term  (('+' | '-') term)*
///   term   := unary (('*' | '/') unary)*
///   unary  := '-' unary | power
///   power  := atom ('^' unary)?          (right associative)
///   atom   := number | ident '(' expr ')' | ident | '(' expr ')'
///
/// Identifiers are constants (pi, e, i) unless followed by '(', in which case they
/// are function calls (sqrt, exp, ln, log, sin, cos, tan, atan).
/// </summary>
internal static class ExpressionParser
{
    public static Expr ParseString(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException("Empty expression.");
        return Expression.End().Parse(text);
    }

    private static readonly Parser<Expr> Number =
        (from whole in Parse.Digit.AtLeastOnce().Text()
         from fraction in
             (from dot in Parse.Char('.')
              from digits in Parse.Digit.AtLeastOnce().Text()
              select digits).Optional()
         select MakeNumber(whole, fraction.GetOrDefault())).Token();

    private static readonly Parser<string> Identifier =
        Parse.Letter.AtLeastOnce().Text().Token();

    // Forward references for the recursive grammar.
    private static readonly Parser<Expr> ExpressionRef = Parse.Ref(() => Expression);
    private static readonly Parser<Expr> UnaryRef = Parse.Ref(() => Unary);

    private static readonly Parser<Expr> Group =
        from open in Parse.Char('(').Token()
        from body in ExpressionRef
        from close in Parse.Char(')').Token()
        select body;

    private static readonly Parser<Expr> IdentifierExpr =
        from name in Identifier
        from call in
            (from open in Parse.Char('(').Token()
             from arg in ExpressionRef
             from close in Parse.Char(')').Token()
             select arg).Optional()
        select call.IsDefined
            ? (Expr)new Expr.Function(name, call.Get())
            : new Expr.Constant(name);

    private static readonly Parser<Expr> Atom = Number.Or(IdentifierExpr).Or(Group);

    private static readonly Parser<Expr> Power =
        from baseValue in Atom
        from exponent in
            (from caret in Parse.Char('^').Token()
             from e in UnaryRef
             select e).Optional()
        select exponent.IsDefined
            ? (Expr)new Expr.Binary('^', baseValue, exponent.Get())
            : baseValue;

    private static readonly Parser<Expr> Unary =
        (from minus in Parse.Char('-').Token()
         from operand in UnaryRef
         select (Expr)new Expr.Negate(operand))
        .Or(Power);

    private static readonly Parser<Expr> Term =
        Parse.ChainOperator(
            Parse.Char('*').Or(Parse.Char('/')).Token(),
            Unary,
            (op, left, right) => new Expr.Binary(op, left, right));

    private static readonly Parser<Expr> Expression =
        Parse.ChainOperator(
            Parse.Char('+').Or(Parse.Char('-')).Token(),
            Term,
            (op, left, right) => new Expr.Binary(op, left, right));

    private static Expr MakeNumber(string whole, string? fraction)
    {
        if (string.IsNullOrEmpty(fraction))
            return new Expr.Number(new BigRational(BigInteger.Parse(whole)));

        BigInteger numerator = BigInteger.Parse(whole + fraction);
        BigInteger denominator = BigInteger.Pow(10, fraction.Length);
        return new Expr.Number(new BigRational(numerator, denominator));
    }
}
