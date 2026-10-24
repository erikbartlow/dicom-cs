using Dicom.Core;

namespace Dicom.Parser;

/// <summary>
/// The kinds of step a data set is read in.
/// </summary>
public enum DataSetTokenKind
{
    /// <summary>The header of an element whose value follows as a single token.</summary>
    ElementHeader,

    /// <summary>The decoded value of the element last announced.</summary>
    PrimitiveValue,

    /// <summary>The start of a sequence of items.</summary>
    SequenceStart,

    /// <summary>The end of a sequence of items or of encapsulated pixel data.</summary>
    SequenceEnd,

    /// <summary>The start of one item of a sequence.</summary>
    ItemStart,

    /// <summary>The end of one item of a sequence.</summary>
    ItemEnd,

    /// <summary>The start of encapsulated pixel data.</summary>
    PixelSequenceStart,

    /// <summary>One fragment of encapsulated pixel data, or the basic offset table when first.</summary>
    ItemValue,
}

/// <summary>
/// One step of reading a data set. Values are carried only by the token kinds that have them.
/// </summary>
public readonly struct DataSetToken
{
    private DataSetToken(DataSetTokenKind kind, DataElementHeader header, PrimitiveValue? value, byte[]? bytes)
    {
        Kind = kind;
        Header = header;
        Value = value;
        Bytes = bytes;
    }

    /// <summary>Which step this token represents.</summary>
    public DataSetTokenKind Kind { get; }

    /// <summary>The header the token relates to, where one applies.</summary>
    public DataElementHeader Header { get; }

    /// <summary>The decoded value, for a <see cref="DataSetTokenKind.PrimitiveValue"/> token.</summary>
    public PrimitiveValue? Value { get; }

    /// <summary>The raw bytes, for an <see cref="DataSetTokenKind.ItemValue"/> token.</summary>
    public byte[]? Bytes { get; }

    /// <summary>The tag the token relates to.</summary>
    public Tag Tag => Header.Tag;

    public static DataSetToken Element(DataElementHeader header) =>
        new(DataSetTokenKind.ElementHeader, header, null, null);

    public static DataSetToken Primitive(DataElementHeader header, PrimitiveValue value) =>
        new(DataSetTokenKind.PrimitiveValue, header, value, null);

    public static DataSetToken SequenceStart(DataElementHeader header) =>
        new(DataSetTokenKind.SequenceStart, header, null, null);

    public static DataSetToken PixelSequenceStart(DataElementHeader header) =>
        new(DataSetTokenKind.PixelSequenceStart, header, null, null);

    public static DataSetToken SequenceEnd() =>
        new(DataSetTokenKind.SequenceEnd, default, null, null);

    public static DataSetToken ItemStart(DataElementHeader header) =>
        new(DataSetTokenKind.ItemStart, header, null, null);

    public static DataSetToken ItemEnd() =>
        new(DataSetTokenKind.ItemEnd, default, null, null);

    public static DataSetToken ItemValue(byte[] bytes) =>
        new(DataSetTokenKind.ItemValue, default, null, bytes);

    public override string ToString() => Kind switch
    {
        DataSetTokenKind.PrimitiveValue => $"{Kind} {Value}",
        DataSetTokenKind.ItemValue => $"{Kind} {Bytes?.Length ?? 0} byte(s)",
        DataSetTokenKind.SequenceEnd or DataSetTokenKind.ItemEnd => Kind.ToString(),
        _ => $"{Kind} {Header}",
    };
}
