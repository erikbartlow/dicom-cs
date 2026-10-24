using Dicom.Codecs.Jpeg;

namespace Dicom.Codecs;

/// <summary>
/// Baseline JPEG, transfer syntax 1.2.840.10008.1.2.4.50. Colour frames are compressed as
/// luminance and chrominance with the chrominance halved horizontally, which the data set
/// then describes as YBR_FULL_422; decoded colour frames come back as red, green and blue.
/// </summary>
public sealed class JpegBaselineCodec : IPixelCodec
{
    /// <summary>The photometric interpretation a compressed colour frame carries.</summary>
    public const string CompressedColour = "YBR_FULL_422";

    public string TransferSyntaxUid => "1.2.840.10008.1.2.4.50";

    public string Name => "JPEG Baseline (Process 1)";

    public bool CanDecode => true;

    public bool CanEncode => true;

    public DecodedFrame Decode(IReadOnlyList<byte[]> fragments, FrameFormat format)
    {
        JpegFrameSamples samples = JpegBaselineDecoder.Decode(Codec.Concatenate(fragments));

        samples.ToRgb();

        string photometric = samples.Components == 3 ? "RGB" : format.PhotometricInterpretation;

        return new DecodedFrame(samples.ToFrameBytes(format), photometric);
    }

    public string DecodedPhotometricInterpretation(FrameFormat format) =>
        format.SamplesPerPixel == 3 ? "RGB" : format.PhotometricInterpretation;

    public EncodedFrame Encode(ReadOnlySpan<byte> frame, FrameFormat format, int quality)
    {
        byte[] fragment = JpegBaselineEncoder.Encode(frame, format, quality);
        string photometric = format.SamplesPerPixel == 3
            ? CompressedColour
            : format.PhotometricInterpretation;

        return new EncodedFrame(fragment, photometric, lossy: true);
    }
}
