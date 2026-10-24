using System.Buffers.Binary;

namespace Dicom.Codecs;

/// <summary>
/// RLE Lossless, transfer syntax 1.2.840.10008.1.2.5. A frame is split into one segment per
/// byte of each sample, most significant byte first, and each segment is run length encoded
/// as the PackBits scheme describes.
/// </summary>
public sealed class RleCodec : IPixelCodec
{
    private const int HeaderLength = 64;

    public string TransferSyntaxUid => "1.2.840.10008.1.2.5";

    public string Name => "RLE Lossless";

    public bool CanDecode => true;

    public bool CanEncode => true;

    public DecodedFrame Decode(IReadOnlyList<byte[]> fragments, FrameFormat format)
    {
        byte[] data = Codec.Concatenate(fragments);

        if (data.Length < HeaderLength)
            throw new CodecException("The run length encoded frame has no header.");

        int segments = (int)BinaryPrimitives.ReadUInt32LittleEndian(data);
        int expected = format.SamplesPerPixel * format.BytesPerSample;

        if (segments != expected)
            throw new CodecException(
                $"The run length encoded frame has {segments} segments, but {expected} are expected.");

        byte[] frame = new byte[format.FrameLength];
        int bytesPerSample = format.BytesPerSample;

        for (int segment = 0; segment < segments; segment++)
        {
            int offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4 + (segment * 4)));
            int end = segment + 1 < segments
                ? (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8 + (segment * 4)))
                : data.Length;

            if (end <= 0 || end > data.Length)
                end = data.Length;

            int sample = segment / bytesPerSample;
            int byteOfSample = segment % bytesPerSample;
            int destination = (sample * bytesPerSample) + (bytesPerSample - 1 - byteOfSample);
            int stride = format.SamplesPerPixel * bytesPerSample;

            Expand(data.AsSpan(offset, end - offset), frame, destination, stride);
        }

        return new DecodedFrame(frame, format.PhotometricInterpretation, format.PlanarConfiguration);
    }

    public string DecodedPhotometricInterpretation(FrameFormat format) => format.PhotometricInterpretation;

    public EncodedFrame Encode(ReadOnlySpan<byte> frame, FrameFormat format, int quality)
    {
        int bytesPerSample = format.BytesPerSample;
        int segments = format.SamplesPerPixel * bytesPerSample;

        if (segments > 15)
            throw new CodecException("Run length encoding allows at most fifteen segments.");

        List<byte> output = new(HeaderLength);
        List<byte>[] encoded = new List<byte>[segments];
        int stride = segments;

        for (int segment = 0; segment < segments; segment++)
        {
            int sample = segment / bytesPerSample;
            int byteOfSample = segment % bytesPerSample;
            int source = (sample * bytesPerSample) + (bytesPerSample - 1 - byteOfSample);

            encoded[segment] = Compress(frame, source, stride, format.SampleCount);
        }

        byte[] header = new byte[HeaderLength];
        BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)segments);

        int offset = HeaderLength;

        for (int segment = 0; segment < segments; segment++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4 + (segment * 4)), (uint)offset);
            offset += encoded[segment].Count;
        }

        output.AddRange(header);

        foreach (List<byte> segment in encoded)
            output.AddRange(segment);

        if (output.Count % 2 != 0)
            output.Add(0x00);

        return new EncodedFrame([.. output], format.PhotometricInterpretation, lossy: false);
    }

    private static void Expand(ReadOnlySpan<byte> segment, byte[] frame, int destination, int stride)
    {
        int position = 0;

        while (position < segment.Length && destination < frame.Length)
        {
            int control = segment[position++];

            if (control == 128)
                continue;

            if (control < 128)
            {
                int count = control + 1;

                for (int i = 0; i < count && position < segment.Length && destination < frame.Length; i++)
                {
                    frame[destination] = segment[position++];
                    destination += stride;
                }

                continue;
            }

            if (position >= segment.Length)
                break;

            byte value = segment[position++];

            for (int i = 0; i < 257 - control && destination < frame.Length; i++)
            {
                frame[destination] = value;
                destination += stride;
            }
        }
    }

    private static List<byte> Compress(ReadOnlySpan<byte> frame, int source, int stride, int sampleCount)
    {
        byte[] plane = new byte[sampleCount];

        for (int i = 0; i < sampleCount; i++)
            plane[i] = frame[source + (i * stride)];

        List<byte> output = [];
        int position = 0;

        while (position < plane.Length)
        {
            int run = 1;

            while (run < 128 && position + run < plane.Length && plane[position + run] == plane[position])
                run++;

            if (run > 1)
            {
                output.Add((byte)(257 - run));
                output.Add(plane[position]);
                position += run;
                continue;
            }

            int literal = 1;

            while (literal < 128
                && position + literal < plane.Length
                && (position + literal + 1 >= plane.Length || plane[position + literal] != plane[position + literal + 1]))
            {
                literal++;
            }

            output.Add((byte)(literal - 1));

            for (int i = 0; i < literal; i++)
                output.Add(plane[position + i]);

            position += literal;
        }

        if (output.Count % 2 != 0)
            output.Add(0x00);

        return output;
    }
}
