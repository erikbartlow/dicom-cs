namespace Dicom.Codecs.Jpeg;

/// <summary>
/// Writes a baseline JPEG stream of eight bit samples. Grayscale frames are written as one
/// component at full resolution; colour frames are converted to luminance and chrominance and
/// the chrominance is halved horizontally, which is the sampling the YBR_FULL_422 photometric
/// interpretation describes.
/// </summary>
internal static class JpegBaselineEncoder
{
    /// <summary>Compresses one frame of eight bit samples.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> frame, FrameFormat format, int quality)
    {
        if (format.BitsAllocated != 8)
            throw new CodecException("Baseline JPEG carries eight bit samples only.");

        if (format.SamplesPerPixel is not (1 or 3))
            throw new CodecException(
                $"{format.SamplesPerPixel} samples per pixel cannot be written as baseline JPEG.");

        if (format.SamplesPerPixel == 3 && format.PlanarConfiguration != 0)
            throw new CodecException("Colour samples must be interleaved by pixel before they are compressed.");

        bool colour = format.SamplesPerPixel == 3;
        JpegPlane[] planes = colour ? ColourPlanes(frame, format) : [GrayPlane(frame, format)];

        int[] luminanceQuantisation = JpegTables.Scale(JpegTables.LuminanceQuantisation, quality);
        int[] chrominanceQuantisation = JpegTables.Scale(JpegTables.ChrominanceQuantisation, quality);

        List<byte> output = [0xFF, 0xD8];

        WriteQuantisationTable(output, 0, luminanceQuantisation);

        if (colour)
            WriteQuantisationTable(output, 1, chrominanceQuantisation);

        WriteFrameHeader(output, format.Rows, format.Columns, planes);
        WriteHuffmanTables(output, colour);
        WriteScanHeader(output, planes.Length);

        JpegBitWriter writer = new(output);
        JpegEncodeTables tables = new(colour);

        WriteScan(writer, planes, format, luminanceQuantisation, chrominanceQuantisation, tables);

        writer.Flush();
        output.Add(0xFF);
        output.Add(0xD9);

        return [.. output];
    }

    private static void WriteScan(
        JpegBitWriter writer,
        JpegPlane[] planes,
        FrameFormat format,
        int[] luminanceQuantisation,
        int[] chrominanceQuantisation,
        JpegEncodeTables tables)
    {
        int horizontalMax = planes[0].HorizontalSampling;
        int mcuWidth = 8 * horizontalMax;
        int mcusAcross = (format.Columns + mcuWidth - 1) / mcuWidth;
        int mcusDown = (format.Rows + 7) / 8;

        int[] previousDc = new int[planes.Length];
        double[] block = new double[64];
        double[] scratch = new double[64];
        int[] coefficients = new int[64];

        for (int mcuY = 0; mcuY < mcusDown; mcuY++)
        {
            for (int mcuX = 0; mcuX < mcusAcross; mcuX++)
            {
                for (int component = 0; component < planes.Length; component++)
                {
                    JpegPlane plane = planes[component];
                    bool luminance = component == 0;
                    int[] quantisation = luminance ? luminanceQuantisation : chrominanceQuantisation;

                    for (int blockX = 0; blockX < plane.HorizontalSampling; blockX++)
                    {
                        plane.ReadBlock(
                            (mcuX * plane.HorizontalSampling * 8) + (blockX * 8),
                            mcuY * 8,
                            block);

                        JpegTransform.Forward(block, scratch);
                        Quantise(block, quantisation, coefficients);

                        previousDc[component] = WriteBlock(
                            writer,
                            coefficients,
                            previousDc[component],
                            luminance ? tables.DcLuminance : tables.DcChrominance,
                            luminance ? tables.AcLuminance : tables.AcChrominance);
                    }
                }
            }
        }
    }

    private static void Quantise(double[] block, int[] quantisation, int[] coefficients)
    {
        for (int i = 0; i < 64; i++)
        {
            int natural = JpegTables.ZigZag[i];

            coefficients[i] = (int)Math.Round(block[natural] / quantisation[natural]);
        }
    }

    private static int WriteBlock(
        JpegBitWriter writer,
        int[] coefficients,
        int previousDc,
        (int Code, int Length)[] dcTable,
        (int Code, int Length)[] acTable)
    {
        int difference = coefficients[0] - previousDc;
        int category = Magnitude(difference);

        writer.Write(dcTable[category]);

        if (category > 0)
            writer.Write(Bits(difference, category), category);

        int run = 0;

        for (int i = 1; i < 64; i++)
        {
            if (coefficients[i] == 0)
            {
                run++;
                continue;
            }

            while (run > 15)
            {
                writer.Write(acTable[0xF0]);
                run -= 16;
            }

            int size = Magnitude(coefficients[i]);

            writer.Write(acTable[(run << 4) | size]);
            writer.Write(Bits(coefficients[i], size), size);
            run = 0;
        }

        if (run > 0)
            writer.Write(acTable[0x00]);

        return coefficients[0];
    }

    private static int Magnitude(int value)
    {
        int magnitude = Math.Abs(value);
        int bits = 0;

        while (magnitude > 0)
        {
            magnitude >>= 1;
            bits++;
        }

        return bits;
    }

    private static int Bits(int value, int size) => value < 0 ? value + (1 << size) - 1 : value;

    private static JpegPlane GrayPlane(ReadOnlySpan<byte> frame, FrameFormat format)
    {
        byte[] samples = frame[..format.FrameLength].ToArray();

        return new JpegPlane(samples, format.Columns, format.Rows, 1, 1);
    }

    /// <summary>
    /// The luminance and the two chrominance components of a colour frame, with the chrominance
    /// averaged over each pair of columns. Samples already in a luminance and chrominance space
    /// are taken as they are.
    /// </summary>
    private static JpegPlane[] ColourPlanes(ReadOnlySpan<byte> frame, FrameFormat format)
    {
        int width = format.Columns;
        int height = format.Rows;
        int chromaWidth = (width + 1) / 2;
        bool alreadyYbr = format.PhotometricInterpretation.StartsWith("YBR", StringComparison.OrdinalIgnoreCase);

        byte[] luminance = new byte[width * height];
        byte[] blue = new byte[chromaWidth * height];
        byte[] red = new byte[chromaWidth * height];

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int source = ((row * width) + column) * 3;
                int first = frame[source];
                int second = frame[source + 1];
                int third = frame[source + 2];

                luminance[(row * width) + column] = alreadyYbr
                    ? (byte)first
                    : Clamp(JpegTransform.Luminance(first, second, third));
            }

            for (int column = 0; column < chromaWidth; column++)
            {
                int left = column * 2;
                int right = Math.Min(left + 1, width - 1);

                int leftSource = ((row * width) + left) * 3;
                int rightSource = ((row * width) + right) * 3;

                double blueSum = Chrominance(frame, leftSource, alreadyYbr, blueDifference: true)
                    + Chrominance(frame, rightSource, alreadyYbr, blueDifference: true);
                double redSum = Chrominance(frame, leftSource, alreadyYbr, blueDifference: false)
                    + Chrominance(frame, rightSource, alreadyYbr, blueDifference: false);

                blue[(row * chromaWidth) + column] = Clamp(blueSum / 2.0);
                red[(row * chromaWidth) + column] = Clamp(redSum / 2.0);
            }
        }

        return
        [
            new JpegPlane(luminance, width, height, 2, 1),
            new JpegPlane(blue, chromaWidth, height, 1, 1),
            new JpegPlane(red, chromaWidth, height, 1, 1),
        ];
    }

    private static double Chrominance(ReadOnlySpan<byte> frame, int source, bool alreadyYbr, bool blueDifference)
    {
        int first = frame[source];
        int second = frame[source + 1];
        int third = frame[source + 2];

        if (alreadyYbr)
            return blueDifference ? second : third;

        return blueDifference
            ? JpegTransform.BlueDifference(first, second, third)
            : JpegTransform.RedDifference(first, second, third);
    }

    private static byte Clamp(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);

    private static void WriteQuantisationTable(List<byte> output, int identifier, int[] quantisation)
    {
        output.Add(0xFF);
        output.Add(0xDB);
        Write16(output, 67);
        output.Add((byte)identifier);

        for (int i = 0; i < 64; i++)
            output.Add((byte)quantisation[JpegTables.ZigZag[i]]);
    }

    private static void WriteFrameHeader(List<byte> output, int rows, int columns, JpegPlane[] planes)
    {
        output.Add(0xFF);
        output.Add(0xC0);
        Write16(output, 8 + (planes.Length * 3));
        output.Add(8);
        Write16(output, rows);
        Write16(output, columns);
        output.Add((byte)planes.Length);

        for (int i = 0; i < planes.Length; i++)
        {
            output.Add((byte)(i + 1));
            output.Add((byte)((planes[i].HorizontalSampling << 4) | planes[i].VerticalSampling));
            output.Add((byte)(i == 0 ? 0 : 1));
        }
    }

    private static void WriteHuffmanTables(List<byte> output, bool colour)
    {
        WriteHuffmanTable(output, 0x00, JpegTables.DcLuminanceCounts, JpegTables.DcLuminanceValues);
        WriteHuffmanTable(output, 0x10, JpegTables.AcLuminanceCounts, JpegTables.AcLuminanceValues);

        if (!colour)
            return;

        WriteHuffmanTable(output, 0x01, JpegTables.DcChrominanceCounts, JpegTables.DcChrominanceValues);
        WriteHuffmanTable(output, 0x11, JpegTables.AcChrominanceCounts, JpegTables.AcChrominanceValues);
    }

    private static void WriteHuffmanTable(List<byte> output, int identifier, byte[] counts, byte[] values)
    {
        output.Add(0xFF);
        output.Add(0xC4);
        Write16(output, 2 + 1 + 16 + values.Length);
        output.Add((byte)identifier);
        output.AddRange(counts);
        output.AddRange(values);
    }

    private static void WriteScanHeader(List<byte> output, int components)
    {
        output.Add(0xFF);
        output.Add(0xDA);
        Write16(output, 6 + (components * 2));
        output.Add((byte)components);

        for (int i = 0; i < components; i++)
        {
            output.Add((byte)(i + 1));
            output.Add((byte)(i == 0 ? 0x00 : 0x11));
        }

        output.Add(0x00);
        output.Add(0x3F);
        output.Add(0x00);
    }

    private static void Write16(List<byte> output, int value)
    {
        output.Add((byte)(value >> 8));
        output.Add((byte)value);
    }
}
