using Dicom.Codecs;
using Dicom.Core;
using Dicom.Object;

namespace Dicom.PixelData;

/// <summary>
/// Moves the pixel data of a data set between transfer syntaxes, decompressing it to store it
/// uncompressed and compressing it to store it in fragments. The attributes that describe the
/// samples are brought into line with what is stored.
/// </summary>
public static class PixelDataTranscoder
{
    private static readonly Tag LossyImageCompression = new(0x0028, 0x2110);

    private static readonly Tag LossyImageCompressionRatio = new(0x0028, 0x2112);

    private static readonly Tag LossyImageCompressionMethod = new(0x0028, 0x2114);

    /// <summary>
    /// Rewrites the pixel data so that it can be stored in the target transfer syntax, which
    /// does nothing when it is already stored that way. Reports whether anything changed.
    /// </summary>
    public static bool ToTransferSyntax(
        InMemDicomObject dataSet,
        string? currentTransferSyntaxUid,
        string targetTransferSyntaxUid,
        bool targetIsEncapsulated,
        int quality = 100)
    {
        DataElement? element = dataSet.GetElement(Tags.PixelData);

        if (element is null || element.IsEmpty)
            return false;

        bool storedInFragments = element.Value is PixelSequenceDicomValue;

        if (storedInFragments == targetIsEncapsulated
            && (!storedInFragments || IsSame(currentTransferSyntaxUid, targetTransferSyntaxUid)))
            return false;

        byte[][] frames = Frames(dataSet, currentTransferSyntaxUid, out PixelDataDescription description);

        if (!targetIsEncapsulated)
        {
            Store(dataSet, description, frames);
            return true;
        }

        Compress(dataSet, description, frames, targetTransferSyntaxUid, quality);

        return true;
    }

    /// <summary>The uncompressed bytes of every frame.</summary>
    private static byte[][] Frames(
        InMemDicomObject dataSet,
        string? currentTransferSyntaxUid,
        out PixelDataDescription description)
    {
        NativePixelData pixels = NativePixelData.From(dataSet, currentTransferSyntaxUid);
        description = pixels.Description;
        byte[][] frames = new byte[pixels.FrameCount][];

        for (int frame = 0; frame < frames.Length; frame++)
            frames[frame] = pixels.GetFrameBytes(frame).ToArray();

        return frames;
    }

    /// <summary>Stores the frames uncompressed, one after another.</summary>
    private static void Store(InMemDicomObject dataSet, PixelDataDescription description, byte[][] frames)
    {
        byte[] bytes = new byte[frames.Sum(static frame => frame.Length)];
        int offset = 0;

        foreach (byte[] frame in frames)
        {
            frame.CopyTo(bytes, offset);
            offset += frame.Length;
        }

        Vr vr = description.BitsAllocated > 8 ? Vr.OW : Vr.OB;

        dataSet.Put(new DataElement(Tags.PixelData, vr, PrimitiveValue.FromBytes(bytes)));
        Describe(dataSet, description);
    }

    /// <summary>Stores the frames compressed, one fragment per frame with an offset table.</summary>
    private static void Compress(
        InMemDicomObject dataSet,
        PixelDataDescription description,
        byte[][] frames,
        string targetTransferSyntaxUid,
        int quality)
    {
        IPixelCodec codec = PixelCodecRegistry.RequireEncoder(targetTransferSyntaxUid);
        FrameFormat format = description.ToFrameFormat();

        List<byte[]> fragments = [];
        List<uint> offsets = [];
        uint position = 0;
        string photometric = description.PhotometricInterpretation;
        bool lossy = false;
        long compressed = 0;

        foreach (byte[] frame in frames)
        {
            EncodedFrame encoded = codec.Encode(frame, format, quality);
            byte[] fragment = encoded.Fragment.Length % 2 == 0
                ? encoded.Fragment
                : [.. encoded.Fragment, (byte)0];

            offsets.Add(position);
            fragments.Add(fragment);

            position += (uint)(fragment.Length + 8);
            photometric = encoded.PhotometricInterpretation;
            lossy |= encoded.Lossy;
            compressed += fragment.Length;
        }

        dataSet.Put(new DataElement(Tags.PixelData, Vr.OB, DicomValue.OfFragments(fragments, offsets)));
        Describe(dataSet, description.With(photometric));

        if (!lossy)
            return;

        double ratio = compressed == 0 ? 1 : frames.Sum(static frame => (long)frame.Length) / (double)compressed;

        dataSet.Put(new DataElement(LossyImageCompression, Vr.CS, PrimitiveValue.FromString("01")));
        dataSet.Put(new DataElement(
            LossyImageCompressionRatio,
            Vr.DS,
            PrimitiveValue.FromString(ratio.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))));
        dataSet.Put(new DataElement(LossyImageCompressionMethod, Vr.CS, PrimitiveValue.FromString("ISO_10918_1")));
    }

    /// <summary>Brings the attributes that describe the samples into line with what is stored.</summary>
    private static void Describe(InMemDicomObject dataSet, PixelDataDescription description)
    {
        dataSet.Put(new DataElement(
            Tags.PhotometricInterpretation,
            Vr.CS,
            PrimitiveValue.FromString(description.PhotometricInterpretation)));

        if (description.SamplesPerPixel > 1)
        {
            dataSet.Put(new DataElement(
                Tags.PlanarConfiguration,
                Vr.US,
                PrimitiveValue.FromUInt16([(ushort)description.PlanarConfiguration])));
        }
    }

    private static bool IsSame(string? first, string? second) =>
        string.Equals(first?.Trim().TrimEnd('\0'), second?.Trim().TrimEnd('\0'), StringComparison.Ordinal);
}
