namespace Dicom.Codecs.Jpeg;

/// <summary>
/// Reads a sequential, Huffman coded JPEG stream of eight or twelve bit samples, including
/// streams whose chrominance is subsampled. Baseline (SOF0) and extended sequential (SOF1)
/// streams differ only in the sample precision they allow, so both are read here. Progressive
/// and arithmetic coded streams are refused.
/// </summary>
internal static class JpegBaselineDecoder
{
    /// <summary>The samples of the stream, interleaved by pixel.</summary>
    public static JpegFrameSamples Decode(byte[] jpeg)
    {
        if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
            throw new CodecException("The fragment does not begin with a JPEG start of image marker.");

        int[][] quantisation = new int[4][];
        JpegHuffmanTable?[] dcTables = new JpegHuffmanTable?[4];
        JpegHuffmanTable?[] acTables = new JpegHuffmanTable?[4];
        JpegScanComponent[] components = [];
        int position = 2;
        int width = 0;
        int height = 0;
        int restartInterval = 0;
        int adobeTransform = -1;
        int precision = 8;

        while (position + 1 < jpeg.Length)
        {
            if (jpeg[position] != 0xFF)
            {
                position++;
                continue;
            }

            byte marker = jpeg[position + 1];
            position += 2;

            switch (marker)
            {
                case 0xD8:
                case 0xFF:
                case 0x01:
                    continue;

                case 0xC0:
                case 0xC1:
                {
                    int length = Read16(jpeg, position);

                    precision = jpeg[position + 2];

                    if (precision is not (8 or 12))
                        throw new CodecException(
                            $"Sequential JPEG of {precision} bit samples is not supported.");

                    height = Read16(jpeg, position + 3);
                    width = Read16(jpeg, position + 5);
                    components = new JpegScanComponent[jpeg[position + 7]];

                    for (int i = 0; i < components.Length; i++)
                    {
                        int offset = position + 8 + (i * 3);

                        components[i] = new JpegScanComponent(
                            jpeg[offset],
                            jpeg[offset + 1] >> 4,
                            jpeg[offset + 1] & 0x0F,
                            jpeg[offset + 2]);
                    }

                    position += length;
                    continue;
                }

                case 0xC2:
                case 0xC9:
                case 0xCA:
                    throw new CodecException(
                        "Progressive or arithmetic coded JPEG is not supported.");

                case 0xC4:
                {
                    int length = Read16(jpeg, position);
                    int end = position + length;
                    int cursor = position + 2;

                    while (cursor < end)
                    {
                        int identifier = jpeg[cursor++];
                        int total = 0;

                        for (int i = 0; i < 16; i++)
                            total += jpeg[cursor + i];

                        JpegHuffmanTable table = new(jpeg.AsSpan(cursor, 16), jpeg[(cursor + 16)..(cursor + 16 + total)]);

                        if ((identifier & 0xF0) == 0)
                            dcTables[identifier & 0x0F] = table;
                        else
                            acTables[identifier & 0x0F] = table;

                        cursor += 16 + total;
                    }

                    position = end;
                    continue;
                }

                case 0xDB:
                {
                    int length = Read16(jpeg, position);
                    int end = position + length;
                    int cursor = position + 2;

                    while (cursor < end)
                    {
                        int identifier = jpeg[cursor++];
                        int[] table = new int[64];
                        bool wide = (identifier & 0xF0) != 0;

                        for (int i = 0; i < 64; i++)
                        {
                            table[JpegTables.ZigZag[i]] = wide
                                ? Read16(jpeg, cursor + (i * 2))
                                : jpeg[cursor + i];
                        }

                        quantisation[identifier & 0x0F] = table;
                        cursor += wide ? 128 : 64;
                    }

                    position = end;
                    continue;
                }

                case 0xDD:
                {
                    restartInterval = Read16(jpeg, position + 2);
                    position += Read16(jpeg, position);
                    continue;
                }

                case 0xEE:
                {
                    int length = Read16(jpeg, position);

                    if (length >= 14)
                        adobeTransform = jpeg[position + length - 1];

                    position += length;
                    continue;
                }

                case 0xDA:
                {
                    int length = Read16(jpeg, position);
                    int scanComponents = jpeg[position + 2];

                    for (int i = 0; i < scanComponents; i++)
                    {
                        int id = jpeg[position + 3 + (i * 2)];
                        int selector = jpeg[position + 4 + (i * 2)];
                        JpegScanComponent component = Find(components, id);

                        component.DcTable = dcTables[selector >> 4];
                        component.AcTable = acTables[selector & 0x0F];
                    }

                    JpegFrameSamples samples = JpegBaselineScan.Decode(
                        jpeg,
                        position + length,
                        components,
                        quantisation,
                        width,
                        height,
                        restartInterval,
                        precision);

                    samples.IsYCbCr = components.Length == 3 && adobeTransform != 0;

                    return samples;
                }

                case 0xD9:
                    throw new CodecException("The compressed stream ends before its scan.");

                default:
                {
                    if (marker is >= 0xD0 and <= 0xD7)
                        continue;

                    position += Read16(jpeg, position);
                    continue;
                }
            }
        }

        throw new CodecException("The compressed stream has no scan.");
    }

    private static JpegScanComponent Find(JpegScanComponent[] components, int id)
    {
        foreach (JpegScanComponent component in components)
        {
            if (component.Id == id)
                return component;
        }

        throw new CodecException("The scan refers to a component the frame header does not define.");
    }

    private static int Read16(byte[] data, int position) => (data[position] << 8) | data[position + 1];
}

/// <summary>
/// One component of a sequential frame, with the tables its blocks are coded with.
/// </summary>
internal sealed class JpegScanComponent
{
    public JpegScanComponent(int id, int horizontalSampling, int verticalSampling, int quantisationTable)
    {
        Id = id;
        HorizontalSampling = Math.Max(horizontalSampling, 1);
        VerticalSampling = Math.Max(verticalSampling, 1);
        QuantisationTable = quantisationTable;
    }

    public int Id { get; }

    public int HorizontalSampling { get; }

    public int VerticalSampling { get; }

    public int QuantisationTable { get; }

    public JpegHuffmanTable? DcTable { get; set; }

    public JpegHuffmanTable? AcTable { get; set; }

    public ushort[] Plane { get; set; } = [];

    public int PlaneWidth { get; set; }

    public int PlaneHeight { get; set; }
}
