using System.Buffers.Binary;
using Dicom.Core;
using Dicom.Parser;

namespace Dicom.Object;

/// <summary>
/// Assembles the token stream produced by the parser into an in memory data set,
/// keeping track of the nesting introduced by sequences and encapsulated pixel data.
/// </summary>
public static class DataSetBuilder
{
    /// <summary>Builds a data set from a token stream.</summary>
    public static InMemDicomObject Build(IEnumerable<DataSetToken> tokens, IDataDictionary dictionary)
    {
        InMemDicomObject root = new(dictionary);
        BuildInto(root, tokens, dictionary);

        return root;
    }

    /// <summary>Fills an existing data set from a token stream.</summary>
    public static void BuildInto(
        InMemDicomObject root,
        IEnumerable<DataSetToken> tokens,
        IDataDictionary dictionary)
    {
        InMemDicomObject current = root;
        Stack<Frame> frames = new();

        foreach (DataSetToken token in tokens)
        {
            switch (token.Kind)
            {
                case DataSetTokenKind.ElementHeader:
                    break;

                case DataSetTokenKind.PrimitiveValue:
                    current.Put(new DataElement(token.Tag, token.Header.Vr, token.Value!));
                    break;

                case DataSetTokenKind.SequenceStart:
                case DataSetTokenKind.PixelSequenceStart:
                    frames.Push(new Frame(token.Header, current, token.Kind == DataSetTokenKind.PixelSequenceStart));
                    break;

                case DataSetTokenKind.ItemStart:
                    current = new InMemDicomObject(dictionary);
                    break;

                case DataSetTokenKind.ItemEnd:
                    frames.Peek().Items.Add(current);
                    current = frames.Peek().Owner;
                    break;

                case DataSetTokenKind.ItemValue:
                    frames.Peek().AddFragment(token.Bytes!);
                    break;

                case DataSetTokenKind.SequenceEnd:
                    Frame frame = frames.Pop();
                    frame.Owner.Put(frame.ToElement());
                    current = frame.Owner;
                    break;
            }
        }
    }

    private sealed class Frame
    {
        private readonly bool _isPixelSequence;
        private bool _offsetTableSeen;

        public Frame(DataElementHeader header, InMemDicomObject owner, bool isPixelSequence)
        {
            Header = header;
            Owner = owner;
            _isPixelSequence = isPixelSequence;
        }

        public DataElementHeader Header { get; }

        public InMemDicomObject Owner { get; }

        public List<IDicomDataSet> Items { get; } = [];

        public List<byte[]> Fragments { get; } = [];

        public List<uint> OffsetTable { get; } = [];

        public void AddFragment(byte[] bytes)
        {
            // The first item of encapsulated pixel data is the basic offset table, which may be empty.
            if (_isPixelSequence && !_offsetTableSeen)
            {
                _offsetTableSeen = true;

                for (int offset = 0; offset + 4 <= bytes.Length; offset += 4)
                    OffsetTable.Add(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)));

                return;
            }

            Fragments.Add(bytes);
        }

        public DataElement ToElement() => _isPixelSequence
            ? new DataElement(Header.Tag, Header.Vr, new PixelSequenceDicomValue(Fragments, OffsetTable))
            : new DataElement(Header.Tag, Vr.SQ, new SequenceDicomValue(Items));
    }
}
