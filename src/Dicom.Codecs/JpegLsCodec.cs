using CharLS.Managed;

namespace Dicom.Codecs;

/// <summary>
/// JPEG-LS, as transfer syntaxes 1.2.840.10008.1.2.4.80 and .81 carry it. The samples are
/// stored in the colour space the data set names, so decoding does not change it. Writing is
/// not supported: SpineCAMP writes lossless JPEG and baseline JPEG only.
/// </summary>
public sealed class JpegLsCodec : IPixelCodec
{
    public JpegLsCodec(string transferSyntaxUid, string name)
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
        byte[] codestream = Codec.Concatenate(fragments);

        try
        {
            JpegLSDecoder decoder = new(codestream, readHeader: true, tryReadSpiffHeader: false);
            FrameInfo frame = decoder.FrameInfo;

            if (frame.Width != format.Columns
                || frame.Height != format.Rows
                || frame.ComponentCount != format.SamplesPerPixel)
            {
                throw new CodecException(
                    $"The compressed frame is {frame.Width}x{frame.Height}x{frame.ComponentCount}, but "
                    + $"the data set describes {format.Columns}x{format.Rows}x{format.SamplesPerPixel}.");
            }

            int bytesPerSample = frame.BitsPerSample > 8 ? 2 : 1;

            if (bytesPerSample > format.BytesPerSample)
                throw new CodecException(
                    $"The compressed frame carries {frame.BitsPerSample} bit samples, which do not fit "
                    + $"the {format.BitsAllocated} bits the data set allocates.");

            byte[] decoded = decoder.Decode(stride: 0);

            return new DecodedFrame(
                ToFrameBytes(decoded, bytesPerSample, decoder.InterleaveMode, format),
                format.PhotometricInterpretation,
                format.PlanarConfiguration);
        }
        catch (Exception ex) when (ex is not CodecException)
        {
            throw new CodecException($"The JPEG-LS frame could not be decoded: {ex.Message}", ex);
        }
    }

    public string DecodedPhotometricInterpretation(FrameFormat format) => format.PhotometricInterpretation;

    public EncodedFrame Encode(ReadOnlySpan<byte> frame, FrameFormat format, int quality) =>
        throw new CodecException($"This library cannot compress pixel data as {Name}.");

    /// <summary>
    /// The decoded samples written out as uncompressed pixel data of the given layout, widening
    /// them where the data set allocates more bits than the stream carries, and putting the
    /// colour samples in the order the data set describes.
    /// </summary>
    private static byte[] ToFrameBytes(
        byte[] decoded,
        int bytesPerSample,
        InterleaveMode interleaveMode,
        FrameFormat format)
    {
        int pixels = format.Rows * format.Columns;
        int components = format.SamplesPerPixel;

        if (decoded.Length < pixels * components * bytesPerSample)
            throw new CodecException("The decoded frame is shorter than the image description implies.");

        // The decoder lays a frame out plane by plane when the stream is not interleaved, and
        // pixel by pixel otherwise. The data set states which of the two it is stored as.
        bool decodedIsPlanar = components > 1 && interleaveMode == InterleaveMode.None;
        bool wantedIsPlanar = components > 1 && format.PlanarConfiguration == 1;

        if (decodedIsPlanar == wantedIsPlanar && bytesPerSample == format.BytesPerSample)
        {
            if (decoded.Length == format.FrameLength)
                return decoded;

            byte[] exact = new byte[format.FrameLength];
            Array.Copy(decoded, exact, format.FrameLength);

            return exact;
        }

        byte[] bytes = new byte[format.FrameLength];

        for (int component = 0; component < components; component++)
        {
            int from = decodedIsPlanar ? component * pixels : component;
            int fromStride = decodedIsPlanar ? 1 : components;
            int to = wantedIsPlanar ? component * pixels : component;
            int toStride = wantedIsPlanar ? 1 : components;

            for (int i = 0; i < pixels; i++)
            {
                int source = (from + (i * fromStride)) * bytesPerSample;
                int target = (to + (i * toStride)) * format.BytesPerSample;

                bytes[target] = decoded[source];

                if (bytesPerSample == 2)
                    bytes[target + 1] = decoded[source + 1];
            }
        }

        return bytes;
    }
}
