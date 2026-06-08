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
///   expr   := term  (('+' | '-') term)*
///   term   := unary (('*' | '/') unary)*
///   unary  := '-' unary | power
///   power  := atom ('^' unary)?          (right associative)
///   atom   := number | ident '(' expr (',' expr)* ')' | ident | '(' expr ')'
///
/// Number literals: integers and big integers ("123", "234...90"), decimals ("1.5"),
/// scientific notation ("1.23e5", "-1.23e4", "6.02E23"), and hexadecimal ("0xFF").
/// Identifiers are function calls when followed by '('. Unary functions: sqrt, cbrt,
/// exp, ln (alias log), log10, log2, sin, cos, tan, asin, acos, atan, sinh, cosh, tanh,
/// asinh, acosh, atanh, abs. Two-argument functions: atan2(y, x), root(x, n),
/// logb(x, base) (and log(x, base)). Otherwise identifiers are constants:
/// "true"/"false" -> 1/0, pi (or the symbol π), tau/τ (2·pi), e, i, phi/φ (the golden
/// ratio), and the omega constant (omega or the symbol Ω). Unicode escapes of the form
/// \uXXXX in the input are decoded first, so "π" reads as pi.
///
/// Uniform typed-literal forms spell a primitive as keyword(&lt;content&gt;), with or
/// without double quotes (only " is used, never '): bool(true)/bool("false") -> 1/0;
/// char(A)/char("A") -> the Unicode code point; int(34567) -> an integer of any size;
/// float(2134,23)/float("2134.23") -> an exact BigRational (the decimal separator is
/// culture-invariant: '.' and ',' both work); string("ciao") -> UTF-8 bytes read
/// big-endian as an unsigned integer; base64("SGVsbG8=") -> decoded bytes the same way
/// (standard or URL-safe alphabet); datetime(2024-01-15T10:30:00Z) -> UTC ticks.
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
        .Or(TypedLiteral("string", MakeString))
        .Or(TypedLiteral("base64", MakeBase64))
        .Or(TypedLiteral("datetime", MakeDateTime));

    private static readonly Parser<Expr> Atom =
        Number.Or(TypedLiterals).Or(IdentifierExpr).Or(Group);

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

    // ---------- builders ----------

    private static Expr MakeIdentifier(string name, IOption<IEnumerable<Expr>> call)
    {
        if (call.IsDefined) return new Expr.Function(name, call.Get().ToList());
        return name switch
        {
            "true" => new Expr.Number(BigRational.One),
            "false" => new Expr.Number(BigRational.Zero),
            "π" or "Π" => new Expr.Constant(Expr.Constant.Pi),
            "τ" => new Expr.Constant(Expr.Constant.Tau),
            "φ" or "Φ" => new Expr.Constant(Expr.Constant.Phi),
            "Ω" or "ω" => new Expr.Constant(Expr.Constant.Omega),
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
