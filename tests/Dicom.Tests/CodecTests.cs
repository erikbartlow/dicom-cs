using Dicom.Codecs;

namespace Dicom.Tests;

public class CodecTests
{
    [Theory]
    [InlineData(16, 1)]
    [InlineData(8, 1)]
    [InlineData(8, 3)]
    public void LosslessJpegReproducesEverySample(int bits, int samplesPerPixel)
    {
        FrameFormat format = new(37, 53, samplesPerPixel, bits, bits, isSigned: false,
            photometricInterpretation: samplesPerPixel == 3 ? "RGB" : "MONOCHROME2");
        byte[] frame = Ramp(format);

        IPixelCodec codec = PixelCodecRegistry.RequireDecoder("1.2.840.10008.1.2.4.70");
        EncodedFrame encoded = codec.Encode(frame, format, 100);
        DecodedFrame decoded = codec.Decode([encoded.Fragment], format);

        Assert.False(encoded.Lossy);
        Assert.Equal(frame, decoded.Bytes);
    }

    [Fact]
    public void LosslessJpegReproducesSignedSamples()
    {
        FrameFormat format = new(16, 16, 1, 16, 16, isSigned: true);
        byte[] frame = new byte[format.FrameLength];

        for (int i = 0; i < format.SampleCount; i++)
        {
            short value = (short)(-2000 + (i * 37));

            frame[i * 2] = (byte)value;
            frame[(i * 2) + 1] = (byte)(value >> 8);
        }

        IPixelCodec codec = PixelCodecRegistry.RequireDecoder("1.2.840.10008.1.2.4.70");
        DecodedFrame decoded = codec.Decode([codec.Encode(frame, format, 100).Fragment], format);

        Assert.Equal(frame, decoded.Bytes);
    }

    [Fact]
    public void RunLengthEncodingReproducesEverySample()
    {
        FrameFormat format = new(21, 34, 1, 16, 16, isSigned: false);
        byte[] frame = Ramp(format);

        IPixelCodec codec = PixelCodecRegistry.RequireDecoder("1.2.840.10008.1.2.5");
        DecodedFrame decoded = codec.Decode([codec.Encode(frame, format, 100).Fragment], format);

        Assert.Equal(frame, decoded.Bytes);
    }

    [Fact]
    public void BaselineJpegKeepsGrayscaleCloseToTheOriginal()
    {
        FrameFormat format = new(32, 32, 1, 8, 8, isSigned: false);
        byte[] frame = Ramp(format);

        IPixelCodec codec = PixelCodecRegistry.RequireEncoder("1.2.840.10008.1.2.4.50");
        EncodedFrame encoded = codec.Encode(frame, format, 100);
        DecodedFrame decoded = codec.Decode([encoded.Fragment], format);

        Assert.True(encoded.Lossy);
        Assert.Equal("MONOCHROME2", decoded.PhotometricInterpretation);
        AssertClose(frame, decoded.Bytes, tolerance: 4);
    }

    [Fact]
    public void BaselineJpegKeepsColourCloseToTheOriginal()
    {
        FrameFormat format = new(24, 40, 3, 8, 8, isSigned: false, photometricInterpretation: "RGB");
        byte[] frame = new byte[format.FrameLength];

        for (int row = 0; row < format.Rows; row++)
        {
            for (int column = 0; column < format.Columns; column++)
            {
                int offset = ((row * format.Columns) + column) * 3;

                frame[offset] = (byte)(column * 6);
                frame[offset + 1] = (byte)(row * 10);
                frame[offset + 2] = 64;
            }
        }

        IPixelCodec codec = PixelCodecRegistry.RequireEncoder("1.2.840.10008.1.2.4.50");
        EncodedFrame encoded = codec.Encode(frame, format, 100);

        Assert.Equal("YBR_FULL_422", encoded.PhotometricInterpretation);

        DecodedFrame decoded = codec.Decode([encoded.Fragment], format);

        Assert.Equal("RGB", decoded.PhotometricInterpretation);
        AssertClose(frame, decoded.Bytes, tolerance: 12);
    }

    [Fact]
    public void BaselineJpegReadsItsOwnRestartFreeStreamOfOddSize()
    {
        FrameFormat format = new(13, 19, 1, 8, 8, isSigned: false);
        byte[] frame = Ramp(format);

        IPixelCodec codec = PixelCodecRegistry.RequireEncoder("1.2.840.10008.1.2.4.50");
        DecodedFrame decoded = codec.Decode([codec.Encode(frame, format, 90).Fragment], format);

        Assert.Equal(format.FrameLength, decoded.Bytes.Length);
        AssertClose(frame, decoded.Bytes, tolerance: 12);
    }

    /// <summary>
    /// The lumbar studies the analysis is run against are stored as JPEG Lossless, Selection
    /// Value 1 by the facilities that send them, so they exercise the decoder against streams
    /// this library did not write. The expected sizes and digests were taken from DicomObjects
    /// 8.48 reading the same files. The test does nothing where the studies are not installed.
    /// </summary>
    [Theory]
    [InlineData("Lumbar Flexion [01-Jan-2024 08.00.00].dcm", 1758, 2819, "0DB226B1122C1394C494A4CF2AF9E832")]
    [InlineData("Lumbar Extension [01-Jan-2024 08.00.00].dcm", 1828, 2871, "F1072EAF0878B03AAA5CC28D9066C7CD")]
    [InlineData("Lumbar Neutral Lateral [01-Jan-2024 08.00.00].dcm", 1470, 2871, "1B1F7B8DF52D659856B91C5AE4E8DD3C")]
    public void DecodesTheLumbarTestStudiesAsDicomObjectsDoes(string name, int width, int height, string digest)
    {
        string path = Path.Combine(@"C:\Public Access\MMDx\Sites\TestingSource", name);

        if (!File.Exists(path))
            return;

        DicomObjects.DicomImage image = new();
        image.Read(path);

        Assert.Equal("1.2.840.10008.1.2.4.70", image.OriginalTS);
        Assert.Equal(width, image.Size.Width);
        Assert.Equal(height, image.Size.Height);

        ushort[] samples = Assert.IsType<ushort[]>(image.RawPixelValuesByFrame(1));

        Assert.Equal(width * height, samples.Length);
        Assert.Equal(digest, Digest(samples));
    }

    private static string Digest(ushort[] samples)
    {
        byte[] bytes = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);

        return Convert.ToHexString(System.Security.Cryptography.MD5.HashData(bytes));
    }

    private static byte[] Ramp(FrameFormat format)
    {
        byte[] frame = new byte[format.FrameLength];

        for (int i = 0; i < format.SampleCount; i++)
        {
            int value = ((i * 97) % 4093) & ((1 << format.BitsStored) - 1);

            if (format.BytesPerSample == 1)
            {
                frame[i] = (byte)value;
                continue;
            }

            frame[i * 2] = (byte)value;
            frame[(i * 2) + 1] = (byte)(value >> 8);
        }

        return frame;
    }

    private static void AssertClose(byte[] expected, byte[] actual, int tolerance)
    {
        Assert.Equal(expected.Length, actual.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.True(
                Math.Abs(expected[i] - actual[i]) <= tolerance,
                $"Sample {i} is {actual[i]} but {expected[i]} was expected.");
        }
    }
}
