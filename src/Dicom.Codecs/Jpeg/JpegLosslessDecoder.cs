namespace Dicom.Codecs.Jpeg;

/// <summary>
/// Reads a lossless JPEG stream, as processes 14 and its selection value 1 variant produce.
/// The samples come back exactly as they were compressed.
/// </summary>
internal static class JpegLosslessDecoder
{
    /// <summary>The samples of the stream, with the layout the frame header states.</summary>
    public static JpegFrameSamples Decode(byte[] jpeg)
    {
        JpegHuffmanTable?[] tables = new JpegHuffmanTable?[4];
        int position = 2;
        int precision = 0;
        int width = 0;
        int height = 0;
        int componentCount = 0;
        int[] componentIds = [];
        int restartInterval = 0;

        if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
            throw new CodecException("The fragment does not begin with a JPEG start of image marker.");

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
                    continue;

                case 0xD9:
                    throw new CodecException("The compressed stream ends before its scan.");

                case 0xC3:
                case 0xC7:
                case 0xCB:
                case 0xCF:
                {
                    if (marker != 0xC3)
                        throw new CodecException(
                            $"Lossless JPEG of type {marker:X2} (hierarchical or arithmetic) is not supported.");

                    int length = Read16(jpeg, position);
                    precision = jpeg[position + 2];
                    height = Read16(jpeg, position + 3);
                    width = Read16(jpeg, position + 5);
                    componentCount = jpeg[position + 7];
                    componentIds = new int[componentCount];

                    for (int i = 0; i < componentCount; i++)
                    {
                        int offset = position + 8 + (i * 3);
                        componentIds[i] = jpeg[offset];

                        if (jpeg[offset + 1] != 0x11)
                            throw new CodecException("Subsampled lossless JPEG is not supported.");
                    }

                    position += length;
                    continue;
                }

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

                        byte[] values = jpeg[(cursor + 16)..(cursor + 16 + total)];
                        tables[identifier & 0x0F] = new JpegHuffmanTable(jpeg.AsSpan(cursor, 16), values);
                        cursor += 16 + total;
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

                case 0xDA:
                {
                    int length = Read16(jpeg, position);
                    int scanComponents = jpeg[position + 2];
                    int[] tableOf = new int[scanComponents];

                    for (int i = 0; i < scanComponents; i++)
                        tableOf[i] = jpeg[position + 4 + (i * 2)] >> 4;

                    int predictor = jpeg[position + 2 + (scanComponents * 2) + 1];
                    int pointTransform = jpeg[position + 2 + (scanComponents * 2) + 3] & 0x0F;

                    if (scanComponents != componentCount)
                        throw new CodecException("Lossless JPEG scans of a subset of components are not supported.");

                    int[] samples = DecodeScan(
                        jpeg,
                        position + length,
                        tables,
                        tableOf,
                        width,
                        height,
                        componentCount,
                        precision,
                        predictor,
                        pointTransform,
                        restartInterval);

                    return new JpegFrameSamples(samples, width, height, componentCount, precision, componentIds);
                }

                default:
                {
                    if (marker is >= 0xD0 and <= 0xD7 or 0x01)
                        continue;

                    position += Read16(jpeg, position);
                    continue;
                }
            }
        }

        throw new CodecException("The compressed stream has no scan.");
    }

    private static int[] DecodeScan(
        byte[] jpeg,
        int start,
        JpegHuffmanTable?[] tables,
        int[] tableOf,
        int width,
        int height,
        int components,
        int precision,
        int predictor,
        int pointTransform,
        int restartInterval)
    {
        if (width <= 0 || height <= 0)
            throw new CodecException("The compressed frame has no dimensions.");

        if (predictor is < 1 or > 7)
            throw new CodecException($"Predictor {predictor} is not one the specification defines.");

        JpegBitReader reader = new(jpeg, start);
        int[] samples = new int[width * height * components];
        int[] previous = new int[width * components];
        int[] current = new int[width * components];
        int start0 = 1 << (precision - 1 - pointTransform);
        int sinceRestart = 0;
        bool resetPrediction = true;

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                for (int component = 0; component < components; component++)
                {
                    JpegHuffmanTable table = tables[tableOf[component]]
                        ?? throw new CodecException("The scan refers to a Huffman table that was not defined.");

                    int index = (column * components) + component;
                    int prediction = resetPrediction
                        ? start0
                        : Predict(predictor, previous, current, index, components, row, column);

                    int category = table.Decode(reader);
                    int difference = category switch
                    {
                        0 => 0,
                        16 => 32768,
                        _ => Extend(reader.ReadBits(category), category),
                    };

                    current[index] = (prediction + difference) & 0xFFFF;
                }

                resetPrediction = false;

                if (restartInterval > 0 && ++sinceRestart == restartInterval)
                {
                    reader.Restart();
                    sinceRestart = 0;
                    resetPrediction = true;
                }
            }

            Array.Copy(current, 0, samples, row * width * components, width * components);
            (previous, current) = (current, previous);
        }

        if (pointTransform > 0)
        {
            for (int i = 0; i < samples.Length; i++)
                samples[i] = (samples[i] << pointTransform) & 0xFFFF;
        }

        return samples;
    }

    private static int Predict(
        int predictor,
        int[] previous,
        int[] current,
        int index,
        int components,
        int row,
        int column)
    {
        if (row == 0)
            return current[index - components];

        if (column == 0)
            return previous[index];

        int left = current[index - components];
        int above = previous[index];
        int aboveLeft = previous[index - components];

        return predictor switch
        {
            1 => left,
            2 => above,
            3 => aboveLeft,
            4 => left + above - aboveLeft,
            5 => left + ((above - aboveLeft) >> 1),
            6 => above + ((left - aboveLeft) >> 1),
            _ => (left + above) >> 1,
        };
    }

    private static int Extend(int value, int category) =>
        value < (1 << (category - 1)) ? value - (1 << category) + 1 : value;

    private static int Read16(byte[] data, int position) => (data[position] << 8) | data[position + 1];
}
