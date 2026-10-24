namespace Dicom.Codecs.Jpeg;

/// <summary>
/// A JPEG Huffman table, built from the counts of codes of each length and the values those
/// codes stand for, as a define Huffman table segment carries them.
/// </summary>
internal sealed class JpegHuffmanTable
{
    private readonly int[] _minCode = new int[17];
    private readonly int[] _maxCode = new int[17];
    private readonly int[] _valuePointer = new int[17];
    private readonly byte[] _values;

    public JpegHuffmanTable(ReadOnlySpan<byte> countsOfLength, byte[] values)
    {
        _values = values;
        Counts = new byte[17];

        for (int length = 1; length <= 16; length++)
            Counts[length] = countsOfLength[length - 1];

        int code = 0;
        int index = 0;

        for (int length = 1; length <= 16; length++)
        {
            int count = Counts[length];

            if (count == 0)
            {
                _maxCode[length] = -1;
                code <<= 1;
                continue;
            }

            _valuePointer[length] = index;
            _minCode[length] = code;

            index += count;
            code += count;

            _maxCode[length] = code - 1;
            code <<= 1;
        }
    }

    /// <summary>The number of codes of each length, indexed from one to sixteen.</summary>
    public byte[] Counts { get; }

    /// <summary>The values the codes stand for, in order of increasing code length.</summary>
    public byte[] Values => _values;

    /// <summary>The value the next code in the stream stands for.</summary>
    public int Decode(JpegBitReader reader)
    {
        int code = reader.ReadBit();

        for (int length = 1; length <= 16; length++)
        {
            if (_maxCode[length] >= 0 && code <= _maxCode[length])
                return _values[_valuePointer[length] + code - _minCode[length]];

            code = (code << 1) | reader.ReadBit();
        }

        throw new CodecException("The compressed data contains a Huffman code the table does not define.");
    }

    /// <summary>The code and its length for each value, for writing a stream with this table.</summary>
    public (int Code, int Length)[] BuildEncodeTable()
    {
        (int Code, int Length)[] table = new (int, int)[256];
        int code = 0;
        int index = 0;

        for (int length = 1; length <= 16; length++)
        {
            for (int i = 0; i < Counts[length]; i++)
            {
                table[_values[index]] = (code, length);
                index++;
                code++;
            }

            code <<= 1;
        }

        return table;
    }

    /// <summary>
    /// A table whose codes are as short as the given symbol frequencies allow, limited to
    /// sixteen bits as the format requires.
    /// </summary>
    public static JpegHuffmanTable FromFrequencies(int[] frequencies, int symbolCount)
    {
        int[] lengths = CodeLengths(frequencies, symbolCount);
        byte[] counts = new byte[16];
        List<byte> values = [];

        for (int length = 1; length <= 16; length++)
        {
            for (int symbol = 0; symbol < symbolCount; symbol++)
            {
                if (lengths[symbol] != length)
                    continue;

                counts[length - 1]++;
                values.Add((byte)symbol);
            }
        }

        return new JpegHuffmanTable(counts, [.. values]);
    }

    private static int[] CodeLengths(int[] frequencies, int symbolCount)
    {
        int[] lengths = new int[symbolCount];
        List<(int Weight, List<int> Symbols)> nodes = [];

        for (int symbol = 0; symbol < symbolCount; symbol++)
        {
            if (frequencies[symbol] > 0)
                nodes.Add((frequencies[symbol], [symbol]));
        }

        if (nodes.Count == 0)
            nodes.Add((1, [0]));

        if (nodes.Count == 1)
        {
            lengths[nodes[0].Symbols[0]] = 1;
            return lengths;
        }

        while (nodes.Count > 1)
        {
            nodes.Sort(static (a, b) => a.Weight.CompareTo(b.Weight));

            (int Weight, List<int> Symbols) first = nodes[0];
            (int Weight, List<int> Symbols) second = nodes[1];

            nodes.RemoveRange(0, 2);

            foreach (int symbol in first.Symbols)
                lengths[symbol]++;

            foreach (int symbol in second.Symbols)
                lengths[symbol]++;

            nodes.Add((first.Weight + second.Weight, [.. first.Symbols, .. second.Symbols]));
        }

        return Limit(lengths, symbolCount);
    }

    /// <summary>
    /// Shortens any code longer than sixteen bits, which the format does not allow, by moving
    /// the deepest leaves up the tree as Annex K of the specification describes.
    /// </summary>
    private static int[] Limit(int[] lengths, int symbolCount)
    {
        int longest = lengths.Max();

        if (longest <= 16)
            return lengths;

        int[] counts = new int[longest + 2];

        foreach (int length in lengths)
        {
            if (length > 0)
                counts[length]++;
        }

        for (int length = longest; length > 16; length--)
        {
            while (counts[length] > 0)
            {
                int donor = length - 2;

                while (counts[donor] == 0)
                    donor--;

                counts[length] -= 2;
                counts[length - 1]++;
                counts[donor + 1] += 2;
                counts[donor]--;
            }
        }

        int[] ordered = [.. Enumerable.Range(0, symbolCount)
            .Where(symbol => lengths[symbol] > 0)
            .OrderBy(symbol => lengths[symbol])
            .ThenBy(static symbol => symbol)];

        int[] limited = new int[symbolCount];
        int index = 0;

        for (int length = 1; length <= 16; length++)
        {
            for (int i = 0; i < counts[length]; i++)
                limited[ordered[index++]] = length;
        }

        return limited;
    }
}
