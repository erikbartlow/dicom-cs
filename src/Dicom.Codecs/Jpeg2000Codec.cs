using CoreJ2K;
using CoreJ2K.Util;

namespace Dicom.Codecs;

/// <summary>
/// JPEG 2000, as transfer syntaxes 1.2.840.10008.1.2.4.90 and .91 carry it. Colour frames the
/// data set describes as YBR_RCT or YBR_ICT come back as red, green and blue, because the
/// inverse component transform the codestream states is part of decoding it. Writing is not
/// supported: SpineCAMP writes lossless JPEG and baseline JPEG only.
/// </summary>
public sealed class Jpeg2000Codec : IPixelCodec
{
    public Jpeg2000Codec(string transferSyntaxUid, string name)
    {
        TransferSyntaxUid = transferSyntaxUid;
        Name = name;
    }

    public string TransferSyntaxUid { get; }

    public string Name { get; }

    public bool CanDecode => true;

    public bool CanEncode => false;

    public DecodedFrame Decode(IReadOnlyList<byte[]> fragments, FrameFormat format)
    {
        using InterleavedImage image = Read(Codec.Concatenate(fragments));

        if (image.Width != format.Columns
            || image.Height != format.Rows
            || image.NumberOfComponents != format.SamplesPerPixel)
        {
            throw new CodecException(
                $"The compressed frame is {image.Width}x{image.Height}x{image.NumberOfComponents}, but "
                + $"the data set describes {format.Columns}x{format.Rows}x{format.SamplesPerPixel}.");
        }

        return new DecodedFrame(
            ToFrameBytes(image, format),
            DecodedPhotometricInterpretation(format),
            format.PlanarConfiguration);
    }

    public string DecodedPhotometricInterpretation(FrameFormat format) =>
        format.SamplesPerPixel == 3 ? "RGB" : format.PhotometricInterpretation;

    public EncodedFrame Encode(ReadOnlySpan<byte> frame, FrameFormat format, int quality) =>
        throw new CodecException($"This library cannot compress pixel data as {Name}.");

    private static InterleavedImage Read(byte[] codestream)
    {
        try
        {
            return J2kImage.FromBytes(codestream);
        }
        catch (Exception ex) when (ex is not CodecException)
        {
            throw new CodecException($"The JPEG 2000 frame could not be decoded: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The decoded components written out as uncompressed pixel data of the given layout, in
    /// little endian order and in the sample order the data set describes.
    /// </summary>
    private static byte[] ToFrameBytes(InterleavedImage image, FrameFormat format)
    {
        byte[] bytes = new byte[format.FrameLength];
        int pixels = format.Rows * format.Columns;
        int[] samples = new int[pixels];
        bool planar = format.SamplesPerPixel > 1 && format.PlanarConfiguration == 1;

        for (int component = 0; component < format.SamplesPerPixel; component++)
        {
            image.CopyComponentTo(component, samples);

            // The decoder adds half the range to every sample, so that what it returns is
            // unsigned whatever the codestream holds. Samples the data set states are signed
            // are put back; the data set is what says so, not the codestream, whose own flag
            // some encoders leave unset.
            int shift = format.IsSigned ? 1 << (image.GetBitDepth(component) - 1) : 0;
            int first = (planar ? component * pixels : component) * format.BytesPerSample;
            int stride = (planar ? 1 : format.SamplesPerPixel) * format.BytesPerSample;

            for (int i = 0; i < pixels; i++)
            {
                int value = samples[i] - shift;
                int at = first + (i * stride);

                bytes[at] = (byte)value;

                if (format.BytesPerSample == 2)
                    bytes[at + 1] = (byte)(value >> 8);
            }
        }

        return bytes;
    }
}
