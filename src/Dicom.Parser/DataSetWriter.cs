using System.Buffers.Binary;
using Dicom.Core;
using Dicom.Encoding;

namespace Dicom.Parser;

/// <summary>
/// Writes a data set to a stream in a given transfer syntax. Sequences are written with
/// explicit lengths, which is achieved by encoding their items into memory first;
/// encapsulated pixel data keeps the undefined length and delimiter form it requires.
/// </summary>
public sealed class DataSetWriter
{
    private readonly Stream _stream;
    private readonly byte[] _scratch = new byte[12];
    private System.Text.Encoding _textEncoding;

    public DataSetWriter(Stream stream, TransferSyntax transferSyntax, System.Text.Encoding? textEncoding = null)
    {
        _stream = stream;
        TransferSyntax = transferSyntax;
        _textEncoding = textEncoding ?? DicomCharacterSet.Default;
    }

    /// <summary>The encoding the data set is being written in.</summary>
    public TransferSyntax TransferSyntax { get; }

    /// <summary>Writes every element of a data set, in ascending tag order.</summary>
    public void Write(IDicomDataSet dataSet)
    {
        foreach (DataElement element in dataSet.OrderBy(static e => e.Tag))
            WriteElement(element);
    }

    /// <summary>Writes one element, including any nested items.</summary>
    public void WriteElement(DataElement element)
    {
        switch (element.Value)
        {
            case SequenceDicomValue sequence:
                WriteSequence(element, sequence);
                return;

            case PixelSequenceDicomValue pixels:
                WritePixelSequence(element, pixels);
                return;

            default:
                WritePrimitive(element);
                return;
        }
    }

    private void WritePrimitive(DataElement element)
    {
        byte[] bytes = ValueEncoder.Encode(
            element.Vr,
            element.Value.RequirePrimitive(),
            TransferSyntax.ByteOrder,
            _textEncoding);

        WriteHeader(element.Tag, element.Vr, (uint)bytes.Length);
        _stream.Write(bytes);

        if (element.Tag == Tags.SpecificCharacterSet)
            _textEncoding = DicomCharacterSet.Resolve(element.Value.RequirePrimitive().ToMultiStringValue());
    }

    private void WriteSequence(DataElement element, SequenceDicomValue sequence)
    {
        using MemoryStream buffer = new();
        DataSetWriter inner = new(buffer, TransferSyntax, _textEncoding);

        foreach (IDicomDataSet item in sequence.Items)
        {
            using MemoryStream itemBuffer = new();
            DataSetWriter itemWriter = new(itemBuffer, TransferSyntax, _textEncoding);
            itemWriter.Write(item);

            inner.WriteItemHeader(Tag.Item, (uint)itemBuffer.Length);
            itemBuffer.Position = 0;
            itemBuffer.CopyTo(buffer);
        }

        WriteHeader(element.Tag, Vr.SQ, (uint)buffer.Length);
        buffer.Position = 0;
        buffer.CopyTo(_stream);
    }

    private void WritePixelSequence(DataElement element, PixelSequenceDicomValue pixels)
    {
        WriteHeader(element.Tag, element.Vr == Vr.OW ? Vr.OW : Vr.OB, Length.UndefinedValue);

        byte[] offsetTable = new byte[pixels.OffsetTable.Count * 4];

        for (int i = 0; i < pixels.OffsetTable.Count; i++)
            WriteUInt32(offsetTable.AsSpan(i * 4), pixels.OffsetTable[i]);

        WriteItemHeader(Tag.Item, (uint)offsetTable.Length);
        _stream.Write(offsetTable);

        foreach (byte[] fragment in pixels.Fragments)
        {
            WriteItemHeader(Tag.Item, (uint)fragment.Length);
            _stream.Write(fragment);
        }

        WriteItemHeader(Tag.SequenceDelimitationItem, 0);
    }

    private void WriteItemHeader(Tag tag, uint length)
    {
        WriteTag(_scratch.AsSpan(0, 4), tag);
        WriteUInt32(_scratch.AsSpan(4, 4), length);
        _stream.Write(_scratch, 0, 8);
    }

    private void WriteHeader(Tag tag, Vr vr, uint length)
    {
        WriteTag(_scratch.AsSpan(0, 4), tag);

        if (!TransferSyntax.ExplicitVr)
        {
            WriteUInt32(_scratch.AsSpan(4, 4), length);
            _stream.Write(_scratch, 0, 8);
            return;
        }

        vr.WriteCode(_scratch.AsSpan(4, 2));

        if (vr.HasLongExplicitHeader())
        {
            _scratch[6] = 0;
            _scratch[7] = 0;
            WriteUInt32(_scratch.AsSpan(8, 4), length);
            _stream.Write(_scratch, 0, 12);
            return;
        }

        WriteUInt16(_scratch.AsSpan(6, 2), checked((ushort)length));
        _stream.Write(_scratch, 0, 8);
    }

    private void WriteTag(Span<byte> destination, Tag tag)
    {
        WriteUInt16(destination, tag.Group);
        WriteUInt16(destination[2..], tag.Element);
    }

    private void WriteUInt16(Span<byte> destination, ushort value)
    {
        if (TransferSyntax.ByteOrder == ByteOrder.BigEndian)
            BinaryPrimitives.WriteUInt16BigEndian(destination, value);
        else
            BinaryPrimitives.WriteUInt16LittleEndian(destination, value);
    }

    private void WriteUInt32(Span<byte> destination, uint value)
    {
        if (TransferSyntax.ByteOrder == ByteOrder.BigEndian)
            BinaryPrimitives.WriteUInt32BigEndian(destination, value);
        else
            BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
    }
}
