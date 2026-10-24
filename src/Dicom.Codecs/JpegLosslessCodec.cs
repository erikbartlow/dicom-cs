using Dicom.Codecs.Jpeg;

namespace Dicom.Codecs;

/// <summary>
/// Lossless JPEG, as transfer syntaxes 1.2.840.10008.1.2.4.57 and .70 carry it. Streams of
/// either syntax are read with whichever predictor they state; writing always uses the first
/// order predictor, which both syntaxes allow.
/// </summary>
public sealed class JpegLosslessCodec : IPixelCodec
{
    public JpegLosslessCodec(string transferSyntaxUid, string name)
    {
        TransferSyntaxUid = transferSyntaxUid;
        Name = name;
    }

    public string TransferSyntaxUid { get; }

    public string Name { get; }

    public bool CanDecode => true;

    public bool CanEncode => true;

    public DecodedFrame Decode(IReadOnlyList<byte[]> fragments, FrameFormat format)
    {
        JpegFrameSamples samples = JpegLosslessDecoder.Decode(Codec.Concatenate(fragments));

        return new DecodedFrame(
            samples.ToFrameBytes(format),
            format.PhotometricInterpretation,
            format.PlanarConfiguration);
    }

    public string DecodedPhotometricInterpretation(FrameFormat format) => format.PhotometricInterpretation;

    public EncodedFrame Encode(ReadOnlySpan<byte> frame, FrameFormat format, int quality)
    {
        if (format.BitsAllocated is not (8 or 16))
            throw new CodecException(
                $"{format.BitsAllocated} bit samples cannot be compressed as lossless JPEG.");

        return new EncodedFrame(
            JpegLosslessEncoder.Encode(frame, format),
            format.PhotometricInterpretation,
            lossy: false);
    }
}

/// <summary>
/// Helpers shared by the codecs.
/// </summary>
internal static class Codec
{
    /// <summary>The fragments of one frame joined into a single stream.</summary>
    public static byte[] Concatenate(IReadOnlyList<byte[]> fragments)
    {
        if (fragments.Count == 0)
            throw new CodecException("The frame has no compressed data.");

        if (fragments.Count == 1)
            return fragments[0];

        int length = 0;

        foreach (byte[] fragment in fragments)
            length += fragment.Length;

        byte[] joined = new byte[length];
        int offset = 0;

        foreach (byte[] fragment in fragments)
        {
            fragment.CopyTo(joined, offset);
            offset += fragment.Length;
        }

        return joined;
    }
}
