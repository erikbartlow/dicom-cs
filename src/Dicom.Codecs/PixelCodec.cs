using System.Collections.Concurrent;

namespace Dicom.Codecs;

/// <summary>
/// Compression and decompression of the pixel data of one frame, for a single transfer syntax.
/// </summary>
public interface IPixelCodec
{
    /// <summary>The transfer syntax this codec handles.</summary>
    string TransferSyntaxUid { get; }

    /// <summary>The descriptive name of the compression.</summary>
    string Name { get; }

    /// <summary>Whether this codec can turn a fragment back into samples.</summary>
    bool CanDecode { get; }

    /// <summary>Whether this codec can turn samples into a fragment.</summary>
    bool CanEncode { get; }

    /// <summary>
    /// The uncompressed bytes of one frame, laid out as the format states. The fragments of a
    /// frame are given in order, so that codecs which split a frame across fragments see all of it.
    /// </summary>
    DecodedFrame Decode(IReadOnlyList<byte[]> fragments, FrameFormat format);

    /// <summary>
    /// The photometric interpretation decoded samples carry, which differs from the stored one
    /// when the compression holds the samples in another colour space.
    /// </summary>
    string DecodedPhotometricInterpretation(FrameFormat format);

    /// <summary>Compresses one frame of uncompressed samples.</summary>
    EncodedFrame Encode(ReadOnlySpan<byte> frame, FrameFormat format, int quality);
}

/// <summary>
/// The codecs available, keyed by transfer syntax. Callers can replace a codec or add one
/// for a syntax this library does not implement, which may happen while other threads are
/// looking codecs up. The codecs themselves keep no state between calls.
/// </summary>
public static class PixelCodecRegistry
{
    private static readonly ConcurrentDictionary<string, IPixelCodec> Codecs = Build();

    /// <summary>Every codec available.</summary>
    public static IReadOnlyCollection<IPixelCodec> All => [.. Codecs.Values];

    /// <summary>The codec for a transfer syntax, or null when there is none.</summary>
    public static IPixelCodec? Lookup(string? transferSyntaxUid) =>
        transferSyntaxUid is not null
        && Codecs.TryGetValue(transferSyntaxUid.Trim().TrimEnd('\0'), out IPixelCodec? codec)
            ? codec
            : null;

    /// <summary>The codec that can decode a transfer syntax, raising when there is none.</summary>
    public static IPixelCodec RequireDecoder(string? transferSyntaxUid)
    {
        IPixelCodec? codec = Lookup(transferSyntaxUid);

        if (codec is null || !codec.CanDecode)
            throw new CodecException(
                $"Pixel data compressed as '{transferSyntaxUid}' cannot be decoded by this library.");

        return codec;
    }

    /// <summary>The codec that can encode a transfer syntax, raising when there is none.</summary>
    public static IPixelCodec RequireEncoder(string? transferSyntaxUid)
    {
        IPixelCodec? codec = Lookup(transferSyntaxUid);

        if (codec is null || !codec.CanEncode)
            throw new CodecException(
                $"Pixel data cannot be compressed as '{transferSyntaxUid}' by this library.");

        return codec;
    }

    /// <summary>Adds or replaces the codec for a transfer syntax.</summary>
    public static void Register(IPixelCodec codec) => Codecs[codec.TransferSyntaxUid] = codec;

    private static ConcurrentDictionary<string, IPixelCodec> Build()
    {
        IPixelCodec[] codecs =
        [
            new JpegLosslessCodec("1.2.840.10008.1.2.4.57", "JPEG Lossless, Non-Hierarchical (Process 14)"),
            new JpegLosslessCodec("1.2.840.10008.1.2.4.70", "JPEG Lossless, Selection Value 1 (Process 14)"),
            new JpegBaselineCodec(),
            new JpegExtendedCodec(),
            new JpegLsCodec("1.2.840.10008.1.2.4.80", "JPEG-LS Lossless"),
            new JpegLsCodec("1.2.840.10008.1.2.4.81", "JPEG-LS Lossy (Near-Lossless)"),
            new Jpeg2000Codec("1.2.840.10008.1.2.4.90", "JPEG 2000 Image Compression (Lossless Only)"),
            new Jpeg2000Codec("1.2.840.10008.1.2.4.91", "JPEG 2000 Image Compression"),
            new RleCodec(),
        ];

        ConcurrentDictionary<string, IPixelCodec> map = new(StringComparer.Ordinal);

        foreach (IPixelCodec codec in codecs)
            map[codec.TransferSyntaxUid] = codec;

        return map;
    }
}
