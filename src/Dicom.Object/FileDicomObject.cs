using System.IO.Compression;
using Dicom.Core;
using Dicom.Dictionary.Std;
using Dicom.Encoding;
using Dicom.Parser;

namespace Dicom.Object;

/// <summary>
/// A data set together with the file meta information describing how it was stored.
/// </summary>
public sealed class FileDicomObject : InMemDicomObject
{
    public FileDicomObject(FileMetaTable meta)
        : this(meta, StandardDataDictionary.Instance)
    {
    }

    public FileDicomObject(FileMetaTable meta, IDataDictionary dictionary)
        : base(dictionary)
    {
        Meta = meta;
    }

    /// <summary>The file meta information group.</summary>
    public FileMetaTable Meta { get; }

    /// <summary>Writes the object as a part 10 file, in the transfer syntax its meta group names.</summary>
    public void Save(string path)
    {
        using FileStream stream = File.Create(path);
        Save(stream);
    }

    /// <summary>Writes the object as a part 10 file to a stream.</summary>
    public void Save(Stream stream)
    {
        TransferSyntax syntax = Meta.RequireTransferSyntax();

        Meta.Write(stream);

        if (!syntax.Deflated)
        {
            new DataSetWriter(stream, syntax).Write(this);
            return;
        }

        using DeflateStream deflated = new(stream, CompressionMode.Compress, leaveOpen: true);
        new DataSetWriter(deflated, syntax).Write(this);
    }

    /// <summary>
    /// Changes the transfer syntax recorded in the meta group. Only a change between the
    /// uncompressed syntaxes is meaningful, since pixel data is not transcoded here.
    /// </summary>
    public void SetTransferSyntax(TransferSyntax syntax) => Meta.TransferSyntaxUid = syntax.Uid;
}

/// <summary>
/// Reading and writing DICOM part 10 files.
/// </summary>
public static class DicomFile
{
    /// <summary>The identifier this library records as the implementation that wrote a file.</summary>
    public const string ImplementationClassUid = "1.2.826.0.1.3680043.10.1337.1";

    /// <summary>The version name this library records as the implementation that wrote a file.</summary>
    public const string ImplementationVersionName = "DICOM_NET10_1";

    /// <summary>Reads a part 10 file from disk.</summary>
    public static FileDicomObject Open(string path, IDataDictionary? dictionary = null)
    {
        using FileStream stream = File.OpenRead(path);

        return Read(stream, dictionary);
    }

    /// <summary>Reads a part 10 file from a stream.</summary>
    public static FileDicomObject Read(Stream stream, IDataDictionary? dictionary = null)
    {
        IDataDictionary resolved = dictionary ?? StandardDataDictionary.Instance;
        FileMetaTable meta = FileMetaTable.Read(stream, resolved);
        TransferSyntax syntax = meta.RequireTransferSyntax();

        FileDicomObject file = new(meta, resolved);

        if (!syntax.Deflated)
        {
            DataSetBuilder.BuildInto(file, new DataSetReader(stream, syntax, resolved).Read(), resolved);
            return file;
        }

        using DeflateStream inflated = new(stream, CompressionMode.Decompress, leaveOpen: true);
        DataSetBuilder.BuildInto(file, new DataSetReader(inflated, syntax, resolved).Read(), resolved);

        return file;
    }

    /// <summary>
    /// Reads a bare data set, one written without a preamble or file meta group, in the
    /// transfer syntax given.
    /// </summary>
    public static InMemDicomObject ReadDataSet(
        Stream stream,
        TransferSyntax? transferSyntax = null,
        IDataDictionary? dictionary = null)
    {
        IDataDictionary resolved = dictionary ?? StandardDataDictionary.Instance;
        TransferSyntax syntax = transferSyntax ?? TransferSyntaxRegistry.Default;

        return DataSetBuilder.Build(new DataSetReader(stream, syntax, resolved).Read(), resolved);
    }

    /// <summary>
    /// Wraps a data set in file meta information so it can be written as a part 10 file.
    /// The storage class and instance identifiers are taken from the data set when present.
    /// </summary>
    public static FileDicomObject ToFile(InMemDicomObject dataSet, TransferSyntax? transferSyntax = null)
    {
        TransferSyntax syntax = transferSyntax ?? TransferSyntaxRegistry.ExplicitVrLittleEndian;

        FileMetaTable meta = new()
        {
            TransferSyntaxUid = syntax.Uid,
            MediaStorageSopClassUid = dataSet.GetString(Tags.SopClassUid),
            MediaStorageSopInstanceUid = dataSet.GetString(Tags.SopInstanceUid),
            ImplementationClassUid = ImplementationClassUid,
            ImplementationVersionName = ImplementationVersionName,
        };

        meta.Elements.Put(Tags.FileMetaInformationVersion, Vr.OB, PrimitiveValue.FromBytes([0x00, 0x01]));

        FileDicomObject file = new(meta, dataSet.Dictionary);

        foreach (DataElement element in dataSet)
            file.Put(element);

        return file;
    }
}
