using Dicom.Core;
using Dicom.Encoding;
using Dicom.Object;
using Dicom.PixelData;

namespace Dicom.Tests;

public class RoundTripTests
{
    public static TheoryData<TransferSyntax> Syntaxes =>
    [
        TransferSyntaxRegistry.ImplicitVrLittleEndian,
        TransferSyntaxRegistry.ExplicitVrLittleEndian,
        TransferSyntaxRegistry.ExplicitVrBigEndian,
        TransferSyntaxRegistry.DeflatedExplicitVrLittleEndian,
    ];

    [Theory]
    [MemberData(nameof(Syntaxes))]
    public void WritesAndReadsBackAFile(TransferSyntax syntax)
    {
        InMemDicomObject source = Sample();

        using MemoryStream stream = new();
        DicomFile.ToFile(source, syntax).Save(stream);
        stream.Position = 0;

        FileDicomObject read = DicomFile.Read(stream);

        Assert.Equal(syntax.Uid, read.Meta.TransferSyntaxUid);
        Assert.Equal("Doe^Jane", read.GetString(new Tag(0x0010, 0x0010)));
        Assert.Equal("1.2.3.4", read.GetString(Tags.SopInstanceUid));
        Assert.Equal(512, read.GetInt32(Tags.Rows));
        Assert.Equal(2.5, read.GetDouble(Tags.RescaleSlope));
        Assert.Equal([1.0, 0.0, 0.0, 0.0, 1.0, 0.0], read.GetElement(new Tag(0x0020, 0x0037))!.Primitive.ToDoubleArray());
    }

    [Theory]
    [MemberData(nameof(Syntaxes))]
    public void PreservesNestedSequences(TransferSyntax syntax)
    {
        InMemDicomObject source = Sample();

        InMemDicomObject inner = new();
        inner.PutString(new Tag(0x0008, 0x0104), "Lumbar");

        InMemDicomObject outer = new();
        outer.Put(new DataElement(new Tag(0x0008, 0x1150), Vr.UI, PrimitiveValue.FromString("1.2.840.10008.5.1.4.1.1.2")));
        outer.Put(new DataElement(new Tag(0x0040, 0xA043), Vr.SQ, DicomValue.OfSequence([inner])));

        source.Put(new DataElement(new Tag(0x0008, 0x1140), Vr.SQ, DicomValue.OfSequence([outer])));

        using MemoryStream stream = new();
        DicomFile.ToFile(source, syntax).Save(stream);
        stream.Position = 0;

        FileDicomObject read = DicomFile.Read(stream);
        IReadOnlyList<InMemDicomObject> items = read.GetObjects(new Tag(0x0008, 0x1140));

        Assert.Single(items);
        Assert.Equal("1.2.840.10008.5.1.4.1.1.2", items[0].GetString(new Tag(0x0008, 0x1150)));

        IReadOnlyList<InMemDicomObject> nested = items[0].GetObjects(new Tag(0x0040, 0xA043));

        Assert.Single(nested);
        Assert.Equal("Lumbar", nested[0].GetString(new Tag(0x0008, 0x0104)));
    }

    [Fact]
    public void PreservesEncapsulatedPixelDataFragments()
    {
        InMemDicomObject source = Sample();
        byte[] first = [1, 2, 3, 4];
        byte[] second = [5, 6, 7, 8];

        source.Put(new DataElement(Tags.PixelData, Vr.OB, DicomValue.OfFragments([first, second], [0u, 12u])));

        TransferSyntax syntax = TransferSyntaxRegistry.Require("1.2.840.10008.1.2.4.90");

        using MemoryStream stream = new();
        DicomFile.ToFile(source, syntax).Save(stream);
        stream.Position = 0;

        FileDicomObject read = DicomFile.Read(stream);
        PixelSequenceDicomValue pixels = Assert.IsType<PixelSequenceDicomValue>(read.GetElement(Tags.PixelData)!.Value);

        Assert.Equal(2, pixels.Fragments.Count);
        Assert.Equal(first, pixels.Fragments[0]);
        Assert.Equal(second, pixels.Fragments[1]);
        Assert.Equal([0u, 12u], pixels.OffsetTable);
    }

    [Fact]
    public void ReadsTextInTheDeclaredCharacterSet()
    {
        InMemDicomObject source = Sample();
        source.Put(new DataElement(Tags.SpecificCharacterSet, Vr.CS, PrimitiveValue.FromString("ISO_IR 192")));
        source.Put(new DataElement(new Tag(0x0010, 0x0010), Vr.PN, PrimitiveValue.FromString("Mü^Jörg")));

        using MemoryStream stream = new();
        DicomFile.ToFile(source, TransferSyntaxRegistry.ExplicitVrLittleEndian).Save(stream);
        stream.Position = 0;

        Assert.Equal("Mü^Jörg", DicomFile.Read(stream).GetString(new Tag(0x0010, 0x0010)));
    }

    internal static InMemDicomObject Sample()
    {
        InMemDicomObject data = new();

        data.Put(new DataElement(Tags.SopClassUid, Vr.UI, PrimitiveValue.FromString("1.2.840.10008.5.1.4.1.1.1")));
        data.Put(new DataElement(Tags.SopInstanceUid, Vr.UI, PrimitiveValue.FromString("1.2.3.4")));
        data.Put(new DataElement(new Tag(0x0010, 0x0010), Vr.PN, PrimitiveValue.FromString("Doe^Jane")));
        data.Put(new DataElement(new Tag(0x0008, 0x0060), Vr.CS, PrimitiveValue.FromString("CR")));
        data.Put(new DataElement(Tags.Rows, Vr.US, PrimitiveValue.FromUInt16([512])));
        data.Put(new DataElement(Tags.Columns, Vr.US, PrimitiveValue.FromUInt16([512])));
        data.Put(new DataElement(Tags.RescaleSlope, Vr.DS, PrimitiveValue.FromString("2.5")));
        data.Put(new DataElement(
            new Tag(0x0020, 0x0037),
            Vr.DS,
            PrimitiveValue.FromStrings(["1.0", "0.0", "0.0", "0.0", "1.0", "0.0"])));

        return data;
    }
}

public class PixelDataTests
{
    [Fact]
    public void ReadsStoredValuesOfAnUnsignedFrame()
    {
        InMemDicomObject data = Image(bitsAllocated: 16, signed: false, [0, 1000, 2000, 4095]);
        NativePixelData pixels = NativePixelData.From(data);

        Assert.Equal([0, 1000, 2000, 4095], pixels.GetFrameSamples(0));
        Assert.Equal([0, 2000, 4000, 8190], pixels.GetFrameValues(0).Select(static v => (int)v));
    }

    [Fact]
    public void SignExtendsStoredValuesOfASignedFrame()
    {
        InMemDicomObject data = Image(bitsAllocated: 16, signed: true, [0, unchecked((ushort)-500), 1000, 2000]);
        NativePixelData pixels = NativePixelData.From(data);

        Assert.Equal([0, -500, 1000, 2000], pixels.GetFrameSamples(0));
    }

    [Fact]
    public void RendersEightBitGrayscaleFromTheStatedWindow()
    {
        InMemDicomObject data = Image(bitsAllocated: 16, signed: false, [0, 1000, 2000, 4095]);
        byte[] rendered = NativePixelData.From(data).RenderGrayscale8(0, windowCenter: 2048, windowWidth: 4096);

        Assert.Equal(4, rendered.Length);
        Assert.Equal(0, rendered[0]);
        Assert.True(rendered[3] > rendered[2] && rendered[2] > rendered[1]);
    }

    /// <summary>
    /// The byte order of a file reaches the samples as well as the attributes, so a big endian
    /// or deflated file has to give the same stored values as a little endian one.
    /// </summary>
    [Theory]
    [MemberData(nameof(RoundTripTests.Syntaxes), MemberType = typeof(RoundTripTests))]
    public void ReadsTheSameStoredValuesWhateverTheFileIsWrittenAs(TransferSyntax syntax)
    {
        InMemDicomObject data = Image(bitsAllocated: 16, signed: true, [0, unchecked((ushort)-500), 1000, 4095]);

        using MemoryStream stream = new();
        DicomFile.ToFile(data, syntax).Save(stream);
        stream.Position = 0;

        NativePixelData pixels = NativePixelData.From(DicomFile.Read(stream));

        Assert.Equal([0, -500, 1000, 4095], pixels.GetFrameSamples(0));
    }

    private static InMemDicomObject Image(int bitsAllocated, bool signed, ushort[] samples)
    {
        InMemDicomObject data = new();

        data.Put(new DataElement(Tags.Rows, Vr.US, PrimitiveValue.FromUInt16([2])));
        data.Put(new DataElement(Tags.Columns, Vr.US, PrimitiveValue.FromUInt16([2])));
        data.Put(new DataElement(Tags.SamplesPerPixel, Vr.US, PrimitiveValue.FromUInt16([1])));
        data.Put(new DataElement(Tags.BitsAllocated, Vr.US, PrimitiveValue.FromUInt16([(ushort)bitsAllocated])));
        data.Put(new DataElement(Tags.BitsStored, Vr.US, PrimitiveValue.FromUInt16([(ushort)bitsAllocated])));
        data.Put(new DataElement(Tags.HighBit, Vr.US, PrimitiveValue.FromUInt16([(ushort)(bitsAllocated - 1)])));
        data.Put(new DataElement(Tags.PixelRepresentation, Vr.US, PrimitiveValue.FromUInt16([(ushort)(signed ? 1 : 0)])));
        data.Put(new DataElement(Tags.RescaleSlope, Vr.DS, PrimitiveValue.FromString("2")));
        data.Put(new DataElement(Tags.PixelData, Vr.OW, PrimitiveValue.FromUInt16(samples)));

        return data;
    }
}
