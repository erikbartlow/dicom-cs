using Dicom.Core;
using Dicom.Dictionary.Std;
using Dicom.Encoding;
using Dicom.Object;
using Dicom.Parser;
using Dicom.PixelData;

namespace DicomObjects;

/// <summary>
/// Reading and writing of a data set.
/// </summary>
public partial class DicomDataSet
{
    /// <summary>Reads a data set from a file.</summary>
    public void Read(string fileName, ReadBehaviours? behaviours = null)
    {
        using FileStream stream = File.OpenRead(fileName);
        Read(stream, behaviours);
    }

    /// <summary>Reads a data set from a byte array.</summary>
    public void Read(byte[] contents, ReadBehaviours? behaviours = null)
    {
        using MemoryStream stream = new(contents, writable: false);
        Read(stream, behaviours);
    }

    /// <summary>Reads a data set from a stream.</summary>
    public void Read(Stream stream, ReadBehaviours? behaviours = null)
    {
        InMemDicomObject data;
        string originalTransferSyntax;

        if (HasPreamble(stream))
        {
            FileDicomObject file = DicomFile.Read(stream, DicomGlobal.Dictionary);
            data = file;
            originalTransferSyntax = file.Meta.TransferSyntaxUid ?? UIDs.TransferSyntaxes.ImplicitVRLittleEndian;
        }
        else
        {
            TransferSyntax syntax = TransferSyntaxRegistry.Lookup(OverrideTS) ?? TransferSyntaxRegistry.Default;
            data = DicomFile.ReadDataSet(stream, syntax, DicomGlobal.Dictionary);
            originalTransferSyntax = syntax.Uid;
        }

        if (behaviours?.DontReadPixels == true)
            data.Remove(Tags.PixelData);

        Replace(data, originalTransferSyntax);
    }

    /// <summary>Writes the data set to a byte array.</summary>
    public void Write(out byte[] contents, string? transferSyntax = null, int quality = 100)
    {
        using MemoryStream stream = new();
        Write(stream, transferSyntax, quality);
        contents = stream.ToArray();
    }

    /// <summary>Writes the data set to a file.</summary>
    public void Write(string fileName, string? transferSyntax = null, int quality = 100)
    {
        using FileStream stream = File.Create(fileName);
        Write(stream, transferSyntax, quality);
    }

    /// <summary>
    /// Writes the data set to a stream as a part 10 file. Pixel data stored in another way than
    /// the transfer syntax asked for is compressed or decompressed first, so that what is written
    /// always matches what the file says it is. The data set itself is left as it was.
    /// </summary>
    public void Write(Stream stream, string? transferSyntax = null, int quality = 100)
    {
        TransferSyntax syntax = ResolveWriteSyntax(transferSyntax);
        InMemDicomObject data = Transcoded(syntax, quality);

        FileDicomObject file = DicomFile.ToFile(data, syntax);
        file.Save(stream);
    }

    /// <summary>
    /// The data set with its pixel data in the form the transfer syntax requires, which is this
    /// data set itself when nothing has to change.
    /// </summary>
    private InMemDicomObject Transcoded(TransferSyntax syntax, int quality)
    {
        InMemDicomObject copy = _data.Clone();

        bool changed = PixelDataTranscoder.ToTransferSyntax(
            copy,
            OverrideTS ?? OriginalTS,
            syntax.Uid,
            syntax.Encapsulated,
            quality);

        return changed ? copy : _data;
    }

    /// <summary>Whether the bytes are a part 10 file, judged by the preamble.</summary>
    public static bool IsDicom(byte[] contents)
    {
        if (contents.Length < 132)
            return false;

        return contents.AsSpan(128, 4).SequenceEqual(FileMetaTable.Magic);
    }

    private TransferSyntax ResolveWriteSyntax(string? transferSyntax) =>
        TransferSyntaxRegistry.Lookup(transferSyntax)
        ?? TransferSyntaxRegistry.Lookup(OverrideTS)
        ?? TransferSyntaxRegistry.Lookup(OriginalTS)
        ?? TransferSyntaxRegistry.ExplicitVrLittleEndian;

    internal static bool HasPreamble(Stream stream)
    {
        if (!stream.CanSeek)
            return true;

        long start = stream.Position;

        try
        {
            Span<byte> prologue = stackalloc byte[132];
            int read = stream.Read(prologue);

            return read == 132 && prologue[128..].SequenceEqual(FileMetaTable.Magic);
        }
        finally
        {
            stream.Position = start;
        }
    }
}

/// <summary>
/// Options that change how a data set is read.
/// </summary>
public sealed class ReadBehaviours
{
    /// <summary>Whether the basic offset table of encapsulated pixel data is disregarded.</summary>
    public bool IgnoreBOT { get; set; }

    /// <summary>Whether pixel data is left out of the data set that is read.</summary>
    public bool DontReadPixels { get; set; }

    /// <summary>
    /// Whether a file is closed as soon as it has been read. A file is always read into memory
    /// and closed here, so the setting is recorded and otherwise has no effect.
    /// </summary>
    public bool CloseFilesAfterReading { get; set; } = true;
}
