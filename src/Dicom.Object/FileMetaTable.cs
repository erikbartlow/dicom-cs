using Dicom.Core;
using Dicom.Dictionary.Std;
using Dicom.Encoding;
using Dicom.Parser;

namespace Dicom.Object;

/// <summary>
/// The file meta information group, which states how the data set that follows is encoded.
/// It is always written itself in explicit VR little endian.
/// </summary>
public sealed class FileMetaTable
{
    /// <summary>The four bytes that follow the preamble of a part 10 file.</summary>
    public static ReadOnlySpan<byte> Magic => "DICM"u8;

    private static readonly TransferSyntax MetaSyntax = TransferSyntaxRegistry.ExplicitVrLittleEndian;

    public FileMetaTable()
        : this(new InMemDicomObject(StandardDataDictionary.Instance))
    {
    }

    public FileMetaTable(InMemDicomObject elements) => Elements = elements;

    /// <summary>The attributes of the group, which may include ones this class does not name.</summary>
    public InMemDicomObject Elements { get; }

    /// <summary>The storage class of the stored instance.</summary>
    public string? MediaStorageSopClassUid
    {
        get => Elements.GetString(Tags.MediaStorageSopClassUid);
        set => Elements.Put(Tags.MediaStorageSopClassUid, Vr.UI, PrimitiveValue.FromString(value));
    }

    /// <summary>The identifier of the stored instance.</summary>
    public string? MediaStorageSopInstanceUid
    {
        get => Elements.GetString(Tags.MediaStorageSopInstanceUid);
        set => Elements.Put(Tags.MediaStorageSopInstanceUid, Vr.UI, PrimitiveValue.FromString(value));
    }

    /// <summary>The transfer syntax the data set following this group is encoded in.</summary>
    public string? TransferSyntaxUid
    {
        get => Elements.GetString(Tags.TransferSyntaxUid);
        set => Elements.Put(Tags.TransferSyntaxUid, Vr.UI, PrimitiveValue.FromString(value));
    }

    /// <summary>The implementation that wrote the file.</summary>
    public string? ImplementationClassUid
    {
        get => Elements.GetString(Tags.ImplementationClassUid);
        set => Elements.Put(Tags.ImplementationClassUid, Vr.UI, PrimitiveValue.FromString(value));
    }

    /// <summary>The version name of the implementation that wrote the file.</summary>
    public string? ImplementationVersionName
    {
        get => Elements.GetString(Tags.ImplementationVersionName);
        set => Elements.Put(Tags.ImplementationVersionName, Vr.SH, PrimitiveValue.FromString(value));
    }

    /// <summary>The transfer syntax named by this group, raising when it is unrecognised.</summary>
    public TransferSyntax RequireTransferSyntax() => TransferSyntaxRegistry.Require(TransferSyntaxUid);

    /// <summary>
    /// Reads the group from a stream positioned at the start of a part 10 file, consuming the
    /// preamble and the <c>DICM</c> marker.
    /// </summary>
    public static FileMetaTable Read(Stream stream, IDataDictionary dictionary)
    {
        Span<byte> prologue = stackalloc byte[132];
        stream.ReadExactly(prologue);

        if (!prologue[128..].SequenceEqual(Magic))
            throw new DicomParseException("The stream does not start with a DICOM file preamble.");

        return ReadGroup(stream, dictionary);
    }

    /// <summary>Reads the group from a stream positioned just after the <c>DICM</c> marker.</summary>
    public static FileMetaTable ReadGroup(Stream stream, IDataDictionary dictionary)
    {
        DicomStreamReader reader = new(stream, MetaSyntax, dictionary);

        if (!reader.TryReadHeader(out DataElementHeader header) || header.Tag != Tags.FileMetaInformationGroupLength)
            throw new DicomParseException("The file meta group does not start with its group length.");

        PrimitiveValue groupLength = ValueDecoder.Decode(header.Vr, reader.ReadBytes(header.Length.Require()));
        byte[] body = reader.ReadBytes(checked((int)groupLength.ToUInt32()));

        using MemoryStream buffer = new(body, writable: false);
        DataSetReader elements = new(buffer, MetaSyntax, dictionary);

        return new FileMetaTable(DataSetBuilder.Build(elements.Read(), dictionary));
    }

    /// <summary>
    /// Writes the preamble, the <c>DICM</c> marker and the group, recalculating the group length.
    /// </summary>
    public void Write(Stream stream)
    {
        stream.Write(new byte[128]);
        stream.Write(Magic);
        WriteGroup(stream);
    }

    /// <summary>Writes the group without the preamble, recalculating the group length.</summary>
    public void WriteGroup(Stream stream)
    {
        using MemoryStream buffer = new();
        DataSetWriter writer = new(buffer, MetaSyntax);

        foreach (DataElement element in Elements.Where(static e => e.Tag != Tags.FileMetaInformationGroupLength))
            writer.WriteElement(element);

        DataSetWriter outer = new(stream, MetaSyntax);
        outer.WriteElement(new DataElement(
            Tags.FileMetaInformationGroupLength,
            Vr.UL,
            PrimitiveValue.FromUInt32([(uint)buffer.Length])));

        buffer.Position = 0;
        buffer.CopyTo(stream);
    }

    public override string ToString() => $"file meta information for {TransferSyntaxUid}";
}
