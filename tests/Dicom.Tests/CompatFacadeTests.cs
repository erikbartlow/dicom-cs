using Dicom.Codecs;
using Dicom.Object;
using Dicom.PixelData;
using DicomObjects;
using DicomObjects.Validation;

namespace Dicom.Tests;

public class CompatFacadeTests
{
    [Fact]
    public void WritesAndReadsThroughTheDataSetFacade()
    {
        DicomDataSet source = new();
        source.Add(0x0010, 0x0010, "Doe^Jane");
        source.Add(0x0008, 0x0060, "CR");
        source.Add(0x0028, 0x0010, (ushort)512);
        source.Add(0x0028, 0x1053, 2.5);
        source.SOPClass = UIDs.SOPClasses.ComputedRadiography;

        source.Write(out byte[] contents, UIDs.TransferSyntaxes.ExplicitVRLittleEndian, 100);

        DicomDataSet read = new(contents);

        Assert.Equal("Doe^Jane", read[0x0010, 0x0010].Value);
        Assert.Equal("PN", read[0x0010, 0x0010].VR);
        Assert.Equal(1, read[0x0010, 0x0010].VM);
        Assert.False(read[0x0010, 0x0010].IsNull);
        Assert.Equal((ushort)512, read.Value(0x0028, 0x0010));
        Assert.Equal("2.5", read[0x0028, 0x1053].Value);
        Assert.Equal(UIDs.SOPClasses.ComputedRadiography.Value, read.SOPClass);
        Assert.Equal(UIDs.TransferSyntaxes.ExplicitVRLittleEndian, read.OriginalTS);
    }

    [Fact]
    public void ReportsAbsentAttributesAsNull()
    {
        DicomDataSet dataSet = new();

        Assert.True(dataSet[0x0010, 0x0020].IsNull);
        Assert.Null(dataSet[0x0010, 0x0020].Value);
        Assert.Null(dataSet.Value(0x0010, 0x0020));
    }

    [Fact]
    public void EnumeratesAttributesAndRemovesThem()
    {
        DicomDataSet dataSet = new();
        dataSet.Add(0x0010, 0x0010, "Doe^Jane");
        dataSet.Add(0x0010, 0x0020, "ID-1");

        DicomAttribute? found = dataSet.FirstOrDefault(a => a.Group == 0x0010 && a.Element == 0x0020);

        Assert.NotNull(found);
        Assert.Equal("ID-1", found.Value);

        dataSet.Remove(0x0010, 0x0020);

        Assert.Equal(1, dataSet.Count);
        Assert.True(dataSet[0x0010, 0x0020].IsNull);
    }

    [Fact]
    public void StoresAndReadsBackSequences()
    {
        DicomDataSet item = new();
        item.Add(0x0008, 0x1150, "UI", "1.2.840.10008.5.1.4.1.1.2");

        DicomDataSetCollection sequence = [item];

        DicomDataSet source = new();
        source.Add(0x0010, 0x0010, "Doe^Jane");
        source.Add(0x0008, 0x1140, sequence);
        source.Write(out byte[] contents, UIDs.TransferSyntaxes.ExplicitVRLittleEndian, 100);

        DicomDataSet read = new(contents);
        DicomDataSetCollection items = Assert.IsType<DicomDataSetCollection>(read[0x0008, 0x1140].Value);

        Assert.Single(items);
        Assert.Equal("1.2.840.10008.5.1.4.1.1.2", items[0][0x0008, 0x1150].Value);
    }

    [Fact]
    public void HonoursTheDontReadPixelsBehaviour()
    {
        DicomDataSet source = new(Encoded());
        Assert.NotNull(source.RawPixels);

        source.Write(out byte[] contents, UIDs.TransferSyntaxes.ExplicitVRLittleEndian, 100);

        DicomDataSet read = new();
        read.Read(contents, new ReadBehaviours { IgnoreBOT = true, DontReadPixels = true });

        Assert.Null(read.RawPixels);
        Assert.Equal("Doe^Jane", read[0x0010, 0x0010].Value);
    }

    [Fact]
    public void ExposesFramePixelValuesThroughTheImageFacade()
    {
        using DicomImage image = new(Encoded());

        Assert.Equal(1, image.FrameCount);
        Assert.Equal(2, image.Size.Width);
        Assert.Equal(2, image.Size.Height);

        Array values = image.RawPixelValuesByFrame(1);

        Assert.Equal<ushort[]>([0, 1000, 2000, 4095], Assert.IsType<ushort[]>(values));
    }

    [Fact]
    public void ReplacesPixelDataThroughTheImageFacade()
    {
        using DicomImage image = new(Encoded());

        image.Pixels = new ushort[] { 1, 2, 3, 4 };

        Assert.Equal<ushort[]>([1, 2, 3, 4], Assert.IsType<ushort[]>(image.RawPixelValuesByFrame(1)));
    }

    [Fact]
    public void RecognisesDicomContent()
    {
        Assert.True(DicomDataSet.IsDicom(Encoded()));
        Assert.False(DicomDataSet.IsDicom([1, 2, 3]));
    }

    [Fact]
    public void AddsPrivateAttributesToTheDictionary()
    {
        DicomGlobal.AddDictionaryElement(0x7BF7, 0x1201, "FL", "1", "MMI Downsize Factor");

        DicomDataSet source = new();
        source.Add(0x7BF7, 0x1201, 0.5f);
        source.Write(out byte[] contents, UIDs.TransferSyntaxes.ImplicitVRLittleEndian, 100);

        DicomDataSet read = new(contents);

        Assert.Equal("FL", read[0x7BF7, 0x1201].VR);
        Assert.Equal(0.5f, read[0x7BF7, 0x1201].Value);
    }

    /// <summary>
    /// MMDxWebServices analyses several studies at once in one process, and registers its private
    /// attributes on the first request, while other requests are already reading data sets. So
    /// reading, decoding, modifying and writing has to give the same answer on eight threads as on
    /// one, with attributes being registered throughout.
    /// </summary>
    [Fact]
    public async Task AnalysesTheSameWayOnEightThreadsAsOnOne()
    {
        byte[][] sources = ConcurrencySources();
        string[] expected = [.. sources.Select(Analyse)];
        string[][] results = new string[8][];

        using CancellationTokenSource registering = new();

        Task registrations = Task.Run(() =>
        {
            for (int i = 0; !registering.IsCancellationRequested; i++)
            {
                int element = 0x1000 + (i % 64);

                DicomGlobal.AddDictionaryElement(0x7BF9, element, "LO", "1", $"MMI Concurrency {element:X4}");
                DicomGlobal.SetRegWord("ForceRGB", i % 2);
            }
        });

        Parallel.For(0, results.Length, thread =>
        {
            string[] digests = [];

            for (int iteration = 0; iteration < 50; iteration++)
                digests = [.. sources.Select(Analyse)];

            results[thread] = digests;
        });

        await registering.CancelAsync();
        await registrations;

        foreach (string[] digests in results)
            Assert.Equal(expected, digests);
    }

    /// <summary>
    /// The data sets the concurrency test works on: one stored natively, one whose pixel data has
    /// to be decoded, and one colour study of several frames.
    /// </summary>
    private static byte[][] ConcurrencySources()
    {
        using DicomImage gray = GrayImage();
        using DicomImage colour = ColourImage(frames: 3);

        gray.Write(out byte[] native, UIDs.TransferSyntaxes.ExplicitVRLittleEndian, 100);
        gray.Write(out byte[] compressed, UIDs.TransferSyntaxes.JPEGLosslessFirstOrder, 100);
        colour.Write(out byte[] baseline, UIDs.TransferSyntaxes.JPEGBaseline, 100);

        return [native, compressed, baseline];
    }

    /// <summary>
    /// Reads a data set, decodes its first frame, changes an attribute and writes it back, as one
    /// analysis does. The answer covers the decoded samples, the samples read back from what was
    /// written, and the attribute that was changed, so that it does not depend on the generated
    /// identifiers a write produces.
    /// </summary>
    private static string Analyse(byte[] contents)
    {
        using DicomImage image = new(contents);
        Array pixels = image.RawPixelValuesByFrame(1);

        image.Add(0x0008, 0x103E, "Concurrency");
        image.Write(out byte[] written, UIDs.TransferSyntaxes.JPEGLosslessFirstOrder, 100);

        using DicomImage reread = new(written);

        return $"{Digest(pixels)}:{Digest(reread.RawPixelValuesByFrame(1))}:{reread[0x0008, 0x103E].Value}";
    }

    private static string Digest(Array samples)
    {
        byte[] bytes = new byte[Buffer.ByteLength(samples)];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);

        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }

    [Fact]
    public void WritesColourFramesAsBaselineJpeg()
    {
        using DicomImage image = ColourImage(frames: 3);

        image.Write(out byte[] contents, UIDs.TransferSyntaxes.JPEGBaseline, 100);

        DicomDataSet read = new(contents);

        Assert.Equal(UIDs.TransferSyntaxes.JPEGBaseline, read.OriginalTS);
        Assert.Equal("YBR_FULL_422", read[0x0028, 0x0004].Value);
        Assert.Equal("01", read[0x0028, 0x2110].Value);
        Assert.Equal("ISO_10918_1", read[0x0028, 0x2114].Value);

        Core.PixelSequenceDicomValue pixels =
            Assert.IsType<Core.PixelSequenceDicomValue>(read.Data.GetElement(Core.Tags.PixelData)!.Value);

        Assert.Equal(3, pixels.Fragments.Count);
        Assert.Equal(3, pixels.OffsetTable.Count);

        using DicomImage reread = new(contents);
        byte[] samples = Assert.IsType<byte[]>(reread.RawPixelValuesByFrame(2));

        Assert.Equal(8 * 8 * 3, samples.Length);
        Assert.Equal("RGB", NativePixelData.From(reread.Data, reread.OriginalTS).Description.PhotometricInterpretation);
    }

    [Fact]
    public void WritingNativelyDecodesCompressedPixelDataFirst()
    {
        using DicomImage source = GrayImage();

        source.Write(out byte[] compressed, UIDs.TransferSyntaxes.JPEGLosslessFirstOrder, 100);

        using DicomImage read = new(compressed);
        ushort[] expected = Assert.IsType<ushort[]>(read.RawPixelValuesByFrame(1));

        read.Write(out byte[] native, UIDs.TransferSyntaxes.ExplicitVRLittleEndian, 100);

        using DicomImage reread = new(native);

        Assert.Equal(UIDs.TransferSyntaxes.ExplicitVRLittleEndian, reread.OriginalTS);
        Assert.IsType<Core.PrimitiveDicomValue>(reread.Data.GetElement(Core.Tags.PixelData)!.Value);
        Assert.Equal<ushort[]>(expected, Assert.IsType<ushort[]>(reread.RawPixelValuesByFrame(1)));
    }

    [Fact]
    public void RefusesToWriteCompressedPixelDataItCannotDecode()
    {
        DicomDataSet source = new(Encoded());
        source.Data.Put(new Core.DataElement(
            Core.Tags.PixelData,
            Core.Vr.OB,
            Core.DicomValue.OfFragments([[1, 2, 3, 4]])));
        source.OverrideTS = UIDs.TransferSyntaxes.JPEG2000Lossless;

        Assert.Throws<CodecException>(() =>
            source.Write(out byte[] _, UIDs.TransferSyntaxes.ExplicitVRLittleEndian, 100));
    }

    [Fact]
    public void ReturnsDatesTimesAndDateTimesAsDateTime()
    {
        DicomDataSet dataSet = new();
        dataSet.Add(0x0008, 0x0020, "DA", "20240115");
        dataSet.Add(0x0008, 0x0030, "TM", "080000.0000");
        dataSet.Add(0x0008, 0x002A, "DT", "20240115131415");
        dataSet.Add(0x0040, 0xA121, "DA", new[] { "20240115", "20240116" });
        dataSet.Add(0x0008, 0x0023, "DA", string.Empty);

        Assert.Equal(new DateTime(2024, 1, 15), dataSet.Value(0x0008, 0x0020));
        Assert.Equal(DateTime.MinValue.AddHours(8), dataSet[0x0008, 0x0030].Value);
        Assert.Equal(new DateTime(2024, 1, 15, 13, 14, 15), dataSet.Value(0x0008, 0x002A));
        Assert.Equal(
            new[] { new DateTime(2024, 1, 15), new DateTime(2024, 1, 16) },
            Assert.IsType<DateTime[]>(dataSet.Value(0x0040, 0xA121)));
        Assert.Null(dataSet.Value(0x0008, 0x0023));
    }

    [Fact]
    public void ExposesTheMembersTheAnalysisCompilesAgainst()
    {
        using DicomImage image = new(Encoded());

        image.ValidationOptions = ValidationOptions.None;

        Assert.Equal("Doe^Jane", image[0x0010, 0x0010].Value);
        Assert.IsAssignableFrom<DicomDataSet>(image);
        Assert.Same(image, image.DataSet);
        Assert.Equal(ValidationOptions.None, image.ValidationOptions);
        Assert.Equal("1.2.840.10008.5.1.4.1.1.7.4", UIDs.SOPClasses.MultiFrameTrueColorSC.Value);
        Assert.Equal("1.2.840.10008.5.1.4.1.1.104.1", UIDs.SOPClasses.EncapsulatedPDF.Value);
        Assert.True(new ReadBehaviours { CloseFilesAfterReading = true }.CloseFilesAfterReading);

        DicomDataSet dataSet = new();
        dataSet.Add(0x0020, 0x0037, "DS", new[] { "1.0", "0.0", "0.0", "0.0", "1.0", "0.0" });

        Assert.True(dataSet[0x0020, 0x0037].IsMultiple);
        Assert.False(dataSet[0x0010, 0x0010].IsMultiple);
        Assert.Equal("Image Orientation (Patient)", dataSet[0x0020, 0x0037].Description);
    }

    /// <summary>
    /// The storage classes are identifiers rather than strings, so a caller can read the value,
    /// compare one with a string, and store one wherever a value is taken loosely.
    /// </summary>
    [Fact]
    public void TreatsStorageClassesAsIdentifiersAndAsStrings()
    {
        DicomDataSet dataSet = new();
        dataSet.Add(0x0008, 0x0016, UIDs.SOPClasses.SecondaryCapture);

        string sopClass = UIDs.SOPClasses.SecondaryCapture;

        Assert.Equal("1.2.840.10008.5.1.4.1.1.7", dataSet[0x0008, 0x0016].Value);
        Assert.Equal("1.2.840.10008.5.1.4.1.1.7", sopClass);
        Assert.Equal("1.2.840.10008.5.1.4.1.1.7", UIDs.SOPClasses.SecondaryCapture.Value);
        Assert.True(sopClass == UIDs.SOPClasses.SecondaryCapture);
        Assert.Same(UIDs.SOPClasses.MultiFrameTrueColorSecondaryCapture, UIDs.SOPClasses.MultiFrameTrueColorSC);
    }

    /// <summary>A small grayscale image of the kind the analysis reads.</summary>
    private static DicomImage GrayImage()
    {
        DicomImage image = new();

        image.Add(0x0028, 0x0010, (ushort)16);
        image.Add(0x0028, 0x0011, (ushort)16);
        image.Add(0x0028, 0x0002, (ushort)1);
        image.Add(0x0028, 0x0004, "MONOCHROME2");
        image.Add(0x0028, 0x0100, (ushort)16);
        image.Add(0x0028, 0x0101, (ushort)16);
        image.Add(0x0028, 0x0102, (ushort)15);
        image.Add(0x0028, 0x0103, (ushort)0);

        ushort[] samples = new ushort[16 * 16];

        for (int i = 0; i < samples.Length; i++)
            samples[i] = (ushort)((i * 211) % 4096);

        image.Pixels = samples;

        return image;
    }

    /// <summary>A small true colour image of the kind the reports are built from.</summary>
    private static DicomImage ColourImage(int frames)
    {
        DicomImage image = new();

        image.SOPClass = UIDs.SOPClasses.MultiFrameTrueColorSC;
        image.Add(0x0028, 0x0010, (ushort)8);
        image.Add(0x0028, 0x0011, (ushort)8);
        image.Add(0x0028, 0x0002, (ushort)3);
        image.Add(0x0028, 0x0004, "RGB");
        image.Add(0x0028, 0x0006, (ushort)0);
        image.Add(0x0028, 0x0100, (ushort)8);
        image.Add(0x0028, 0x0101, (ushort)8);
        image.Add(0x0028, 0x0102, (ushort)7);
        image.Add(0x0028, 0x0103, (ushort)0);
        image.Add(0x0028, 0x0008, "IS", frames.ToString());

        byte[] pixels = new byte[8 * 8 * 3 * frames];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = (byte)(i % 251);

        image.RawPixels = pixels;

        return image;
    }

    private static byte[] Encoded()
    {
        InMemDicomObject data = RoundTripTests.Sample();
        data.Remove(new Core.Tag(0x0020, 0x0037));
        data.Put(new Core.DataElement(Core.Tags.Rows, Core.Vr.US, Core.PrimitiveValue.FromUInt16([2])));
        data.Put(new Core.DataElement(Core.Tags.Columns, Core.Vr.US, Core.PrimitiveValue.FromUInt16([2])));
        data.Put(new Core.DataElement(Core.Tags.SamplesPerPixel, Core.Vr.US, Core.PrimitiveValue.FromUInt16([1])));
        data.Put(new Core.DataElement(Core.Tags.BitsAllocated, Core.Vr.US, Core.PrimitiveValue.FromUInt16([16])));
        data.Put(new Core.DataElement(Core.Tags.BitsStored, Core.Vr.US, Core.PrimitiveValue.FromUInt16([16])));
        data.Put(new Core.DataElement(Core.Tags.HighBit, Core.Vr.US, Core.PrimitiveValue.FromUInt16([15])));
        data.Put(new Core.DataElement(Core.Tags.PixelRepresentation, Core.Vr.US, Core.PrimitiveValue.FromUInt16([0])));
        data.Put(new Core.DataElement(
            Core.Tags.PixelData,
            Core.Vr.OW,
            Core.PrimitiveValue.FromUInt16([0, 1000, 2000, 4095])));

        using MemoryStream stream = new();
        DicomFile.ToFile(data, Encoding.TransferSyntaxRegistry.ExplicitVrLittleEndian).Save(stream);

        return stream.ToArray();
    }
}
