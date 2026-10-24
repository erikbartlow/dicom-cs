using Dicom.Core;

namespace DicomObjects;

/// <summary>
/// The dimensions of an image in pixels.
/// </summary>
public readonly struct ImageSize
{
    public ImageSize(int width, int height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>The number of columns.</summary>
    public int Width { get; }

    /// <summary>The number of rows.</summary>
    public int Height { get; }

    public override string ToString() => $"{Width}x{Height}";
}

/// <summary>
/// An image held as a DICOM data set, with access to its pixel data. An image is a data set,
/// so it can be used wherever one is expected.
/// </summary>
public partial class DicomImage : DicomDataSet
{
    public DicomImage()
    {
    }

    public DicomImage(byte[] contents)
        : base(contents)
    {
    }

    /// <summary>
    /// An image over the attributes of an existing data set. The two work on the same
    /// attributes, so a change through one is seen through the other.
    /// </summary>
    public DicomImage(DicomDataSet dataSet)
        : base(dataSet)
    {
    }

    /// <summary>The data set holding the image and its attributes, which is the image itself.</summary>
    public DicomDataSet DataSet => this;

    /// <summary>The identifier of the study the image belongs to.</summary>
    public string? StudyUID
    {
        get => DataSet.Data.GetString(StudyInstanceUid);
        set => DataSet.Add(0x0020, 0x000D, "UI", value);
    }

    /// <summary>The identifier of the series the image belongs to.</summary>
    public string? SeriesUID
    {
        get => DataSet.Data.GetString(SeriesInstanceUid);
        set => DataSet.Add(0x0020, 0x000E, "UI", value);
    }

    /// <summary>The identifier of the image itself.</summary>
    public string? SOPInstanceUID
    {
        get => DataSet.Data.GetString(Tags.SopInstanceUid);
        set => DataSet.Add(0x0008, 0x0018, "UI", value);
    }

    /// <summary>The number of frames the image holds.</summary>
    public int FrameCount => Math.Max(DataSet.Data.GetInt32(Tags.NumberOfFrames) ?? 1, 1);

    /// <summary>The dimensions of each frame.</summary>
    public ImageSize Size => new(
        DataSet.Data.GetInt32(Tags.Columns) ?? 0,
        DataSet.Data.GetInt32(Tags.Rows) ?? 0);

    private static readonly Tag StudyInstanceUid = new(0x0020, 0x000D);

    private static readonly Tag SeriesInstanceUid = new(0x0020, 0x000E);

    public override string ToString() => $"image {Size}, {FrameCount} frame(s)";
}
