namespace Dicom.Codecs;

/// <summary>
/// How the samples of one frame are laid out when they are not compressed. A codec reads
/// this to know what it is compressing, and writes frames back in the same layout.
/// </summary>
public sealed class FrameFormat
{
    public FrameFormat(
        int rows,
        int columns,
        int samplesPerPixel,
        int bitsAllocated,
        int bitsStored,
        bool isSigned,
        int planarConfiguration = 0,
        string photometricInterpretation = "MONOCHROME2")
    {
        Rows = rows;
        Columns = columns;
        SamplesPerPixel = samplesPerPixel;
        BitsAllocated = bitsAllocated;
        BitsStored = bitsStored;
        IsSigned = isSigned;
        PlanarConfiguration = planarConfiguration;
        PhotometricInterpretation = photometricInterpretation;
    }

    /// <summary>The number of rows in the frame.</summary>
    public int Rows { get; }

    /// <summary>The number of columns in the frame.</summary>
    public int Columns { get; }

    /// <summary>The number of samples at each pixel.</summary>
    public int SamplesPerPixel { get; }

    /// <summary>The number of bits each sample occupies when stored uncompressed.</summary>
    public int BitsAllocated { get; }

    /// <summary>The number of bits of each sample that carry data.</summary>
    public int BitsStored { get; }

    /// <summary>Whether stored values are two's complement signed integers.</summary>
    public bool IsSigned { get; }

    /// <summary>Zero when colour samples are interleaved per pixel, one when stored plane by plane.</summary>
    public int PlanarConfiguration { get; }

    /// <summary>How the samples are to be interpreted, such as <c>MONOCHROME2</c> or <c>RGB</c>.</summary>
    public string PhotometricInterpretation { get; }

    /// <summary>The number of bytes each sample occupies uncompressed.</summary>
    public int BytesPerSample => Math.Max(BitsAllocated, 8) / 8;

    /// <summary>The number of samples in the frame.</summary>
    public int SampleCount => Rows * Columns * SamplesPerPixel;

    /// <summary>The number of bytes the frame occupies uncompressed.</summary>
    public int FrameLength => SampleCount * BytesPerSample;

    /// <summary>The same layout with a different photometric interpretation.</summary>
    public FrameFormat With(string photometricInterpretation) => new(
        Rows,
        Columns,
        SamplesPerPixel,
        BitsAllocated,
        BitsStored,
        IsSigned,
        PlanarConfiguration,
        photometricInterpretation);

    public override string ToString() =>
        $"{Columns}x{Rows} {BitsStored}/{BitsAllocated}-bit {PhotometricInterpretation}";
}

/// <summary>
/// The outcome of compressing one frame: the fragment to store, and how the data set must
/// describe it afterwards.
/// </summary>
public sealed class EncodedFrame
{
    public EncodedFrame(byte[] fragment, string photometricInterpretation, bool lossy)
    {
        Fragment = fragment;
        PhotometricInterpretation = photometricInterpretation;
        Lossy = lossy;
    }

    /// <summary>The compressed bytes of the frame.</summary>
    public byte[] Fragment { get; }

    /// <summary>The photometric interpretation the compressed samples carry.</summary>
    public string PhotometricInterpretation { get; }

    /// <summary>Whether the compression discarded information.</summary>
    public bool Lossy { get; }
}

/// <summary>
/// The outcome of decompressing one frame: the uncompressed samples, and how a data set must
/// describe them once they are stored that way.
/// </summary>
public sealed class DecodedFrame
{
    public DecodedFrame(byte[] bytes, string photometricInterpretation, int planarConfiguration = 0)
    {
        Bytes = bytes;
        PhotometricInterpretation = photometricInterpretation;
        PlanarConfiguration = planarConfiguration;
    }

    /// <summary>The uncompressed samples of the frame, interleaved by pixel.</summary>
    public byte[] Bytes { get; }

    /// <summary>The photometric interpretation the uncompressed samples carry.</summary>
    public string PhotometricInterpretation { get; }

    /// <summary>The planar configuration the uncompressed samples are in.</summary>
    public int PlanarConfiguration { get; }
}

/// <summary>
/// Raised when compressed pixel data cannot be read or written.
/// </summary>
public sealed class CodecException : Exception
{
    public CodecException(string message)
        : base(message)
    {
    }

    public CodecException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
