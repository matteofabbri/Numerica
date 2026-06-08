using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Sprache;

namespace Numerica.Parsing;

/// <summary>
/// Maps strings to <see cref="Expr"/> trees using the Sprache parser-combinator
/// library. Grammar (lowest to highest precedence):
///
///   expr   := term   (('+' | '-') term)*
///   term   := unary  (('*' | '/' | '%') unary | implicit)*   (juxtaposition = '*')
///   implicit := (typedLiteral | call | ident | group) '!'* ('^' unary)?
///   unary  := '-' unary | power
///   power  := factor ('^' unary)?         (right associative)
///   factor := atom '!'*                    (postfix factorial, binds tighter than '^')
///   atom   := number | ident '(' expr (',' expr)* ')' | ident | '(' expr ')'
///
/// Implicit multiplication: a factor next to one that begins with a letter or '(' (never
/// a bare number, never a leading '-') multiplies, so "2pi", "2(x+1)", "2sqrt(2)" and
/// "(a)(b)" are products, while "2 2" and "2 - 3" keep their usual meaning. It is
/// left-associative at the '*' level, so "1/2pi" is "(1/2)*pi".
///
/// Number literals: integers and big integers ("123", "234...90"), decimals ("1.5"),
/// scientific notation ("1.23e5", "-1.23e4", "6.02E23"), and hexadecimal ("0xFF").
/// The '%' operator is the rational remainder (sign of the dividend), at the same
/// precedence as '*' and '/'.
/// Identifiers are function calls when followed by '('. Unary functions: sqrt, cbrt,
/// exp, ln (alias log), log10, log2, sin, cos, tan, asin, acos, atan, sinh, cosh, tanh,
/// asinh, acosh, atanh, abs. Two-argument functions: atan2(y, x), root(x, n),
/// logb(x, base) (and log(x, base)), mod(a, b). Variadic reductions: min, max, gcd, lcm.
/// A postfix '!' is factorial (exact, non-negative integers): "5!" == "fact(5)" == 120.
/// Otherwise identifiers are constants:
/// "true"/"false" -> 1/0, pi (or the symbol π), tau/τ (2·pi), e, i, phi/φ (the golden
/// ratio), omega/Ω, catalan, and the Euler-Mascheroni constant (egamma, gamma or γ).
/// Unicode escapes of the form \uXXXX in the input are decoded first, so "π" reads as pi.
///
/// Uniform typed-literal forms spell a primitive as keyword(&lt;content&gt;), with or
/// without double quotes (only " is used, never '): bool(true)/bool("false") -> 1/0;
/// char(A)/char("A") -> the Unicode code point; int(34567) -> an integer of any size;
/// float(2134,23)/float("2134.23") -> an exact BigRational (the decimal separator is
/// culture-invariant: '.' and ',' both work); rational("3/7") -> an exact fraction;
/// complex(3+4i) -> a complex number built from existing nodes; hex(FF)/bin(1010)/oct(17)
/// -> integers in base 16/2/8 (optional 0x/0b/0o prefix, '_' separators); string("ciao")
/// -> UTF-8 bytes read big-endian as an unsigned integer; base64("SGVsbG8=") -> decoded
/// bytes the same way (standard or URL-safe alphabet); timespan(1:00:00) -> tick count;
/// datetime(2024-01-15T10:30:00Z) -> UTC ticks; guid(...) -> the 128 bits as an integer.
/// </summary>
internal static class ExpressionParser
{
    public static Expr ParseString(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException("Empty expression.");
        return Expression.End().Parse(DecodeUnicodeEscapes(text));
    }

    // ---------- number literals ----------

    private static readonly Parser<Expr> HexNumber =
        (from prefix in Parse.IgnoreCase("0x")
         from digits in Parse.Chars("0123456789abcdefABCDEF").AtLeastOnce().Text()
         select (Expr)new Expr.Number(new BigRational(ParseHex(digits)))).Token();

    private static readonly Parser<Expr> DecimalNumber =
        (from whole in Parse.Digit.AtLeastOnce().Text()
         from fraction in
             (from dot in Parse.Char('.')
              from digits in Parse.Digit.AtLeastOnce().Text()
              select digits).Optional()
         from exponent in
             (from e in Parse.IgnoreCase("e")
              from sign in Parse.Chars("+-").Optional()
              from digits in Parse.Digit.AtLeastOnce().Text()
              select (sign.IsDefined ? sign.Get().ToString() : "+") + digits).Optional()
         select MakeNumber(whole, fraction.GetOrDefault(), exponent.GetOrDefault())).Token();

    private static readonly Parser<Expr> Number = HexNumber.Or(DecimalNumber);

    // A letter followed by letters or digits, so names like "log2", "log10" and
    // "atan2" are single identifiers (a leading digit is always a number instead).
    private static readonly Parser<string> Identifier =
        (from first in Parse.Letter
         from rest in Parse.LetterOrDigit.Many().Text()
         select first + rest).Token();

    // Forward references for the recursive grammar.
    private static readonly Parser<Expr> ExpressionRef = Parse.Ref(() => Expression);
    private static readonly Parser<Expr> UnaryRef = Parse.Ref(() => Unary);

    private static readonly Parser<Expr> Group =
        from open in Parse.Char('(').Token()
        from body in ExpressionRef
        from close in Parse.Char(')').Token()
        select body;

    // A parenthesised, comma-separated argument list: "(a)", "(a, b)", "(y, x)".
    private static readonly Parser<IEnumerable<Expr>> ArgumentList =
        from open in Parse.Char('(').Token()
        from args in ExpressionRef.DelimitedBy(Parse.Char(',').Token())
        from close in Parse.Char(')').Token()
        select args;

    private static readonly Parser<Expr> IdentifierExpr =
        from name in Identifier
        from call in ArgumentList.Optional()
        select MakeIdentifier(name, call);

    // A C#-style double-quoted literal, with the usual escapes (\n \t \\ \" \uXXXX \xH... \UXXXXXXXX).
    // Only double quotes are used -- there is no separate char-vs-string quoting here.
    private static readonly Parser<string> QuotedString =
        from open in Parse.Char('"')
        from body in
            ((from bs in Parse.Char('\\') from c in Parse.AnyChar select "\\" + c)
             .Or(Parse.CharExcept(c => c == '"' || c == '\\', "string character").Select(c => c.ToString())))
            .Many()
        from close in Parse.Char('"')
        select Unescape(string.Concat(body));

    // A typed-literal form "keyword(<content>)" -- the content is either a quoted string
    // (with escapes) or raw text up to ')', handed to a type-specific converter. This is the
    // one uniform way to spell a primitive, with or without quotes:
    //   bool(true) / bool("false") / char(A) / char("A") / int(34567) / int("34567")
    //   float(2134,23) / float("2134.23") / string("ciao") / datetime(2024-01-15T10:30:00Z)
    private static Parser<Expr> TypedLiteral(string keyword, Func<string, Expr> convert) =>
        (from kw in Parse.IgnoreCase(keyword)
         from open in Parse.Char('(')
         from content in QuotedString.Or(Parse.CharExcept(')').Many().Text())
         from close in Parse.Char(')')
         select convert(content)).Token();

    private static readonly Parser<Expr> TypedLiterals =
        TypedLiteral("bool", MakeBool)
        .Or(TypedLiteral("char", MakeChar))
        .Or(TypedLiteral("int", MakeInt))
        .Or(TypedLiteral("float", MakeFloat))
        .Or(TypedLiteral("rational", MakeRational))
        .Or(TypedLiteral("complex", MakeComplex))
        .Or(TypedLiteral("hex", MakeHex))
        .Or(TypedLiteral("bin", MakeBin))
        .Or(TypedLiteral("oct", MakeOct))
        .Or(TypedLiteral("string", MakeString))
        .Or(TypedLiteral("base64", MakeBase64))
        .Or(TypedLiteral("timespan", MakeTimeSpan))
        .Or(TypedLiteral("datetime", MakeDateTime))
        .Or(TypedLiteral("guid", MakeGuid));

    private static readonly Parser<Expr> Atom =
        Number.Or(TypedLiterals).Or(IdentifierExpr).Or(Group);

    // Postfix factorial: each trailing '!' wraps the value in fact(...). Binds tighter
    // than '^', so "2^3!" is "2^(3!)" and "-3!" is "-(3!)".
    private static readonly Parser<Expr> Factor =
        from atom in Atom
        from bangs in Parse.Char('!').Token().Many()
        select bangs.Aggregate(atom, (e, _) => (Expr)new Expr.Function("fact", new[] { e }));

    private static readonly Parser<Expr> Power =
        from baseValue in Factor
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

    // A factor reachable by juxtaposition (implicit multiplication). It must begin with a
    // letter or '(' -- never a bare number and never a leading '-' -- so "2pi", "2(x+1)",
    // "2sqrt(2)" and "(a)(b)" multiply, while "2 2" and "2 - 3" keep their usual meaning.
    // Supports postfix '!' and '^' like a normal power, so "2pi^2" is "2*(pi^2)".
    private static readonly Parser<Expr> ImplicitFactor =
        from atom in TypedLiterals.Or(IdentifierExpr).Or(Group)
        from bangs in Parse.Char('!').Token().Many()
        from exponent in
            (from caret in Parse.Char('^').Token()
             from e in UnaryRef
             select e).Optional()
        let withBangs = bangs.Aggregate(atom, (e, _) => (Expr)new Expr.Function("fact", new[] { e }))
        select exponent.IsDefined ? (Expr)new Expr.Binary('^', withBangs, exponent.Get()) : withBangs;

    // Multiplicative level: explicit '*' '/' '%' (right side may be unary, e.g. "2 * -3"),
    // plus implicit multiplication by juxtaposition. All left-associative and at the same
    // precedence, so "1/2pi" is "(1/2)*pi".
    private static readonly Parser<Expr> Term =
        from first in Unary
        from rest in
            ((from op in Parse.Chars("*/%").Token()
              from factor in UnaryRef
              select (Op: op, Factor: factor))
             .Or(from factor in ImplicitFactor
                 select (Op: '*', Factor: factor)))
            .Many()
        select rest.Aggregate(first, (left, t) => (Expr)new Expr.Binary(t.Op, left, t.Factor));

    private static readonly Parser<Expr> Expression =
        Parse.ChainOperator(
            Parse.Char('+').Or(Parse.Char('-')).Token(),
            Term,
            (op, left, right) => new Expr.Binary(op, left, right));

    // ---------- builders ----------

    private static Expr MakeIdentifier(string name, IOption<IEnumerable<Expr>> call)
    {
        if (call.IsDefined)
        {
            var args = call.Get().ToList();
            // pow(a, b) is sugar for a ^ b, reusing the power evaluation across the tower.
            if (name == "pow" && args.Count == 2)
                return new Expr.Binary('^', args[0], args[1]);
            return new Expr.Function(name, args);
        }
        return name switch
        {
            "true" => new Expr.Number(BigRational.One),
            "false" => new Expr.Number(BigRational.Zero),
            "π" or "Π" => new Expr.Constant(Expr.Constant.Pi),
            "τ" => new Expr.Constant(Expr.Constant.Tau),
            "φ" or "Φ" => new Expr.Constant(Expr.Constant.Phi),
            "Ω" or "ω" => new Expr.Constant(Expr.Constant.Omega),
            "γ" => new Expr.Constant(Expr.Constant.EulerGamma),
            _ => new Expr.Constant(name),
        };
    }

    private static Expr MakeNumber(string whole, string? fraction, string? exponent)
    {
        string fracDigits = fraction ?? string.Empty;
        BigInteger mantissa = BigInteger.Parse(whole + fracDigits, CultureInfo.InvariantCulture);
        int tenPower = (string.IsNullOrEmpty(exponent) ? 0 : int.Parse(exponent, CultureInfo.InvariantCulture))
                       - fracDigits.Length;

        return tenPower >= 0
            ? new Expr.Number(new BigRational(mantissa * BigInteger.Pow(10, tenPower)))
            : new Expr.Number(new BigRational(mantissa, BigInteger.Pow(10, -tenPower)));
    }

    // Parse hex digits as a non-negative integer (leading "0" keeps NumberStyles.HexNumber positive).
    private static BigInteger ParseHex(string digits)
        => BigInteger.Parse("0" + digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    // A DateTime / DateTimeOffset (any .NET or ISO-8601 format) becomes its UTC tick count.
    private static Expr MakeDateTime(string body)
    {
        const DateTimeStyles styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces;
        string text = body.Trim();

        BigInteger ticks;
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, styles, out DateTimeOffset offset))
            ticks = offset.UtcTicks;
        else if (DateTime.TryParse(text, CultureInfo.InvariantCulture, styles, out DateTime dateTime))
            ticks = dateTime.ToUniversalTime().Ticks;
        else
            throw new FormatException($"'{text}' is not a valid DateTime or DateTimeOffset.");

        return new Expr.Number(new BigRational(ticks));
    }

    // bool -> 1 / 0 (case-insensitive, like bool.Parse).
    private static Expr MakeBool(string content)
    {
        if (bool.TryParse(content.Trim(), out bool value))
            return new Expr.Number(value ? BigRational.One : BigRational.Zero);
        throw new FormatException($"'{content}' is not a valid bool.");
    }

    // char -> its Unicode code point (a single character, or one surrogate pair).
    private static Expr MakeChar(string content)
    {
        if (content.Length == 1)
            return new Expr.Number(new BigRational(content[0]));
        if (content.Length == 2 && char.IsHighSurrogate(content[0]) && char.IsLowSurrogate(content[1]))
            return new Expr.Number(new BigRational(char.ConvertToUtf32(content[0], content[1])));
        throw new FormatException($"char(...) expects exactly one character, got '{content}'.");
    }

    // int -> an integer of any size (culture-invariant).
    private static Expr MakeInt(string content)
        => new Expr.Number(new BigRational(BigInteger.Parse(content.Trim(), CultureInfo.InvariantCulture)));

    // float -> an exact decimal as a BigRational. The decimal separator is culture-invariant:
    // both '.' and ',' are accepted and mean the same thing.
    private static Expr MakeFloat(string content)
    {
        string text = content.Trim().Replace(',', '.');
        bool negative = false;
        if (text.StartsWith('-')) { negative = true; text = text[1..]; }
        else if (text.StartsWith('+')) { text = text[1..]; }

        int exponent = 0;
        int e = text.IndexOfAny(new[] { 'e', 'E' });
        if (e >= 0)
        {
            exponent = int.Parse(text[(e + 1)..], CultureInfo.InvariantCulture);
            text = text[..e];
        }

        int dot = text.IndexOf('.');
        string digits = dot >= 0 ? text[..dot] + text[(dot + 1)..] : text;
        int fractionLength = dot >= 0 ? text.Length - dot - 1 : 0;
        if (digits.Length == 0) digits = "0";

        BigInteger mantissa = BigInteger.Parse(digits, CultureInfo.InvariantCulture);
        if (negative) mantissa = -mantissa;

        int tenPower = exponent - fractionLength;
        BigRational value = tenPower >= 0
            ? new BigRational(mantissa * BigInteger.Pow(10, tenPower))
            : new BigRational(mantissa, BigInteger.Pow(10, -tenPower));
        return new Expr.Number(value);
    }

    // rational -> an exact fraction "a", "a/b" or "-a/b" (whitespace allowed around '/').
    private static Expr MakeRational(string content)
        => new Expr.Number(BigRational.Parse(content));

    // hex / bin / oct -> an integer in base 16 / 2 / 8. A leading sign, an optional base
    // prefix (0x / 0b / 0o) and '_' digit separators are all accepted.
    private static Expr MakeHex(string content) => IntegerInBase(content, 16, "0x");
    private static Expr MakeBin(string content) => IntegerInBase(content, 2, "0b");
    private static Expr MakeOct(string content) => IntegerInBase(content, 8, "0o");

    private static Expr IntegerInBase(string content, int radix, string prefix)
    {
        string text = content.Trim().Replace("_", string.Empty);
        bool negative = false;
        if (text.StartsWith('-')) { negative = true; text = text[1..]; }
        else if (text.StartsWith('+')) text = text[1..];
        if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) text = text[prefix.Length..];
        if (text.Length == 0) throw new FormatException($"Empty base-{radix} integer literal.");

        BigInteger value = BigInteger.Zero;
        foreach (char c in text)
        {
            int digit = DigitValue(c);
            if (digit < 0 || digit >= radix)
                throw new FormatException($"'{c}' is not a base-{radix} digit.");
            value = value * radix + digit;
        }
        return new Expr.Number(new BigRational(negative ? -value : value));
    }

    private static int DigitValue(char c)
        => c is >= '0' and <= '9' ? c - '0'
         : c is >= 'a' and <= 'z' ? c - 'a' + 10
         : c is >= 'A' and <= 'Z' ? c - 'A' + 10
         : -1;

    // timespan -> its tick count (100 ns), the duration counterpart of datetime.
    private static Expr MakeTimeSpan(string content)
    {
        if (TimeSpan.TryParse(content.Trim(), CultureInfo.InvariantCulture, out TimeSpan value))
            return new Expr.Number(new BigRational(value.Ticks));
        throw new FormatException($"'{content}' is not a valid TimeSpan.");
    }

    // guid -> its 128 bits read as the unsigned integer spelled by the canonical text
    // (big-endian: the same order a human reads the hex digits).
    private static Expr MakeGuid(string content)
    {
        if (!Guid.TryParse(content.Trim(), out Guid value))
            throw new FormatException($"'{content}' is not a valid Guid.");
        BigInteger number = BigInteger.Parse(
            "0" + value.ToString("N"), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new Expr.Number(new BigRational(number));
    }

    // ----- complex literal sub-grammar (used only by complex(...)) -----

    // A non-negative decimal -> exact BigRational ("3", "2.5").
    private static readonly Parser<BigRational> UnsignedDecimal =
        from whole in Parse.Digit.AtLeastOnce().Text()
        from fraction in
            (from dot in Parse.Char('.')
             from digits in Parse.Digit.AtLeastOnce().Text()
             select digits).Optional()
        select DecimalToRational(whole, fraction.GetOrDefault());

    // One signed component: a number (with optional trailing 'i') or a bare 'i'.
    private static readonly Parser<Expr> ComplexComponent =
        from sign in Parse.Chars("+-").Token().Optional()
        from body in
            (from magnitude in UnsignedDecimal.Token()
             from imaginary in Parse.Char('i').Optional()
             select ComponentExpr(magnitude, imaginary.IsDefined))
            .Or(from i in Parse.Char('i').Token() select ComponentExpr(BigRational.One, imaginary: true))
        select sign.IsDefined && sign.Get() == '-' ? new Expr.Negate(body) : body;

    // A sum of components: "a", "bi", "a+bi", "a-bi", bare "i"/"-i", decimals, whitespace.
    private static readonly Parser<Expr> ComplexLiteral =
        from first in ComplexComponent
        from rest in ComplexComponent.Many()
        select rest.Aggregate(first, (acc, term) => (Expr)new Expr.Binary('+', acc, term));

    private static Expr ComponentExpr(BigRational magnitude, bool imaginary)
        => imaginary
            ? new Expr.Binary('*', new Expr.Number(magnitude), new Expr.Constant(Expr.Constant.I))
            : new Expr.Number(magnitude);

    private static BigRational DecimalToRational(string whole, string? fraction)
    {
        string frac = fraction ?? string.Empty;
        BigInteger mantissa = BigInteger.Parse(whole + frac, CultureInfo.InvariantCulture);
        return frac.Length == 0
            ? new BigRational(mantissa)
            : new BigRational(mantissa, BigInteger.Pow(10, frac.Length));
    }

    // complex -> a + bi built from existing nodes, so it evaluates at the complex level.
    private static Expr MakeComplex(string content)
    {
        var result = ComplexLiteral.End().TryParse(content.Trim());
        if (!result.WasSuccessful)
            throw new FormatException($"'{content}' is not a valid complex literal.");
        return result.Value;
    }

    // string -> UTF-8 bytes -> big-endian unsigned BigInteger.
    private static Expr MakeString(string content) => BytesToNumber(Encoding.UTF8.GetBytes(content));

    // base64 -> bytes -> big-endian unsigned BigInteger. Accepts both standard ('+', '/')
    // and URL-safe ('-', '_') alphabets, with or without '=' padding.
    private static Expr MakeBase64(string content)
    {
        string text = content.Trim().Replace('-', '+').Replace('_', '/');
        switch (text.Length % 4)
        {
            case 2: text += "=="; break;
            case 3: text += "="; break;
        }
        return BytesToNumber(Convert.FromBase64String(text));
    }

    private static Expr BytesToNumber(byte[] bytes)
    {
        BigInteger value = bytes.Length == 0
            ? BigInteger.Zero
            : new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        return new Expr.Number(new BigRational(value));
    }

    // Applies C# string-literal escape semantics to the raw quoted body.
    private static string Unescape(string s)
    {
        var sb = new StringBuilder(s.Length);
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i++];
            if (c != '\\') { sb.Append(c); continue; }
            if (i >= s.Length) throw new FormatException("Dangling escape in string literal.");

            char e = s[i++];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case '0': sb.Append('\0'); break;
                case 'a': sb.Append('\a'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'v': sb.Append('\v'); break;
                case '\\': sb.Append('\\'); break;
                case '"': sb.Append('"'); break;
                case '\'': sb.Append('\''); break;
                case 'u': sb.Append((char)ReadHex(s, ref i, 4, 4)); break;
                case 'x': sb.Append((char)ReadHex(s, ref i, 1, 4)); break;
                case 'U': sb.Append(char.ConvertFromUtf32(ReadHex(s, ref i, 8, 8))); break;
                default: throw new FormatException($"Unrecognized escape '\\{e}' in string literal.");
            }
        }
        return sb.ToString();
    }

    private static int ReadHex(string s, ref int i, int min, int max)
    {
        int value = 0, count = 0;
        while (count < max && i < s.Length && Uri.IsHexDigit(s[i]))
        {
            value = value * 16 + Convert.ToInt32(s[i].ToString(), 16);
            i++;
            count++;
        }
        if (count < min) throw new FormatException("Invalid hex escape in string literal.");
        return value;
    }

    private static string DecodeUnicodeEscapes(string text)
        => Regex.Replace(text, @"\\u([0-9A-Fa-f]{4})",
            m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
}
