namespace Dicom.Codecs.Jpeg;

/// <summary>
/// The samples a JPEG stream decoded to, interleaved by pixel, together with what the frame
/// header said about them.
/// </summary>
internal sealed class JpegFrameSamples
{
    public JpegFrameSamples(int[] samples, int width, int height, int components, int precision, int[] componentIds)
    {
        Samples = samples;
        Width = width;
        Height = height;
        Components = components;
        Precision = precision;
        ComponentIds = componentIds;
    }

    /// <summary>The samples, interleaved by pixel and in row order.</summary>
    public int[] Samples { get; }

    /// <summary>The number of columns.</summary>
    public int Width { get; }

    /// <summary>The number of rows.</summary>
    public int Height { get; }

    /// <summary>The number of samples at each pixel.</summary>
    public int Components { get; }

    /// <summary>The number of bits each sample carries.</summary>
    public int Precision { get; }

    /// <summary>The component identifiers the frame header listed, in order.</summary>
    public int[] ComponentIds { get; }

    /// <summary>
    /// Whether the three components are luminance and chrominance rather than red, green and
    /// blue, which is what a baseline stream carries unless it says otherwise.
    /// </summary>
    public bool IsYCbCr { get; set; }

    /// <summary>Converts luminance and chrominance samples to red, green and blue in place.</summary>
    public void ToRgb()
    {
        if (!IsYCbCr || Components != 3)
            return;

        for (int i = 0; i < Samples.Length; i += 3)
        {
            double luminance = Samples[i];
            double blue = Samples[i + 1] - 128.0;
            double red = Samples[i + 2] - 128.0;

            Samples[i] = Clamp(luminance + (1.402 * red));
            Samples[i + 1] = Clamp(luminance - (0.344136 * blue) - (0.714136 * red));
            Samples[i + 2] = Clamp(luminance + (1.772 * blue));
        }

        IsYCbCr = false;
    }

    private static int Clamp(double value) => (int)Math.Clamp(Math.Round(value), 0, 255);

    /// <summary>
    /// The samples written out as uncompressed pixel data of the given layout, in little endian
    /// order and interleaved by pixel.
    /// </summary>
    public byte[] ToFrameBytes(FrameFormat format)
    {
        if (Width != format.Columns || Height != format.Rows || Components != format.SamplesPerPixel)
            throw new CodecException(
                $"The compressed frame is {Width}x{Height}x{Components}, but the data set describes "
                + $"{format.Columns}x{format.Rows}x{format.SamplesPerPixel}.");

        byte[] bytes = new byte[format.FrameLength];

        if (format.BytesPerSample == 1)
        {
            for (int i = 0; i < Samples.Length; i++)
                bytes[i] = (byte)Samples[i];

            return bytes;
        }

        for (int i = 0; i < Samples.Length; i++)
        {
            bytes[i * 2] = (byte)Samples[i];
            bytes[(i * 2) + 1] = (byte)(Samples[i] >> 8);
        }

        return bytes;
    }
}
