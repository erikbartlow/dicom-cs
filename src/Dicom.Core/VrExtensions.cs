using System.Text;

namespace Dicom.Core;

/// <summary>
/// Metadata about each value representation: its two-letter code, how it is framed on the wire,
/// how its values are padded, and the size of its individual binary items.
/// </summary>
public static class VrExtensions
{
    /// <summary>The two ASCII letters identifying the value representation, such as "PN".</summary>
    public static string Code(this Vr vr) => string.Create(2, vr, static (span, v) =>
    {
        span[0] = (char)((ushort)v >> 8);
        span[1] = (char)((ushort)v & 0xFF);
    });

    /// <summary>Writes the two-letter code as ASCII bytes.</summary>
    public static void WriteCode(this Vr vr, Span<byte> destination)
    {
        destination[0] = (byte)((ushort)vr >> 8);
        destination[1] = (byte)((ushort)vr & 0xFF);
    }

    /// <summary>
    /// Whether an explicit VR data element with this representation uses the long header form:
    /// two reserved bytes followed by a 32-bit length, rather than a 16-bit length.
    /// </summary>
    public static bool HasLongExplicitHeader(this Vr vr) => vr switch
    {
        Vr.OB or Vr.OD or Vr.OF or Vr.OL or Vr.OV or Vr.OW or Vr.SQ or Vr.SV
            or Vr.UC or Vr.UN or Vr.UR or Vr.UT or Vr.UV => true,
        _ => false,
    };

    /// <summary>
    /// Whether the value is a sequence of text characters rather than binary data.
    /// </summary>
    public static bool IsText(this Vr vr) => vr switch
    {
        Vr.AE or Vr.AS or Vr.CS or Vr.DA or Vr.DS or Vr.DT or Vr.IS or Vr.LO or Vr.LT
            or Vr.PN or Vr.SH or Vr.ST or Vr.TM or Vr.UC or Vr.UI or Vr.UR or Vr.UT => true,
        _ => false,
    };

    /// <summary>
    /// Whether values of this representation may be interpreted with the data set's specific character set.
    /// The remaining text representations are restricted to the default repertoire.
    /// </summary>
    public static bool IsCharacterSetAware(this Vr vr) => vr switch
    {
        Vr.LO or Vr.LT or Vr.PN or Vr.SH or Vr.ST or Vr.UC or Vr.UT => true,
        _ => false,
    };

    /// <summary>
    /// Whether multiple values are separated by a backslash. Text representations that may legitimately
    /// contain a backslash, and all binary representations, are single valued.
    /// </summary>
    public static bool IsMultiValued(this Vr vr) => vr switch
    {
        Vr.LT or Vr.ST or Vr.UT or Vr.UR => false,
        _ => vr.IsText(),
    };

    /// <summary>
    /// The byte used to pad a value to an even length: a space for most text representations,
    /// and a null byte for unique identifiers and binary data.
    /// </summary>
    public static byte PaddingByte(this Vr vr) => vr switch
    {
        Vr.UI or Vr.OB or Vr.OD or Vr.OF or Vr.OL or Vr.OV or Vr.OW or Vr.UN => 0x00,
        _ => 0x20,
    };

    /// <summary>
    /// The size in bytes of a single binary item, which is also the width used when swapping byte order.
    /// Text and opaque representations report one.
    /// </summary>
    public static int ItemSize(this Vr vr) => vr switch
    {
        Vr.AT or Vr.OW or Vr.SS or Vr.US => 2,
        Vr.FL or Vr.OF or Vr.OL or Vr.SL or Vr.UL => 4,
        Vr.FD or Vr.OD or Vr.OV or Vr.SV or Vr.UV => 8,
        _ => 1,
    };

    /// <summary>
    /// Parses a two-letter value representation code.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> code, out Vr vr)
    {
        vr = default;

        if (code.Length != 2)
            return false;

        return IsDefinedCode((ushort)((code[0] << 8) | code[1]), out vr);
    }

    /// <summary>
    /// Parses a two-letter value representation code from its ASCII bytes.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> code, out Vr vr)
    {
        vr = default;

        if (code.Length != 2)
            return false;

        return IsDefinedCode((ushort)((code[0] << 8) | code[1]), out vr);
    }

    /// <summary>
    /// Parses a two-letter value representation code, falling back to <see cref="Vr.UN"/> when unrecognised.
    /// </summary>
    public static Vr ParseOrUnknown(ReadOnlySpan<char> code) => TryParse(code, out Vr vr) ? vr : Vr.UN;

    /// <summary>
    /// Parses a two-letter value representation code, falling back to <see cref="Vr.UN"/> when unrecognised.
    /// </summary>
    public static Vr ParseOrUnknown(ReadOnlySpan<byte> code) => TryParse(code, out Vr vr) ? vr : Vr.UN;

    /// <summary>The text encoding used for representations restricted to the default repertoire.</summary>
    public static Encoding DefaultEncoding { get; } = Encoding.Latin1;

    private static bool IsDefinedCode(ushort raw, out Vr vr)
    {
        vr = (Vr)raw;
        return Enum.IsDefined(vr);
    }
}
