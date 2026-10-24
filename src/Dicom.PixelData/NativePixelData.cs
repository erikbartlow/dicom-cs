using Dicom.Codecs;
using Dicom.Core;
using Dicom.Object;

namespace Dicom.PixelData;

/// <summary>
/// Access to the samples of an image: the stored values of a frame, those values in output
/// units, and an eight bit rendering for display. Pixel data stored uncompressed is read as
/// it is; pixel data stored in fragments is decompressed one frame at a time by the codec
/// for its transfer syntax.
/// </summary>
public sealed class NativePixelData
{
    private readonly byte[] _bytes;
    private readonly IReadOnlyList<byte[]>[] _fragmentsByFrame;
    private readonly IPixelCodec? _codec;
    private readonly byte[]?[] _decoded;

    private NativePixelData(PixelDataDescription description, byte[] bytes)
    {
        Description = description;
        _bytes = bytes;
        _fragmentsByFrame = [];
        _decoded = [];
    }

    private NativePixelData(
        PixelDataDescription description,
        IReadOnlyList<byte[]>[] fragmentsByFrame,
        IPixelCodec codec)
    {
        Description = description;
        _bytes = [];
        _fragmentsByFrame = fragmentsByFrame;
        _codec = codec;
        _decoded = new byte[]?[fragmentsByFrame.Length];
    }

    /// <summary>How the pixel data is laid out once it is uncompressed.</summary>
    public PixelDataDescription Description { get; }

    /// <summary>The number of frames available.</summary>
    public int FrameCount => Description.NumberOfFrames;

    /// <summary>Whether the pixel data had to be decompressed to be read.</summary>
    public bool WasCompressed => _codec is not null;

    /// <summary>
    /// Reads the pixel data of a data set. Compressed pixel data needs the transfer syntax it
    /// was stored in, which is taken from the file meta information when the data set has it.
    /// </summary>
    public static NativePixelData From(InMemDicomObject dataSet, string? transferSyntaxUid = null)
    {
        DataElement element = dataSet.GetElement(Tags.PixelData)
            ?? throw new DicomValueException("The data set has no pixel data.");

        PixelDataDescription description = PixelDataDescription.From(dataSet);

        if (element.Value is not PixelSequenceDicomValue pixels)
            return new NativePixelData(description, element.Primitive.ToBytes());

        string? uid = transferSyntaxUid
            ?? (dataSet as FileDicomObject)?.Meta.TransferSyntaxUid;

        IPixelCodec codec = PixelCodecRegistry.RequireDecoder(uid);
        PixelDataDescription decoded = description.With(
            codec.DecodedPhotometricInterpretation(description.ToFrameFormat()));

        return new NativePixelData(decoded, Split(pixels, description.NumberOfFrames), codec);
    }

    /// <summary>The raw bytes of one frame, decompressing it first when it is compressed.</summary>
    public ReadOnlySpan<byte> GetFrameBytes(int frame)
    {
        if (frame < 0 || frame >= FrameCount)
            throw new ArgumentOutOfRangeException(nameof(frame));

        int length = Description.FrameLength;

        if (_codec is not null)
            return Decompress(frame).AsSpan(0, length);

        int offset = frame * length;

        if (offset + length > _bytes.Length)
            throw new DicomValueException("The pixel data is shorter than the image description implies.");

        return _bytes.AsSpan(offset, length);
    }

    private byte[] Decompress(int frame)
    {
        if (_decoded[frame] is { } cached)
            return cached;

        DecodedFrame result = _codec!.Decode(_fragmentsByFrame[frame], Description.ToFrameFormat());

        if (result.Bytes.Length < Description.FrameLength)
            throw new DicomValueException("The decompressed frame is shorter than the image description implies.");

        _decoded[frame] = result.Bytes;

        return result.Bytes;
    }

    /// <summary>
    /// The fragments belonging to each frame. One fragment per frame is the usual case; when
    /// there are more, the basic offset table says where each frame starts.
    /// </summary>
    private static IReadOnlyList<byte[]>[] Split(PixelSequenceDicomValue pixels, int frames)
    {
        if (pixels.Fragments.Count == 0)
            throw new DicomValueException("The pixel data holds no fragments.");

        if (frames <= 1)
            return [pixels.Fragments];

        if (pixels.Fragments.Count == frames)
            return [.. pixels.Fragments.Select(static fragment => (IReadOnlyList<byte[]>)[fragment])];

        if (pixels.OffsetTable.Count != frames)
            throw new DicomValueException(
                $"The pixel data has {pixels.Fragments.Count} fragments for {frames} frames and no usable offset table.");

        List<IReadOnlyList<byte[]>> byFrame = [];
        List<byte[]> current = [];
        uint position = 0;
        int next = 1;

        foreach (byte[] fragment in pixels.Fragments)
        {
            if (next < frames && position == pixels.OffsetTable[next])
            {
                byFrame.Add(current);
                current = [];
                next++;
            }

            current.Add(fragment);
            position += (uint)(fragment.Length + 8);
        }

        byFrame.Add(current);

        if (byFrame.Count != frames)
            throw new DicomValueException("The basic offset table does not agree with the fragments present.");

        return [.. byFrame];
    }

    /// <summary>
    /// The stored values of one frame, sign extended and masked to the stored bits.
    /// Colour samples are returned in the order they appear in the frame.
    /// </summary>
    public int[] GetFrameSamples(int frame)
    {
        ReadOnlySpan<byte> bytes = GetFrameBytes(frame);
        int count = Description.SamplesPerFrame;
        int[] samples = new int[count];

        int shift = Description.HighBit + 1 - Description.BitsStored;
        int mask = Description.BitsStored >= 32 ? -1 : (1 << Description.BitsStored) - 1;
        int signBit = 1 << (Description.BitsStored - 1);

        for (int i = 0; i < count; i++)
        {
            int raw = Description.BytesPerSample switch
            {
                1 => bytes[i],
                2 => BitConverter.ToUInt16(bytes.Slice(i * 2, 2)),
                _ => BitConverter.ToInt32(bytes.Slice(i * 4, 4)),
            };

            int value = shift > 0 ? raw >> shift : raw;
            value &= mask;

            if (Description.IsSigned && (value & signBit) != 0)
                value -= signBit << 1;

            samples[i] = value;
        }

        return samples;
    }

    /// <summary>The values of one frame in output units, after the rescale slope and intercept.</summary>
    public double[] GetFrameValues(int frame)
    {
        int[] samples = GetFrameSamples(frame);
        double[] values = new double[samples.Length];

        for (int i = 0; i < samples.Length; i++)
            values[i] = (samples[i] * Description.RescaleSlope) + Description.RescaleIntercept;

        return values;
    }

    /// <summary>
    /// An eight bit grayscale rendering of one frame, using the window stated by the data set
    /// or, when none is stated, the range of values actually present.
    /// </summary>
    public byte[] RenderGrayscale8(int frame, double? windowCenter = null, double? windowWidth = null)
    {
        if (!Description.IsGrayscale)
            throw new NotSupportedException("The pixel data is not grayscale.");

        double[] values = GetFrameValues(frame);

        double center = windowCenter ?? Description.WindowCenter ?? 0;
        double width = windowWidth ?? Description.WindowWidth ?? 0;

        if (width <= 0)
        {
            double low = values.Length == 0 ? 0 : values.Min();
            double high = values.Length == 0 ? 0 : values.Max();

            width = Math.Max(high - low, 1);
            center = low + (width / 2);
        }

        double lower = center - (width / 2);
        byte[] rendered = new byte[values.Length];

        for (int i = 0; i < values.Length; i++)
        {
            double scaled = (values[i] - lower) / width * 255.0;
            int level = (int)Math.Round(Math.Clamp(scaled, 0, 255));

            rendered[i] = (byte)(Description.IsInverted ? 255 - level : level);
        }

        return rendered;
    }
}
