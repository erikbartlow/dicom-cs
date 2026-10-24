namespace Dicom.Core;

/// <summary>
/// The tag, value representation and length that precede a data element's value on the wire.
/// </summary>
public readonly struct DataElementHeader : IEquatable<DataElementHeader>
{
    public DataElementHeader(Tag tag, Vr vr, Length length)
    {
        Tag = tag;
        Vr = vr;
        Length = length;
    }

    public DataElementHeader(Tag tag, Vr vr, uint length)
        : this(tag, vr, new Length(length))
    {
    }

    /// <summary>The attribute's tag.</summary>
    public Tag Tag { get; }

    /// <summary>The attribute's value representation.</summary>
    public Vr Vr { get; }

    /// <summary>The length in bytes of the attribute's value.</summary>
    public Length Length { get; }

    /// <summary>Whether this header introduces a sequence of items.</summary>
    public bool IsSequence => Vr == Vr.SQ;

    /// <summary>Whether this header introduces encapsulated pixel data held as fragments.</summary>
    public bool IsEncapsulatedPixelData => Tag == Tags.PixelData && Length.IsUndefined;

    /// <summary>Whether this is an item start, item delimitation or sequence delimitation header.</summary>
    public bool IsDelimiter => Tag.IsDelimiter;

    public bool Equals(DataElementHeader other) =>
        Tag == other.Tag && Vr == other.Vr && Length == other.Length;

    public override bool Equals(object? obj) => obj is DataElementHeader other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Tag, Vr, Length);

    public override string ToString() => $"{Tag} {Vr.Code()} {Length}";

    public static bool operator ==(DataElementHeader left, DataElementHeader right) => left.Equals(right);

    public static bool operator !=(DataElementHeader left, DataElementHeader right) => !left.Equals(right);
}

/// <summary>
/// The handful of tags the encoding layer needs to recognise by name.
/// The full catalogue lives in the standard dictionary.
/// </summary>
public static class Tags
{
    public static readonly Tag SpecificCharacterSet = new(0x0008, 0x0005);
    public static readonly Tag PixelData = new(0x7FE0, 0x0010);
    public static readonly Tag FloatPixelData = new(0x7FE0, 0x0008);
    public static readonly Tag DoubleFloatPixelData = new(0x7FE0, 0x0009);
    public static readonly Tag TransferSyntaxUid = new(0x0002, 0x0010);
    public static readonly Tag FileMetaInformationGroupLength = new(0x0002, 0x0000);
    public static readonly Tag MediaStorageSopClassUid = new(0x0002, 0x0002);
    public static readonly Tag MediaStorageSopInstanceUid = new(0x0002, 0x0003);
    public static readonly Tag ImplementationClassUid = new(0x0002, 0x0012);
    public static readonly Tag ImplementationVersionName = new(0x0002, 0x0013);
    public static readonly Tag FileMetaInformationVersion = new(0x0002, 0x0001);
    public static readonly Tag SopClassUid = new(0x0008, 0x0016);
    public static readonly Tag SopInstanceUid = new(0x0008, 0x0018);
    public static readonly Tag Rows = new(0x0028, 0x0010);
    public static readonly Tag Columns = new(0x0028, 0x0011);
    public static readonly Tag NumberOfFrames = new(0x0028, 0x0008);
    public static readonly Tag SamplesPerPixel = new(0x0028, 0x0002);
    public static readonly Tag PhotometricInterpretation = new(0x0028, 0x0004);
    public static readonly Tag PlanarConfiguration = new(0x0028, 0x0006);
    public static readonly Tag BitsAllocated = new(0x0028, 0x0100);
    public static readonly Tag BitsStored = new(0x0028, 0x0101);
    public static readonly Tag HighBit = new(0x0028, 0x0102);
    public static readonly Tag PixelRepresentation = new(0x0028, 0x0103);
    public static readonly Tag PixelPaddingValue = new(0x0028, 0x0120);
    public static readonly Tag RescaleIntercept = new(0x0028, 0x1052);
    public static readonly Tag RescaleSlope = new(0x0028, 0x1053);
    public static readonly Tag WindowCenter = new(0x0028, 0x1050);
    public static readonly Tag WindowWidth = new(0x0028, 0x1051);
}
