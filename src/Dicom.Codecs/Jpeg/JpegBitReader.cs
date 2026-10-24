namespace Dicom.Codecs.Jpeg;

/// <summary>
/// Reads the entropy coded segment of a JPEG stream one bit at a time, undoing the byte
/// stuffing the format uses so that a marker can always be told from data.
/// </summary>
internal sealed class JpegBitReader
{
    private readonly byte[] _data;
    private int _position;
    private int _bits;
    private int _count;

    public JpegBitReader(byte[] data, int position)
    {
        _data = data;
        _position = position;
    }

    /// <summary>The offset of the next byte that has not been consumed.</summary>
    public int Position => _position;

    /// <summary>Whether a marker was reached, so that no more entropy coded data follows.</summary>
    public bool AtMarker { get; private set; }

    /// <summary>The next bit, or zero once a marker has been reached.</summary>
    public int ReadBit()
    {
        if (_count == 0 && !Fill())
            return 0;

        _count--;

        return (_bits >> _count) & 1;
    }

    /// <summary>The next bits, most significant first.</summary>
    public int ReadBits(int count)
    {
        int value = 0;

        for (int i = 0; i < count; i++)
            value = (value << 1) | ReadBit();

        return value;
    }

    /// <summary>
    /// Discards any part-read byte and steps over a restart marker, which is how the decoder
    /// resynchronises at the end of a restart interval.
    /// </summary>
    public void Restart()
    {
        _count = 0;
        AtMarker = false;

        while (_position + 1 < _data.Length)
        {
            if (_data[_position] != 0xFF)
            {
                _position++;
                continue;
            }

            byte marker = _data[_position + 1];

            if (marker is >= 0xD0 and <= 0xD7)
            {
                _position += 2;
                return;
            }

            if (marker == 0xFF)
            {
                _position++;
                continue;
            }

            return;
        }

        _position = _data.Length;
    }

    private bool Fill()
    {
        if (AtMarker || _position >= _data.Length)
        {
            AtMarker = true;
            return false;
        }

        byte value = _data[_position++];

        if (value == 0xFF)
        {
            while (_position < _data.Length && _data[_position] == 0xFF)
                _position++;

            if (_position >= _data.Length)
            {
                AtMarker = true;
                return false;
            }

            byte next = _data[_position];

            if (next != 0x00)
            {
                _position--;
                AtMarker = true;
                return false;
            }

            _position++;
        }

        _bits = value;
        _count = 8;

        return true;
    }
}
