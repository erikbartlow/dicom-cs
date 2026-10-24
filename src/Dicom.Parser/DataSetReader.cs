using Dicom.Core;
using Dicom.Encoding;

namespace Dicom.Parser;

/// <summary>
/// Reads a data set as a stream of tokens: plain elements with their decoded values,
/// and the nested structure of sequences and encapsulated pixel data. The caller assembles
/// whatever representation it needs from the tokens.
/// </summary>
public sealed class DataSetReader
{
    private readonly DicomStreamReader _reader;
    private System.Text.Encoding _textEncoding = DicomCharacterSet.Default;

    public DataSetReader(Stream stream, TransferSyntax transferSyntax, IDataDictionary dictionary)
        : this(new DicomStreamReader(stream, transferSyntax, dictionary))
    {
    }

    public DataSetReader(DicomStreamReader reader) => _reader = reader;

    /// <summary>The encoding currently in force, as declared by Specific Character Set.</summary>
    public System.Text.Encoding TextEncoding => _textEncoding;

    /// <summary>The number of bytes consumed so far.</summary>
    public long Position => _reader.Position;

    /// <summary>
    /// Reads tokens until the stream ends, or until the given number of bytes has been consumed.
    /// </summary>
    public IEnumerable<DataSetToken> Read(long? byteLimit = null)
    {
        long? end = byteLimit is null ? null : _reader.Position + byteLimit.Value;

        return ReadElements(end);
    }

    private IEnumerable<DataSetToken> ReadElements(long? end)
    {
        while (end is null || _reader.Position < end)
        {
            if (!_reader.TryReadHeader(out DataElementHeader header))
                yield break;

            // A delimiter closes the enclosing item or sequence; the caller resumes from there.
            if (header.Tag.IsDelimiter)
                yield break;

            if (IsSequence(header))
            {
                yield return DataSetToken.SequenceStart(header);

                foreach (DataSetToken token in ReadItems(header))
                    yield return token;

                yield return DataSetToken.SequenceEnd();
                continue;
            }

            if (header.IsEncapsulatedPixelData)
            {
                yield return DataSetToken.PixelSequenceStart(header);

                foreach (DataSetToken token in ReadFragments())
                    yield return token;

                yield return DataSetToken.SequenceEnd();
                continue;
            }

            yield return DataSetToken.Element(header);
            yield return DataSetToken.Primitive(header, ReadValue(header));
        }
    }

    private IEnumerable<DataSetToken> ReadItems(DataElementHeader sequence)
    {
        long? end = sequence.Length.IsDefined ? _reader.Position + sequence.Length.Value : null;

        while (end is null || _reader.Position < end)
        {
            DataElementHeader item = _reader.ReadItemHeader();

            if (item.Tag == Tag.SequenceDelimitationItem)
                yield break;

            if (item.Tag != Tag.Item)
                throw new DicomParseException($"Expected an item but found {item.Tag} inside {sequence.Tag}.");

            yield return DataSetToken.ItemStart(item);

            long? itemEnd = item.Length.IsDefined ? _reader.Position + item.Length.Value : null;

            foreach (DataSetToken token in ReadElements(itemEnd))
                yield return token;

            yield return DataSetToken.ItemEnd();
        }
    }

    private IEnumerable<DataSetToken> ReadFragments()
    {
        while (true)
        {
            DataElementHeader item = _reader.ReadItemHeader();

            if (item.Tag == Tag.SequenceDelimitationItem)
                yield break;

            if (item.Tag != Tag.Item)
                throw new DicomParseException($"Expected a pixel data fragment but found {item.Tag}.");

            yield return DataSetToken.ItemValue(_reader.ReadBytes(item.Length.Require()));
        }
    }

    private PrimitiveValue ReadValue(DataElementHeader header)
    {
        if (header.Length.IsUndefined)
            throw new DicomParseException($"{header.Tag} has an undefined length but is not a sequence.");

        byte[] bytes = _reader.ReadBytes(header.Length.Require());

        PrimitiveValue value = ValueDecoder.Decode(
            header.Vr,
            bytes,
            _reader.TransferSyntax.ByteOrder,
            _textEncoding);

        if (header.Tag == Tags.SpecificCharacterSet)
            _textEncoding = DicomCharacterSet.Resolve(value.ToMultiStringValue());

        return value;
    }

    private static bool IsSequence(DataElementHeader header) =>
        header.Vr == Vr.SQ || (header.Vr == Vr.UN && header.Length.IsUndefined && !header.IsEncapsulatedPixelData);
}
