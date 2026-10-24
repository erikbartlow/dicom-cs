using Dicom.Core;
using Dicom.Dictionary.Std;
using Dicom.Encoding;

namespace Dicom.Tests;

public class TagTests
{
    [Theory]
    [InlineData("(0010,0010)", 0x0010, 0x0010)]
    [InlineData("00100010", 0x0010, 0x0010)]
    [InlineData("7FE0,0010", 0x7FE0, 0x0010)]
    public void ParsesTheWrittenForms(string text, int group, int element)
    {
        Assert.True(Tag.TryParse(text, out Tag tag));
        Assert.Equal(group, tag.Group);
        Assert.Equal(element, tag.Element);
    }

    [Fact]
    public void FormatsWithGroupAndElement()
    {
        Assert.Equal("(7FE0,0010)", new Tag(0x7FE0, 0x0010).ToString());
        Assert.Equal("7FE00010", new Tag(0x7FE0, 0x0010).ToString("X", null));
    }

    [Fact]
    public void ReducesRepeatingGroupsToTheirCanonicalForm()
    {
        Assert.Equal(new Tag(0x6000, 0x0010), new Tag(0x601C, 0x0010).Canonical());
        Assert.Equal(new Tag(0x0010, 0x0010), new Tag(0x0010, 0x0010).Canonical());
    }

    [Fact]
    public void RecognisesPrivateGroups()
    {
        Assert.True(new Tag(0x7BF7, 0x1001).IsPrivate);
        Assert.False(new Tag(0x0010, 0x0010).IsPrivate);
    }
}

public class PrimitiveValueTests
{
    [Fact]
    public void SplitsMultipleTextValues()
    {
        PrimitiveValue value = ValueDecoder.Decode(Vr.CS, "AXIAL\\HELIX "u8);

        Assert.Equal(2, value.Multiplicity);
        Assert.Equal(["AXIAL", "HELIX"], value.ToStringArray());
        Assert.Equal("AXIAL\\HELIX", value.ToMultiStringValue());
    }

    [Fact]
    public void KeepsBackslashesInUnsplitRepresentations()
    {
        PrimitiveValue value = ValueDecoder.Decode(Vr.ST, "a\\b"u8);

        Assert.Equal(1, value.Multiplicity);
        Assert.Equal("a\\b", value.ToStringValue());
    }

    [Fact]
    public void ReadsNumbersWrittenAsText()
    {
        PrimitiveValue value = ValueDecoder.Decode(Vr.DS, "1.5\\-2.25"u8);

        Assert.Equal(1.5, value.ToDouble());
        Assert.Equal(-2.25, value.ToDouble(1));
        Assert.Equal([1.5, -2.25], value.ToDoubleArray());
    }

    [Fact]
    public void ReadsBinaryNumbersInBothByteOrders()
    {
        byte[] little = [0x34, 0x12];
        byte[] big = [0x12, 0x34];

        Assert.Equal(0x1234, ValueDecoder.Decode(Vr.US, little).ToInt32());
        Assert.Equal(0x1234, ValueDecoder.Decode(Vr.US, big, ByteOrder.BigEndian).ToInt32());
    }

    [Fact]
    public void PadsOddLengthValuesWhenEncoding()
    {
        byte[] encoded = ValueEncoder.Encode(Vr.LO, PrimitiveValue.FromString("abc"));

        Assert.Equal(4, encoded.Length);
        Assert.Equal((byte)' ', encoded[3]);
    }

    [Fact]
    public void PadsUniqueIdentifiersWithNull()
    {
        byte[] encoded = ValueEncoder.Encode(Vr.UI, PrimitiveValue.FromString("1.2.3"));

        Assert.Equal(6, encoded.Length);
        Assert.Equal(0x00, encoded[5]);
    }

    [Theory]
    [InlineData("20240115", 2024, 1, 15)]
    [InlineData("2024", 2024, 1, 1)]
    [InlineData("202403", 2024, 3, 1)]
    public void ParsesDatesOfPartialPrecision(string text, int year, int month, int day)
    {
        Assert.True(DicomDateTime.TryParseDate(text, out DateOnly date));
        Assert.Equal(new DateOnly(year, month, day), date);
    }

    [Fact]
    public void ParsesTimesWithAFraction()
    {
        Assert.True(DicomDateTime.TryParseTime("131415.500000", out TimeOnly time));
        Assert.Equal(new TimeOnly(13, 14, 15, 500), time);
    }

    [Fact]
    public void ParsesDateTimesWithAnOffset()
    {
        Assert.True(DicomDateTime.TryParseDateTime("20240115131415.000000-0500", out DateTimeOffset moment));
        Assert.Equal(new DateTimeOffset(2024, 1, 15, 13, 14, 15, TimeSpan.FromHours(-5)), moment);
    }
}

public class StandardDictionaryTests
{
    [Theory]
    [InlineData(0x0010, 0x0010, "PatientName", "PN")]
    [InlineData(0x0008, 0x0005, "SpecificCharacterSet", "CS")]
    [InlineData(0x0028, 0x1053, "RescaleSlope", "DS")]
    public void CataloguesTheStandardAttributes(int group, int element, string keyword, string vr)
    {
        DictionaryEntry? entry = StandardDataDictionary.Instance.Lookup(new Tag((ushort)group, (ushort)element));

        Assert.NotNull(entry);
        Assert.Equal(keyword, entry.Keyword);
        Assert.Equal(vr, entry.Vr.Code());
    }

    [Fact]
    public void ResolvesKeywordsToTags()
    {
        Assert.Equal(new Tag(0x0010, 0x0010), StandardDataDictionary.Instance.TagFor("PatientName"));
    }

    [Fact]
    public void FallsBackToUnknownForUncataloguedTags()
    {
        Assert.Equal(Vr.UN, StandardDataDictionary.Instance.VrFor(new Tag(0x7BF7, 0x1001)));
        Assert.Equal(Vr.UL, StandardDataDictionary.Instance.VrFor(new Tag(0x0008, 0x0000)));
    }
}
