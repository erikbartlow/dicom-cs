using System.Buffers.Binary;
using Dicom.Core;
using Dicom.Encoding;

namespace Dicom.Parser;

/// <summary>
/// Reads the byte level structures of a data set — tags, element headers and item headers —
/// from a stream, following the rules of a given transfer syntax. The stream need not be seekable.
/// </summary>
public sealed class DicomStreamReader
{
    private readonly Stream _stream;
    private readonly byte[] _scratch = new byte[8];

    public DicomStreamReader(Stream stream, TransferSyntax transferSyntax, IDataDictionary dictionary)
    {
        _stream = stream;
        TransferSyntax = transferSyntax;
        Dictionary = dictionary;
    }

    /// <summary>The encoding the stream is being read with.</summary>
    public TransferSyntax TransferSyntax { get; private set; }

    /// <summary>The dictionary supplying value representations for implicitly encoded elements.</summary>
    public IDataDictionary Dictionary { get; }

    /// <summary>The number of bytes consumed so far.</summary>
    public long Position { get; private set; }

    /// <summary>
    /// Switches to another transfer syntax, which happens once the file meta group has been read.
    /// </summary>
    public void SwitchTo(TransferSyntax transferSyntax) => TransferSyntax = transferSyntax;

    /// <summary>
    /// Reads the next data element header, or returns false at the end of the stream.
    /// </summary>
    public bool TryReadHeader(out DataElementHeader header)
    {
        header = default;

        if (!TryFill(_scratch.AsSpan(0, 4)))
            return false;

        Tag tag = ReadTag(_scratch.AsSpan(0, 4));

        if (tag.IsDelimiter)
        {
            header = new DataElementHeader(tag, Vr.UN, new Length(ReadUInt32()));
            return true;
        }

        header = TransferSyntax.ExplicitVr ? ReadExplicitHeader(tag) : ReadImplicitHeader(tag);
        return true;
    }

    /// <summary>Reads an item header, which carries a tag and a length but no representation.</summary>
    public DataElementHeader ReadItemHeader()
    {
        Fill(_scratch.AsSpan(0, 4));
        Tag tag = ReadTag(_scratch.AsSpan(0, 4));

        return new DataElementHeader(tag, Vr.UN, new Length(ReadUInt32()));
    }

    /// <summary>Reads exactly the requested number of bytes.</summary>
    public byte[] ReadBytes(int count)
    {
        if (count == 0)
            return [];

        byte[] buffer = new byte[count];
        Fill(buffer);

        return buffer;
    }

    /// <summary>Discards the requested number of bytes.</summary>
    public void Skip(long count)
    {
        byte[] buffer = new byte[(int)Math.Min(count, 64 * 1024)];
        long remaining = count;

        while (remaining > 0)
        {
            int read = _stream.Read(buffer, 0, (int)Math.Min(remaining, buffer.Length));

            if (read == 0)
                throw new DicomParseException("The stream ended while skipping a value.");

            remaining -= read;
            Position += read;
        }
    }

    private DataElementHeader ReadExplicitHeader(Tag tag)
    {
        Fill(_scratch.AsSpan(0, 2));

        if (!VrExtensions.TryParse(_scratch.AsSpan(0, 2), out Vr vr))
        {
            // Some writers emit an implicitly encoded element inside an explicit data set.
            // The two bytes just read are then the low half of a 32-bit length.
            Fill(_scratch.AsSpan(2, 2));
            uint implicitLength = BinaryPrimitives.ReadUInt32LittleEndian(_scratch.AsSpan(0, 4));

            return new DataElementHeader(tag, Dictionary.VrFor(tag), new Length(implicitLength));
        }

        if (vr.HasLongExplicitHeader())
        {
            Fill(_scratch.AsSpan(0, 2));
            return new DataElementHeader(tag, vr, new Length(ReadUInt32()));
        }

        return new DataElementHeader(tag, vr, new Length(ReadUInt16()));
    }

    private DataElementHeader ReadImplicitHeader(Tag tag)
    {
        uint length = ReadUInt32();
        Vr vr = Dictionary.VrFor(tag);

        // Pixel data carries no representation of its own in an implicit data set.
        if (tag == Tags.PixelData)
            vr = Vr.OW;

        return new DataElementHeader(tag, vr, new Length(length));
    }

    private Tag ReadTag(ReadOnlySpan<byte> bytes)
    {
        bool big = TransferSyntax.ByteOrder == ByteOrder.BigEndian;

        ushort group = big
            ? BinaryPrimitives.ReadUInt16BigEndian(bytes)
            : BinaryPrimitives.ReadUInt16LittleEndian(bytes);

        ushort element = big
            ? BinaryPrimitives.ReadUInt16BigEndian(bytes[2..])
            : BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]);

        return new Tag(group, element);
    }

    private ushort ReadUInt16()
    {
        Fill(_scratch.AsSpan(0, 2));

        return TransferSyntax.ByteOrder == ByteOrder.BigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(_scratch)
            : BinaryPrimitives.ReadUInt16LittleEndian(_scratch);
    }

    private uint ReadUInt32()
    {
        Fill(_scratch.AsSpan(0, 4));

        return TransferSyntax.ByteOrder == ByteOrder.BigEndian
            ? BinaryPrimitives.ReadUInt32BigEndian(_scratch)
            : BinaryPrimitives.ReadUInt32LittleEndian(_scratch);
    }

    private void Fill(Span<byte> destination)
    {
        if (!TryFill(destination))
            throw new DicomParseException("The stream ended in the middle of a data element.");
    }

    private bool TryFill(Span<byte> destination)
    {
        int filled = 0;

        while (filled < destination.Length)
        {
            int read = _stream.Read(destination[filled..]);

            if (read == 0)
                return filled == 0 ? false : throw new DicomParseException("The stream ended unexpectedly.");

            filled += read;
        }

        Position += filled;
        return true;
    }
}
