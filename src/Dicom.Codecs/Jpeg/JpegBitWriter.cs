namespace Dicom.Codecs.Jpeg;

/// <summary>
/// Writes the entropy coded segment of a JPEG stream one bit at a time, stuffing a zero byte
/// after every 0xFF so that no marker is produced by accident.
/// </summary>
internal sealed class JpegBitWriter
{
    private readonly List<byte> _output;
    private int _bits;
    private int _count;

    public JpegBitWriter(List<byte> output) => _output = output;

    /// <summary>Writes the low bits of a value, most significant first.</summary>
    public void Write(int value, int length)
    {
        for (int i = length - 1; i >= 0; i--)
            WriteBit((value >> i) & 1);
    }

    /// <summary>Writes a Huffman code.</summary>
    public void Write((int Code, int Length) code)
    {
        if (code.Length == 0)
            throw new CodecException("A symbol was produced that the Huffman table does not define.");

        Write(code.Code, code.Length);
    }

    /// <summary>Pads the last byte with one bits, as the end of an entropy coded segment requires.</summary>
    public void Flush()
    {
        while (_count != 0)
            WriteBit(1);
    }

    private void WriteBit(int bit)
    {
        _bits = (_bits << 1) | bit;
        _count++;

        if (_count != 8)
            return;

        byte value = (byte)_bits;
        _output.Add(value);

        if (value == 0xFF)
            _output.Add(0x00);

        _bits = 0;
        _count = 0;
    }
}
