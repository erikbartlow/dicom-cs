using Dicom.Codecs.Jpeg;

namespace Dicom.Codecs;

/// <summary>
/// Extended sequential JPEG, transfer syntax 1.2.840.10008.1.2.4.51, which carries eight or
/// twelve bit samples. The stream differs from a baseline one only in its sample precision, so
/// it is read by the same decoder. Writing this syntax is not offered: frames are compressed
/// as baseline JPEG instead.
/// </summary>
public sealed class JpegExtendedCodec : IPixelCodec
{
    public string TransferSyntaxUid => "1.2.840.10008.1.2.4.51";

    public string Name => "JPEG Extended (Process 2 & 4)";

    public bool CanDecode => true;

    public bool CanEncode => false;

    public DecodedFrame Decode(IReadOnlyList<byte[]> fragments, FrameFormat format)
    {
        JpegFrameSamples samples = JpegBaselineDecoder.Decode(Codec.Concatenate(fragments));

        samples.ToRgb();

        string photometric = samples.Components == 3 ? "RGB" : format.PhotometricInterpretation;

        return new DecodedFrame(samples.ToFrameBytes(format), photometric);
    }

    public string DecodedPhotometricInterpretation(FrameFormat format) =>
        format.SamplesPerPixel == 3 ? "RGB" : format.PhotometricInterpretation;

    public EncodedFrame Encode(ReadOnlySpan<byte> frame, FrameFormat format, int quality) =>
        throw new CodecException(
            "Extended sequential JPEG cannot be written by this library; "
            + "compress as 1.2.840.10008.1.2.4.50 instead.");
}
