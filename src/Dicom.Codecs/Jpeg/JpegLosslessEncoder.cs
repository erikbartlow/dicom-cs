namespace Dicom.Codecs.Jpeg;

/// <summary>
/// Writes a lossless JPEG stream using the first order predictor, which is the encoding
/// transfer syntax 1.2.840.10008.1.2.4.70 requires. One Huffman table is built per stream
/// from the differences the image actually produces.
/// </summary>
internal static class JpegLosslessEncoder
{
    private const int Predictor = 1;

    /// <summary>Compresses one frame of uncompressed samples.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> frame, FrameFormat format)
    {
        int components = format.SamplesPerPixel;
        int width = format.Columns;
        int height = format.Rows;
        int[] samples = ReadSamples(frame, format, out int precision);

        int[] frequencies = new int[17];
        int[] categories = new int[samples.Length];
        int[] differences = new int[samples.Length];

        Differences(samples, width, height, components, precision, categories, differences, frequencies);

        JpegHuffmanTable table = JpegHuffmanTable.FromFrequencies(frequencies, 17);
        (int Code, int Length)[] codes = table.BuildEncodeTable();

        List<byte> output = [0xFF, 0xD8];

        WriteFrameHeader(output, width, height, components, precision);
        WriteHuffmanTable(output, table);
        WriteScanHeader(output, components);

        JpegBitWriter writer = new(output);

        for (int i = 0; i < samples.Length; i++)
        {
            int category = categories[i];
            writer.Write(codes[category]);

            if (category is 0 or 16)
                continue;

            int difference = differences[i];
            int value = difference < 0 ? difference + (1 << category) - 1 : difference;

            writer.Write(value, category);
        }

        writer.Flush();
        output.Add(0xFF);
        output.Add(0xD9);

        return [.. output];
    }

    private static void Differences(
        int[] samples,
        int width,
        int height,
        int components,
        int precision,
        int[] categories,
        int[] differences,
        int[] frequencies)
    {
        int start0 = 1 << (precision - 1);

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                for (int component = 0; component < components; component++)
                {
                    int index = (((row * width) + column) * components) + component;

                    int prediction = (row, column) switch
                    {
                        (0, 0) => start0,
                        (0, _) => samples[index - components],
                        (_, 0) => samples[index - (width * components)],
                        _ => samples[index - components],
                    };

                    int difference = Wrap(samples[index] - prediction);
                    int category = Category(difference);

                    categories[index] = category;
                    differences[index] = difference;
                    frequencies[category]++;
                }
            }
        }
    }

    private static int Wrap(int difference)
    {
        int wrapped = difference & 0xFFFF;

        return wrapped > 32767 ? wrapped - 65536 : wrapped;
    }

    private static int Category(int difference)
    {
        if (difference == 0)
            return 0;

        if (difference == -32768)
            return 16;

        int magnitude = Math.Abs(difference);
        int bits = 0;

        while (magnitude > 0)
        {
            magnitude >>= 1;
            bits++;
        }

        return bits;
    }

    private static void WriteFrameHeader(List<byte> output, int width, int height, int components, int precision)
    {
        output.Add(0xFF);
        output.Add(0xC3);
        Write16(output, 8 + (components * 3));
        output.Add((byte)precision);
        Write16(output, height);
        Write16(output, width);
        output.Add((byte)components);

        for (int i = 0; i < components; i++)
        {
            output.Add((byte)(i + 1));
            output.Add(0x11);
            output.Add(0x00);
        }
    }

    private static void WriteHuffmanTable(List<byte> output, JpegHuffmanTable table)
    {
        output.Add(0xFF);
        output.Add(0xC4);
        Write16(output, 2 + 1 + 16 + table.Values.Length);
        output.Add(0x00);

        for (int length = 1; length <= 16; length++)
            output.Add(table.Counts[length]);

        output.AddRange(table.Values);
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
            output.Add(0x00);
        }

        output.Add(Predictor);
        output.Add(0x00);
        output.Add(0x00);
    }

    private static void Write16(List<byte> output, int value)
    {
        output.Add((byte)(value >> 8));
        output.Add((byte)value);
    }

    private static int[] ReadSamples(ReadOnlySpan<byte> frame, FrameFormat format, out int precision)
    {
        if (frame.Length < format.FrameLength)
            throw new CodecException("The frame is shorter than the data set describes.");

        int count = format.SampleCount;
        int[] samples = new int[count];
        int highest = 0;

        for (int i = 0; i < count; i++)
        {
            int value = format.BytesPerSample == 1
                ? frame[i]
                : frame[i * 2] | (frame[(i * 2) + 1] << 8);

            samples[i] = value;
            highest |= value;
        }

        precision = Math.Clamp(format.BitsStored, 2, 16);

        while (precision < 16 && highest >= (1 << precision))
            precision++;

        return samples;
    }
}
