using CharLS.Managed;
using CoreJ2K;
using CoreJ2K.j2k.image;
using CoreJ2K.j2k.util;
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
    /// Extended sequential streams differ from baseline ones in their marker and in allowing
    /// twelve bit samples, so both precisions are read back from a stream whose blocks are
    /// coded exactly.
    /// </summary>
    [Theory]
    [InlineData(8)]
    [InlineData(12)]
    public void ExtendedSequentialJpegReadsSamplesOfEitherPrecision(int precision)
    {
        FrameFormat format = new(24, 32, 1, precision > 8 ? 16 : 8, precision, isSigned: false);
        int[] blocks = BlockValues(format, precision);

        IPixelCodec codec = PixelCodecRegistry.RequireDecoder("1.2.840.10008.1.2.4.51");
        DecodedFrame decoded = codec.Decode([ExtendedJpeg(format, blocks, precision)], format);

        Assert.Equal("MONOCHROME2", decoded.PhotometricInterpretation);
        Assert.Equal(BlockFrame(format, blocks), decoded.Bytes);
    }

    [Fact]
    public void ExtendedSequentialJpegRefusesToCompress()
    {
        FrameFormat format = new(8, 8, 1, 16, 12, isSigned: false);
        IPixelCodec codec = PixelCodecRegistry.RequireDecoder("1.2.840.10008.1.2.4.51");

        Assert.False(codec.CanEncode);
        Assert.Throws<CodecException>(() => codec.Encode(new byte[format.FrameLength], format, 100));
    }

    /// <summary>
    /// JPEG 2000 Lossless is the syntax most likely to arrive from a facility, so the samples
    /// must come back exactly. The streams are written by the same codec library that reads
    /// them, which settles the layout this library has to apply but not the library's own
    /// conformance; sample files from facilities are compared with DicomObjects separately.
    /// </summary>
    [Theory]
    [InlineData(8, false, 1)]
    [InlineData(16, false, 1)]
    [InlineData(16, true, 1)]
    [InlineData(8, false, 3)]
    public void Jpeg2000ReproducesEverySampleOfALosslessStream(int bits, bool isSigned, int samplesPerPixel)
    {
        FrameFormat format = new(29, 43, samplesPerPixel, bits, bits, isSigned,
            photometricInterpretation: samplesPerPixel == 3 ? "RGB" : "MONOCHROME2");
        byte[] frame = isSigned ? SignedRamp(format) : Ramp(format);

        IPixelCodec codec = PixelCodecRegistry.RequireDecoder("1.2.840.10008.1.2.4.90");
        DecodedFrame decoded = codec.Decode([Jpeg2000Lossless(frame, format)], format);

        Assert.Equal(frame, decoded.Bytes);
    }

    [Fact]
    public void Jpeg2000ReportsColourFramesAsRedGreenAndBlue()
    {
        FrameFormat format = new(8, 8, 3, 8, 8, isSigned: false, photometricInterpretation: "YBR_RCT");
        IPixelCodec codec = PixelCodecRegistry.RequireDecoder("1.2.840.10008.1.2.4.90");

        Assert.Equal("RGB", codec.DecodedPhotometricInterpretation(format));
    }

    [Fact]
    public void Jpeg2000RefusesToCompress()
    {
        FrameFormat format = new(8, 8, 1, 16, 16, isSigned: false);
        IPixelCodec codec = PixelCodecRegistry.RequireDecoder("1.2.840.10008.1.2.4.90");

        Assert.False(codec.CanEncode);
        Assert.Throws<CodecException>(() => codec.Encode(new byte[format.FrameLength], format, 100));
    }

    [Theory]
    [InlineData(8, 1)]
    [InlineData(12, 1)]
    [InlineData(16, 1)]
    [InlineData(8, 3)]
    [InlineData(16, 3)]
    public void JpegLsReproducesEverySample(int bitsStored, int samplesPerPixel)
    {
        int bitsAllocated = bitsStored > 8 ? 16 : 8;
        FrameFormat format = new(23, 31, samplesPerPixel, bitsAllocated, bitsStored, isSigned: false,
            photometricInterpretation: samplesPerPixel == 3 ? "RGB" : "MONOCHROME2");
        byte[] frame = Ramp(format);

        IPixelCodec codec = PixelCodecRegistry.RequireDecoder("1.2.840.10008.1.2.4.80");
        DecodedFrame decoded = codec.Decode([JpegLs(frame, format, InterleaveMode.Sample)], format);

        Assert.Equal(format.PhotometricInterpretation, decoded.PhotometricInterpretation);
        Assert.Equal(frame, decoded.Bytes);
    }

    [Fact]
    public void JpegLsReproducesSignedSamples()
    {
        FrameFormat format = new(16, 16, 1, 16, 16, isSigned: true);
        byte[] frame = SignedRamp(format);

        IPixelCodec codec = PixelCodecRegistry.RequireDecoder("1.2.840.10008.1.2.4.80");
        DecodedFrame decoded = codec.Decode([JpegLs(frame, format, InterleaveMode.Sample)], format);

        Assert.Equal(frame, decoded.Bytes);
    }

    /// <summary>
    /// A colour stream may hold its components plane by plane whatever the data set says, so the
    /// decoder has to put the samples in the order the data set describes.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void JpegLsPutsPlanarColourInTheOrderTheDataSetDescribes(int planarConfiguration)
    {
        FrameFormat format = new(12, 20, 3, 8, 8, isSigned: false,
            planarConfiguration: planarConfiguration, photometricInterpretation: "RGB");
        byte[] frame = Ramp(format);

        IPixelCodec codec = PixelCodecRegistry.RequireDecoder("1.2.840.10008.1.2.4.80");
        DecodedFrame decoded = codec.Decode([JpegLs(frame, format, InterleaveMode.None)], format);

        Assert.Equal(planarConfiguration, decoded.PlanarConfiguration);
        Assert.Equal(frame, decoded.Bytes);
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

    /// <summary>A lossless JPEG 2000 codestream of the frame, without the JP2 wrapper.</summary>
    private static byte[] Jpeg2000Lossless(byte[] frame, FrameFormat format)
    {
        List<Stream> components = [];

        for (int component = 0; component < format.SamplesPerPixel; component++)
            components.Add(PgxComponent(frame, format, component));

        BlkImgDataSrc source = (components.Count == 1
            ? J2kImage.CreateEncodableSource(components[0])
            : J2kImage.CreateEncodableSource(components))
            ?? throw new InvalidOperationException("The components could not be read for encoding.");

        ParameterList parameters = J2kImage.GetDefaultEncoderParameterList();
        parameters["lossless"] = "on";
        parameters["file_format"] = "off";

        return J2kImage.ToBytes(source, parameters);
    }

    /// <summary>
    /// One component of the frame as a PGX stream, which is what the JPEG 2000 encoder reads a
    /// single component of arbitrary bit depth and signedness from.
    /// </summary>
    private static MemoryStream PgxComponent(byte[] frame, FrameFormat format, int component)
    {
        MemoryStream stream = new();
        string sign = format.IsSigned ? "-" : "+";

        stream.Write(System.Text.Encoding.ASCII.GetBytes(
            $"PG LM {sign} {format.BitsStored} {format.Columns} {format.Rows}\n"));

        int stride = format.SamplesPerPixel * format.BytesPerSample;
        int offset = component * format.BytesPerSample;

        for (int i = 0; i < format.Rows * format.Columns; i++)
            stream.Write(frame, offset + (i * stride), format.BytesPerSample);

        stream.Position = 0;

        return stream;
    }

    /// <summary>
    /// A JPEG-LS stream of the frame, stored in the given interleave mode. A single component
    /// is only ever stored as one plane, whatever is asked for.
    /// </summary>
    private static byte[] JpegLs(byte[] frame, FrameFormat format, InterleaveMode interleaveMode)
    {
        InterleaveMode mode = format.SamplesPerPixel == 1 ? InterleaveMode.None : interleaveMode;
        bool sourceIsPlanar = format.SamplesPerPixel > 1 && mode == InterleaveMode.None;
        byte[] source = Reorder(frame, format, planar: sourceIsPlanar);
        FrameInfo info = new(format.Columns, format.Rows, format.BitsStored, format.SamplesPerPixel);

        return JpegLSEncoder.Encode(source, info, mode, EncodingOptions.None, stride: 0).ToArray();
    }

    /// <summary>The frame with its colour samples laid out plane by plane, or pixel by pixel.</summary>
    private static byte[] Reorder(byte[] frame, FrameFormat format, bool planar)
    {
        if (format.SamplesPerPixel == 1 || planar == (format.PlanarConfiguration == 1))
            return frame;

        byte[] bytes = new byte[frame.Length];
        int pixels = format.Rows * format.Columns;

        for (int component = 0; component < format.SamplesPerPixel; component++)
        {
            for (int i = 0; i < pixels; i++)
            {
                int from = planar
                    ? ((i * format.SamplesPerPixel) + component) * format.BytesPerSample
                    : ((component * pixels) + i) * format.BytesPerSample;
                int to = planar
                    ? ((component * pixels) + i) * format.BytesPerSample
                    : ((i * format.SamplesPerPixel) + component) * format.BytesPerSample;

                Array.Copy(frame, from, bytes, to, format.BytesPerSample);
            }
        }

        return bytes;
    }

    /// <summary>The value each eight by eight block of the frame carries, spread over the range.</summary>
    private static int[] BlockValues(FrameFormat format, int precision)
    {
        int[] values = new int[(format.Rows / 8) * (format.Columns / 8)];

        for (int i = 0; i < values.Length; i++)
            values[i] = (i * 331) % (1 << precision);

        return values;
    }

    /// <summary>The uncompressed frame those block values stand for.</summary>
    private static byte[] BlockFrame(FrameFormat format, int[] blocks)
    {
        byte[] frame = new byte[format.FrameLength];
        int across = format.Columns / 8;

        for (int row = 0; row < format.Rows; row++)
        {
            for (int column = 0; column < format.Columns; column++)
            {
                int value = blocks[((row / 8) * across) + (column / 8)];
                int offset = ((row * format.Columns) + column) * format.BytesPerSample;

                frame[offset] = (byte)value;

                if (format.BytesPerSample == 2)
                    frame[offset + 1] = (byte)(value >> 8);
            }
        }

        return frame;
    }

    /// <summary>
    /// An extended sequential stream of the given precision whose blocks each hold one value.
    /// Only the direct current coefficient of each block is coded, against a quantisation table
    /// of ones and Huffman tables written into the stream, so the samples come back exactly.
    /// </summary>
    private static byte[] ExtendedJpeg(FrameFormat format, int[] blocks, int precision)
    {
        List<byte> output = [0xFF, 0xD8, 0xFF, 0xDB, 0x00, 0x43, 0x00];

        output.AddRange(Enumerable.Repeat((byte)1, 64));
        output.AddRange([0xFF, 0xC1, 0x00, 0x0B, (byte)precision]);
        output.AddRange([(byte)(format.Rows >> 8), (byte)format.Rows]);
        output.AddRange([(byte)(format.Columns >> 8), (byte)format.Columns]);
        output.AddRange([0x01, 0x01, 0x11, 0x00]);

        // Sixteen direct current codes of five bits, so every category up to fifteen is defined.
        output.AddRange([0xFF, 0xC4, 0x00, 0x23, 0x00, 0, 0, 0, 0, 16, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
        output.AddRange(Enumerable.Range(0, 16).Select(static c => (byte)c));

        // One alternating current code of a single bit, which is the end of block that follows.
        output.AddRange([0xFF, 0xC4, 0x00, 0x14, 0x10, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x00]);
        output.AddRange([0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00]);

        int accumulator = 0;
        int filled = 0;

        void Emit(int value, int length)
        {
            for (int i = length - 1; i >= 0; i--)
            {
                accumulator = (accumulator << 1) | ((value >> i) & 1);
                filled++;

                if (filled < 8)
                    continue;

                output.Add((byte)accumulator);

                if (accumulator == 0xFF)
                    output.Add(0x00);

                accumulator = 0;
                filled = 0;
            }
        }

        int shift = 1 << (precision - 1);
        int previous = 0;

        foreach (int value in blocks)
        {
            // The inverse transform of a lone direct current coefficient is an eighth of it.
            int coefficient = (value - shift) * 8;
            int difference = coefficient - previous;
            int magnitude = Math.Abs(difference);
            int category = 0;

            previous = coefficient;

            while (magnitude >= (1 << category))
                category++;

            Emit(category, 5);

            if (category > 0)
                Emit(difference > 0 ? difference : difference + (1 << category) - 1, category);

            Emit(0, 1);
        }

        if (filled > 0)
            Emit((1 << (8 - filled)) - 1, 8 - filled);

        output.AddRange([0xFF, 0xD9]);

        return [.. output];
    }

    private static byte[] SignedRamp(FrameFormat format)
    {
        byte[] frame = new byte[format.FrameLength];

        for (int i = 0; i < format.SampleCount; i++)
        {
            short value = (short)(-2000 + (i * 37));

            frame[i * 2] = (byte)value;
            frame[(i * 2) + 1] = (byte)(value >> 8);
        }

        return frame;
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
