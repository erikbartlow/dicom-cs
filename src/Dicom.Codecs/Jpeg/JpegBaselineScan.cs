namespace Dicom.Codecs.Jpeg;

/// <summary>
/// The entropy coded segment of a baseline scan: blocks are decoded minimum coded unit by
/// minimum coded unit into one plane per component, then the planes are sampled back up to
/// the size of the frame.
/// </summary>
internal static class JpegBaselineScan
{
    public static JpegFrameSamples Decode(
        byte[] jpeg,
        int start,
        JpegScanComponent[] components,
        int[][] quantisation,
        int width,
        int height,
        int restartInterval)
    {
        if (width <= 0 || height <= 0 || components.Length == 0)
            throw new CodecException("The compressed frame has no dimensions.");

        int horizontalMax = components.Max(static c => c.HorizontalSampling);
        int verticalMax = components.Max(static c => c.VerticalSampling);
        int mcusAcross = (width + (8 * horizontalMax) - 1) / (8 * horizontalMax);
        int mcusDown = (height + (8 * verticalMax) - 1) / (8 * verticalMax);

        foreach (JpegScanComponent component in components)
        {
            component.PlaneWidth = mcusAcross * component.HorizontalSampling * 8;
            component.PlaneHeight = mcusDown * component.VerticalSampling * 8;
            component.Plane = new byte[component.PlaneWidth * component.PlaneHeight];
        }

        JpegBitReader reader = new(jpeg, start);
        int[] previousDc = new int[components.Length];
        int[] coefficients = new int[64];
        double[] block = new double[64];
        double[] scratch = new double[64];
        int sinceRestart = 0;

        for (int mcuY = 0; mcuY < mcusDown; mcuY++)
        {
            for (int mcuX = 0; mcuX < mcusAcross; mcuX++)
            {
                for (int index = 0; index < components.Length; index++)
                {
                    JpegScanComponent component = components[index];
                    int[] table = quantisation[component.QuantisationTable]
                        ?? throw new CodecException("The scan refers to a quantisation table that was not defined.");

                    for (int blockY = 0; blockY < component.VerticalSampling; blockY++)
                    {
                        for (int blockX = 0; blockX < component.HorizontalSampling; blockX++)
                        {
                            previousDc[index] = ReadBlock(reader, component, coefficients, previousDc[index]);
                            Reconstruct(coefficients, table, block, scratch);

                            Store(
                                component,
                                ((mcuX * component.HorizontalSampling) + blockX) * 8,
                                ((mcuY * component.VerticalSampling) + blockY) * 8,
                                block);
                        }
                    }
                }

                if (restartInterval <= 0 || ++sinceRestart != restartInterval)
                    continue;

                reader.Restart();
                Array.Clear(previousDc);
                sinceRestart = 0;
            }
        }

        return Interleave(components, width, height, horizontalMax, verticalMax);
    }

    private static int ReadBlock(
        JpegBitReader reader,
        JpegScanComponent component,
        int[] coefficients,
        int previousDc)
    {
        JpegHuffmanTable dc = component.DcTable
            ?? throw new CodecException("The scan refers to a Huffman table that was not defined.");
        JpegHuffmanTable ac = component.AcTable
            ?? throw new CodecException("The scan refers to a Huffman table that was not defined.");

        Array.Clear(coefficients);

        int category = dc.Decode(reader);
        int difference = category == 0 ? 0 : Extend(reader.ReadBits(category), category);

        coefficients[0] = previousDc + difference;

        for (int i = 1; i < 64;)
        {
            int symbol = ac.Decode(reader);
            int run = symbol >> 4;
            int size = symbol & 0x0F;

            if (size == 0)
            {
                if (run != 15)
                    break;

                i += 16;
                continue;
            }

            i += run;

            if (i > 63)
                break;

            coefficients[JpegTables.ZigZag[i]] = Extend(reader.ReadBits(size), size);
            i++;
        }

        return coefficients[0];
    }

    private static void Reconstruct(int[] coefficients, int[] table, double[] block, double[] scratch)
    {
        for (int i = 0; i < 64; i++)
            block[i] = coefficients[i] * table[i];

        JpegTransform.Inverse(block, scratch);
    }

    private static void Store(JpegScanComponent component, int left, int top, double[] block)
    {
        for (int y = 0; y < 8; y++)
        {
            int row = top + y;

            if (row >= component.PlaneHeight)
                break;

            for (int x = 0; x < 8; x++)
            {
                int column = left + x;

                if (column >= component.PlaneWidth)
                    break;

                component.Plane[(row * component.PlaneWidth) + column] =
                    (byte)Math.Clamp(Math.Round(block[(y * 8) + x]) + 128, 0, 255);
            }
        }
    }

    private static JpegFrameSamples Interleave(
        JpegScanComponent[] components,
        int width,
        int height,
        int horizontalMax,
        int verticalMax)
    {
        int count = components.Length;
        int[] samples = new int[width * height * count];

        for (int index = 0; index < count; index++)
        {
            JpegScanComponent component = components[index];

            for (int row = 0; row < height; row++)
            {
                int sourceRow = row * component.VerticalSampling / verticalMax * component.PlaneWidth;

                for (int column = 0; column < width; column++)
                {
                    int sourceColumn = column * component.HorizontalSampling / horizontalMax;

                    samples[(((row * width) + column) * count) + index] =
                        component.Plane[sourceRow + sourceColumn];
                }
            }
        }

        return new JpegFrameSamples(samples, width, height, count, 8, [.. components.Select(static c => c.Id)]);
    }

    private static int Extend(int value, int category) =>
        value < (1 << (category - 1)) ? value - (1 << category) + 1 : value;
}
