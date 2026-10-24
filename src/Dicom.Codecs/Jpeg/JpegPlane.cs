namespace Dicom.Codecs.Jpeg;

/// <summary>
/// The samples of one component of a frame, at that component's own resolution.
/// </summary>
internal sealed class JpegPlane
{
    private readonly byte[] _samples;

    public JpegPlane(byte[] samples, int width, int height, int horizontalSampling, int verticalSampling)
    {
        _samples = samples;
        Width = width;
        Height = height;
        HorizontalSampling = horizontalSampling;
        VerticalSampling = verticalSampling;
    }

    /// <summary>The number of columns this component holds.</summary>
    public int Width { get; }

    /// <summary>The number of rows this component holds.</summary>
    public int Height { get; }

    /// <summary>How many blocks of this component a minimum coded unit holds across.</summary>
    public int HorizontalSampling { get; }

    /// <summary>How many blocks of this component a minimum coded unit holds down.</summary>
    public int VerticalSampling { get; }

    /// <summary>The samples themselves, in row order.</summary>
    public byte[] Samples => _samples;

    /// <summary>
    /// One block of samples, shifted to be centred on zero as the transform expects. Blocks that
    /// run past the edge repeat the last sample, which is what encoders do to avoid an edge.
    /// </summary>
    public void ReadBlock(int left, int top, double[] block)
    {
        for (int y = 0; y < 8; y++)
        {
            int row = Math.Min(top + y, Height - 1) * Width;

            for (int x = 0; x < 8; x++)
                block[(y * 8) + x] = _samples[row + Math.Min(left + x, Width - 1)] - 128.0;
        }
    }
}

/// <summary>
/// The Huffman tables a baseline stream is written with, as codes ready to be emitted.
/// </summary>
internal sealed class JpegEncodeTables
{
    public JpegEncodeTables(bool colour)
    {
        DcLuminance = new JpegHuffmanTable(JpegTables.DcLuminanceCounts, JpegTables.DcLuminanceValues)
            .BuildEncodeTable();
        AcLuminance = new JpegHuffmanTable(JpegTables.AcLuminanceCounts, JpegTables.AcLuminanceValues)
            .BuildEncodeTable();

        DcChrominance = colour
            ? new JpegHuffmanTable(JpegTables.DcChrominanceCounts, JpegTables.DcChrominanceValues)
                .BuildEncodeTable()
            : DcLuminance;
        AcChrominance = colour
            ? new JpegHuffmanTable(JpegTables.AcChrominanceCounts, JpegTables.AcChrominanceValues)
                .BuildEncodeTable()
            : AcLuminance;
    }

    public (int Code, int Length)[] DcLuminance { get; }

    public (int Code, int Length)[] AcLuminance { get; }

    public (int Code, int Length)[] DcChrominance { get; }

    public (int Code, int Length)[] AcChrominance { get; }
}
