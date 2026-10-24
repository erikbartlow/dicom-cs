namespace Dicom.Encoding;

/// <summary>
/// The byte order a data set's binary values are written in.
/// </summary>
public enum ByteOrder
{
    LittleEndian,
    BigEndian,
}

/// <summary>
/// How a data set is encoded: whether each element carries its value representation,
/// in which byte order binary values are written, and whether the stream or the pixel data
/// is compressed.
/// </summary>
public sealed class TransferSyntax
{
    public TransferSyntax(
        string uid,
        string name,
        bool explicitVr,
        ByteOrder byteOrder,
        bool deflated = false,
        bool encapsulated = false,
        bool lossy = false)
    {
        Uid = uid;
        Name = name;
        ExplicitVr = explicitVr;
        ByteOrder = byteOrder;
        Deflated = deflated;
        Encapsulated = encapsulated;
        Lossy = lossy;
    }

    /// <summary>The unique identifier of the transfer syntax.</summary>
    public string Uid { get; }

    /// <summary>The descriptive name of the transfer syntax.</summary>
    public string Name { get; }

    /// <summary>Whether each data element states its own value representation.</summary>
    public bool ExplicitVr { get; }

    /// <summary>The byte order of binary values.</summary>
    public ByteOrder ByteOrder { get; }

    /// <summary>Whether the data set after the file meta group is deflated.</summary>
    public bool Deflated { get; }

    /// <summary>
    /// Whether pixel data is encapsulated in fragments, which is the case for every
    /// compressed transfer syntax.
    /// </summary>
    public bool Encapsulated { get; }

    /// <summary>Whether the pixel data compression discards information.</summary>
    public bool Lossy { get; }

    /// <summary>Whether binary values need their bytes swapped on this machine.</summary>
    public bool RequiresByteSwap =>
        BitConverter.IsLittleEndian != (ByteOrder == ByteOrder.LittleEndian);

    /// <summary>
    /// Whether pixel data can be read without a decompressor, which is true only for the
    /// uncompressed native syntaxes.
    /// </summary>
    public bool IsNative => !Encapsulated;

    public override string ToString() => $"{Name} ({Uid})";
}
