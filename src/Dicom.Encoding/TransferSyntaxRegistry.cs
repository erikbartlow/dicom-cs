using System.Collections.Concurrent;

namespace Dicom.Encoding;

/// <summary>
/// The transfer syntaxes this library recognises. The uncompressed syntaxes can be read and
/// written in full; the compressed ones can be read and written as encapsulated fragments,
/// but the fragments are not decoded. A syntax may be registered while other threads are
/// looking syntaxes up.
/// </summary>
public static class TransferSyntaxRegistry
{
    /// <summary>Implicit VR little endian, the default when none is stated.</summary>
    public static TransferSyntax ImplicitVrLittleEndian { get; } =
        new("1.2.840.10008.1.2", "Implicit VR Little Endian", explicitVr: false, ByteOrder.LittleEndian);

    /// <summary>Explicit VR little endian, the usual encoding for stored files.</summary>
    public static TransferSyntax ExplicitVrLittleEndian { get; } =
        new("1.2.840.10008.1.2.1", "Explicit VR Little Endian", explicitVr: true, ByteOrder.LittleEndian);

    /// <summary>Explicit VR little endian with the data set deflated.</summary>
    public static TransferSyntax DeflatedExplicitVrLittleEndian { get; } =
        new("1.2.840.10008.1.2.1.99", "Deflated Explicit VR Little Endian", explicitVr: true,
            ByteOrder.LittleEndian, deflated: true);

    /// <summary>Explicit VR big endian, retired but still found in older archives.</summary>
    public static TransferSyntax ExplicitVrBigEndian { get; } =
        new("1.2.840.10008.1.2.2", "Explicit VR Big Endian", explicitVr: true, ByteOrder.BigEndian);

    /// <summary>The transfer syntax assumed for a data set with no file meta group.</summary>
    public static TransferSyntax Default => ImplicitVrLittleEndian;

    private static readonly ConcurrentDictionary<string, TransferSyntax> ByUid = Build();

    /// <summary>Every recognised transfer syntax.</summary>
    public static IReadOnlyCollection<TransferSyntax> All => [.. ByUid.Values];

    /// <summary>The transfer syntax with the given identifier, or null when it is not recognised.</summary>
    public static TransferSyntax? Lookup(string? uid) =>
        uid is not null && ByUid.TryGetValue(uid.Trim().TrimEnd('\0'), out TransferSyntax? syntax) ? syntax : null;

    /// <summary>
    /// The transfer syntax with the given identifier, raising when it is not recognised.
    /// </summary>
    public static TransferSyntax Require(string? uid) =>
        Lookup(uid) ?? throw new NotSupportedException($"The transfer syntax '{uid}' is not recognised.");

    /// <summary>Adds or replaces a transfer syntax, for data sets using a private encoding.</summary>
    public static void Register(TransferSyntax syntax) => ByUid[syntax.Uid] = syntax;

    private static ConcurrentDictionary<string, TransferSyntax> Build()
    {
        TransferSyntax[] syntaxes =
        [
            ImplicitVrLittleEndian,
            ExplicitVrLittleEndian,
            DeflatedExplicitVrLittleEndian,
            ExplicitVrBigEndian,
            Encapsulated("1.2.840.10008.1.2.4.50", "JPEG Baseline (Process 1)", lossy: true),
            Encapsulated("1.2.840.10008.1.2.4.51", "JPEG Extended (Process 2 and 4)", lossy: true),
            Encapsulated("1.2.840.10008.1.2.4.57", "JPEG Lossless, Non-Hierarchical (Process 14)"),
            Encapsulated("1.2.840.10008.1.2.4.70", "JPEG Lossless, Selection Value 1 (Process 14)"),
            Encapsulated("1.2.840.10008.1.2.4.80", "JPEG-LS Lossless"),
            Encapsulated("1.2.840.10008.1.2.4.81", "JPEG-LS Lossy (Near-Lossless)", lossy: true),
            Encapsulated("1.2.840.10008.1.2.4.90", "JPEG 2000 Image Compression (Lossless Only)"),
            Encapsulated("1.2.840.10008.1.2.4.91", "JPEG 2000 Image Compression", lossy: true),
            Encapsulated("1.2.840.10008.1.2.4.92", "JPEG 2000 Part 2 Multi-Component (Lossless Only)"),
            Encapsulated("1.2.840.10008.1.2.4.93", "JPEG 2000 Part 2 Multi-Component", lossy: true),
            Encapsulated("1.2.840.10008.1.2.4.100", "MPEG2 Main Profile / Main Level", lossy: true),
            Encapsulated("1.2.840.10008.1.2.4.102", "MPEG-4 AVC/H.264 High Profile / Level 4.1", lossy: true),
            Encapsulated("1.2.840.10008.1.2.4.107", "HEVC/H.265 Main Profile / Level 5.1", lossy: true),
            Encapsulated("1.2.840.10008.1.2.5", "RLE Lossless"),
        ];

        ConcurrentDictionary<string, TransferSyntax> map = new(StringComparer.Ordinal);

        foreach (TransferSyntax syntax in syntaxes)
            map[syntax.Uid] = syntax;

        return map;
    }

    private static TransferSyntax Encapsulated(string uid, string name, bool lossy = false) =>
        new(uid, name, explicitVr: true, ByteOrder.LittleEndian, deflated: false, encapsulated: true, lossy: lossy);
}
