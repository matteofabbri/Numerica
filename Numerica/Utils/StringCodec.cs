using System.Numerics;
using System.Text;

namespace Numerica.Utils;

/// <summary>
/// The order-preserving codec behind <c>string(...)</c> / <c>base64(...)</c> and
/// <see cref="Numeric.AsSpan"/>. A byte string <c>b0 b1 ...</c> is read as the base-256
/// fraction <c>0.b0 b1 ... = (big-endian integer) / 256^count</c> in [0, 1), so numeric
/// order matches the bytes' lexicographic (alphabetical) order even across strings of
/// different lengths — a plain integer ranks any longer string above a shorter one
/// regardless of its leading bytes.
/// </summary>
internal static class StringCodec
{
    /// <summary>A UTF-8 string → its order-preserving fraction in [0, 1).</summary>
    public static BigRational Encode(string text) => FromBytes(Encoding.UTF8.GetBytes(text));

    /// <summary>The UTF-8 string a byte sequence spells — the string-level counterpart of
    /// <see cref="Encode"/>, applied to the bytes from <see cref="Numeric.AsSpan"/>.</summary>
    public static string Decode(ReadOnlySpan<byte> bytes) => Encoding.UTF8.GetString(bytes);

    /// <summary>Bytes → the order-preserving fraction in [0, 1).</summary>
    public static BigRational FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return BigRational.Zero;
        BigInteger value = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        BigInteger scale = BigInteger.One << (8 * bytes.Length); // 256^count
        return new BigRational(value, scale);
    }

    /// <summary>
    /// The bytes a fraction encodes — the inverse of <see cref="FromBytes"/>. A byte string
    /// of length n encodes as <c>(big-endian integer) / 256^n</c>, so a valid fraction has a
    /// power-of-two denominator; this recovers the byte count and the big-endian integer.
    /// Throws when the value is not such a fraction (outside [0, 1), or a denominator that
    /// is not a power of two).
    /// </summary>
    public static byte[] ToBytes(BigRational value)
    {
        if (value.IsZero) return Array.Empty<byte>();            // the empty byte string
        BigInteger num = value.Numerator, den = value.Denominator;
        if (num.Sign < 0 || num >= den)
            throw new InvalidOperationException("Not a base-256 byte fraction: the value must lie in [0, 1).");

        int bits = (int)(den.GetBitLength() - 1);               // den == 2^bits
        if ((BigInteger.One << bits) != den)
            throw new InvalidOperationException("Not a base-256 byte fraction: the denominator must be a power of two.");

        int count = (bits + 7) / 8;                             // bytes needed: ceil(bits / 8)
        BigInteger integer = num << (8 * count - bits);         // restore the big-endian integer
        byte[] raw = integer.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (raw.Length == count) return raw;
        byte[] padded = new byte[count];                        // left-pad dropped leading zeros
        raw.CopyTo(padded, count - raw.Length);
        return padded;
    }
}
