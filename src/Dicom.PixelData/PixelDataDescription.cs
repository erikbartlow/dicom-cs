using Dicom.Codecs;
using Dicom.Core;
using Dicom.Object;

namespace Dicom.PixelData;

/// <summary>
/// The image pixel attributes that describe how the pixel data of a data set is laid out.
/// </summary>
public sealed class PixelDataDescription
{
    private PixelDataDescription(PixelDataDescription source, string photometricInterpretation)
    {
        Rows = source.Rows;
        Columns = source.Columns;
        SamplesPerPixel = source.SamplesPerPixel;
        NumberOfFrames = source.NumberOfFrames;
        BitsAllocated = source.BitsAllocated;
        BitsStored = source.BitsStored;
        HighBit = source.HighBit;
        IsSigned = source.IsSigned;
        PlanarConfiguration = source.PlanarConfiguration;
        PhotometricInterpretation = photometricInterpretation;
        RescaleSlope = source.RescaleSlope;
        RescaleIntercept = source.RescaleIntercept;
        WindowCenter = source.WindowCenter;
        WindowWidth = source.WindowWidth;
    }

    private PixelDataDescription(InMemDicomObject dataSet)
    {
        Rows = dataSet.GetInt32(Tags.Rows) ?? 0;
        Columns = dataSet.GetInt32(Tags.Columns) ?? 0;
        SamplesPerPixel = dataSet.GetInt32(Tags.SamplesPerPixel) ?? 1;
        NumberOfFrames = dataSet.GetInt32(Tags.NumberOfFrames) ?? 1;
        BitsAllocated = dataSet.GetInt32(Tags.BitsAllocated) ?? 0;
        BitsStored = dataSet.GetInt32(Tags.BitsStored) ?? BitsAllocated;
        HighBit = dataSet.GetInt32(Tags.HighBit) ?? Math.Max(BitsStored - 1, 0);
        IsSigned = (dataSet.GetInt32(Tags.PixelRepresentation) ?? 0) == 1;
        PlanarConfiguration = dataSet.GetInt32(Tags.PlanarConfiguration) ?? 0;
        PhotometricInterpretation = dataSet.GetString(Tags.PhotometricInterpretation)?.Trim() ?? "MONOCHROME2";
        RescaleSlope = dataSet.GetDouble(Tags.RescaleSlope) ?? 1.0;
        RescaleIntercept = dataSet.GetDouble(Tags.RescaleIntercept) ?? 0.0;
        WindowCenter = dataSet.GetDouble(Tags.WindowCenter);
        WindowWidth = dataSet.GetDouble(Tags.WindowWidth);
    }

    /// <summary>The number of rows in each frame.</summary>
    public int Rows { get; }

    /// <summary>The number of columns in each frame.</summary>
    public int Columns { get; }

    /// <summary>The number of samples at each pixel: one for grayscale, three for colour.</summary>
    public int SamplesPerPixel { get; }

    /// <summary>The number of frames the pixel data holds.</summary>
    public int NumberOfFrames { get; }

    /// <summary>The number of bits each sample occupies, which is a multiple of eight.</summary>
    public int BitsAllocated { get; }

    /// <summary>The number of bits of each sample that carry data.</summary>
    public int BitsStored { get; }

    /// <summary>The position of the most significant stored bit within the allocated bits.</summary>
    public int HighBit { get; }

    /// <summary>Whether stored values are two's complement signed integers.</summary>
    public bool IsSigned { get; }

    /// <summary>Zero when colour samples are interleaved per pixel, one when stored plane by plane.</summary>
    public int PlanarConfiguration { get; }

    /// <summary>How the samples are to be interpreted, such as <c>MONOCHROME2</c> or <c>RGB</c>.</summary>
    public string PhotometricInterpretation { get; }

    /// <summary>The slope applied to stored values to reach output units.</summary>
    public double RescaleSlope { get; }

    /// <summary>The intercept applied to stored values to reach output units.</summary>
    public double RescaleIntercept { get; }

    /// <summary>The centre of the display window, when the data set states one.</summary>
    public double? WindowCenter { get; }

    /// <summary>The width of the display window, when the data set states one.</summary>
    public double? WindowWidth { get; }

    /// <summary>Whether lower stored values are displayed brighter.</summary>
    public bool IsInverted =>
        PhotometricInterpretation.Equals("MONOCHROME1", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether each pixel holds a single grayscale sample.</summary>
    public bool IsGrayscale => SamplesPerPixel == 1;

    /// <summary>The number of bytes each sample occupies.</summary>
    public int BytesPerSample => Math.Max(BitsAllocated, 8) / 8;

    /// <summary>The number of samples in one frame.</summary>
    public int SamplesPerFrame => Rows * Columns * SamplesPerPixel;

    /// <summary>The number of bytes one frame occupies.</summary>
    public int FrameLength => SamplesPerFrame * BytesPerSample;

    /// <summary>Reads the image pixel attributes of a data set.</summary>
    public static PixelDataDescription From(InMemDicomObject dataSet)
    {
        PixelDataDescription description = new(dataSet);

        if (description.Rows <= 0 || description.Columns <= 0)
            throw new DicomValueException("The data set does not describe an image.");

        if (description.BitsAllocated is not (8 or 16 or 32))
            throw new NotSupportedException(
                $"{description.BitsAllocated} bits allocated per sample is not supported.");

        return description;
    }

    /// <summary>The same description with a different photometric interpretation.</summary>
    public PixelDataDescription With(string photometricInterpretation) =>
        new(this, photometricInterpretation);

    /// <summary>The layout of one frame, as the codecs describe it.</summary>
    public FrameFormat ToFrameFormat() => new(
        Rows,
        Columns,
        SamplesPerPixel,
        BitsAllocated,
        BitsStored,
        IsSigned,
        PlanarConfiguration,
        PhotometricInterpretation);

    public override string ToString() =>
        $"{Columns}x{Rows} {BitsStored}-bit {PhotometricInterpretation}, {NumberOfFrames} frame(s)";
}
