using System.Collections;
using Dicom.Core;
using Dicom.Dictionary.Std;
using Dicom.Encoding;
using Dicom.Object;
using DicomObjects.Enums;
using DicomObjects.Validation;

namespace DicomObjects;

/// <summary>
/// A DICOM data set that can be read from and written to files, streams and byte arrays,
/// and whose attributes are addressed by group and element number.
/// </summary>
public partial class DicomDataSet : IEnumerable<DicomAttribute>, IDisposable
{
    private InMemDicomObject _data;

    public DicomDataSet() => _data = new InMemDicomObject(DicomGlobal.Dictionary);

    public DicomDataSet(byte[] contents)
        : this() => Read(contents);

    internal DicomDataSet(InMemDicomObject data) => _data = data;

    /// <summary>
    /// A data set that works on the same attributes as another, which is how an image and the
    /// data set it was made from stay in step.
    /// </summary>
    protected DicomDataSet(DicomDataSet other)
    {
        _data = other._data;
        OriginalTS = other.OriginalTS;
        OverrideTS = other.OverrideTS;
        CacheMode = other.CacheMode;
        ValidationOptions = other.ValidationOptions;
    }

    /// <summary>The underlying data set, for callers working directly against this library.</summary>
    public InMemDicomObject Data => _data;

    /// <summary>The transfer syntax the data set was read from, when it was read from a file.</summary>
    public string OriginalTS { get; private set; } = UIDs.TransferSyntaxes.ImplicitVRLittleEndian;

    /// <summary>
    /// A transfer syntax to assume instead of the one the file states, for data sets whose
    /// file meta information is wrong or missing.
    /// </summary>
    public string? OverrideTS { get; set; }

    /// <summary>How the data set holds on to what it has read. Everything is held in memory here.</summary>
    public CacheTypes CacheMode { get; set; } = CacheTypes.Memory;

    /// <summary>
    /// Which checks are applied to values. Nothing is checked here, so the setting is recorded
    /// and read back but changes nothing.
    /// </summary>
    public ValidationOptions ValidationOptions { get; set; } = ValidationOptions.None;

    /// <summary>The storage class of the data set.</summary>
    public string? SOPClass
    {
        get => _data.GetString(Tags.SopClassUid);
        set => _data.Put(Tags.SopClassUid, Vr.UI, PrimitiveValue.FromString(value));
    }

    /// <summary>The number of attributes present.</summary>
    public int Count => _data.Count;

    /// <summary>The bytes of the pixel data attribute, uncompressed.</summary>
    public byte[]? RawPixels
    {
        get
        {
            DataElement? element = _data.GetElement(Tags.PixelData);

            return element is null || element.Value is PixelSequenceDicomValue ? null : element.Primitive.ToBytes();
        }
        set => _data.Put(
            Tags.PixelData,
            (_data.GetInt32(Tags.BitsAllocated) ?? 16) > 8 ? Vr.OW : Vr.OB,
            PrimitiveValue.FromBytes(value));
    }

    /// <summary>The attribute with the given group and element numbers.</summary>
    public DicomAttribute this[int group, int element] => new(this, group, element);

    /// <summary>The value of an attribute, or null when it is absent.</summary>
    public object? Value(int group, int element)
    {
        DataElement? element1 = Find(new Tag((ushort)group, (ushort)element));

        return element1 is null ? null : ValueInterop.ToObject(element1);
    }

    /// <summary>Adds or replaces an attribute, taking the representation from the dictionary.</summary>
    public void Add(int group, int element, object? value) => Add(group, element, null, value);

    /// <summary>Adds or replaces an attribute with an explicit representation.</summary>
    public void Add(int group, int element, string? vr, object? value)
    {
        Tag tag = new((ushort)group, (ushort)element);

        Vr representation = vr is not null && VrExtensions.TryParse(vr.AsSpan(), out Vr parsed)
            ? parsed
            : Find(tag)?.Vr ?? ResolveVr(tag);

        if (value is DicomDataSetCollection sequence)
        {
            _data.Put(new DataElement(tag, Vr.SQ, DicomValue.OfSequence(sequence.Select(static s => s.Data))));
            return;
        }

        if (value is DicomDataSet item)
        {
            _data.Put(new DataElement(tag, Vr.SQ, DicomValue.OfSequence([item.Data])));
            return;
        }

        _data.Put(new DataElement(tag, representation, ValueInterop.FromObject(value, representation)));
    }

    /// <summary>Removes an attribute.</summary>
    public void Remove(int group, int element) => _data.Remove(new Tag((ushort)group, (ushort)element));

    /// <summary>Removes every attribute.</summary>
    public void Clear() => _data.Clear();

    /// <summary>Whether an attribute is present.</summary>
    public bool Exists(int group, int element) => Find(new Tag((ushort)group, (ushort)element)) is not null;

    /// <summary>A copy of the data set that shares nothing with this one.</summary>
    public DicomDataSet Copy() => new(_data.Clone()) { OriginalTS = OriginalTS, OverrideTS = OverrideTS };

    public IEnumerator<DicomAttribute> GetEnumerator()
    {
        foreach (DataElement element in _data)
            yield return new DicomAttribute(this, element.Tag.Group, element.Tag.Element);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose() => GC.SuppressFinalize(this);

    internal DataElement? Find(Tag tag) => _data.GetElement(tag);

    internal Vr ResolveVr(Tag tag) => _data.Dictionary.VrFor(tag);

    internal void SetVr(Tag tag, string vr)
    {
        DataElement? element = Find(tag);

        if (element is null || !VrExtensions.TryParse(vr.AsSpan(), out Vr parsed))
            return;

        _data.Put(new DataElement(tag, parsed, element.Value));
    }

    internal void Replace(InMemDicomObject data, string originalTransferSyntax)
    {
        _data = data;
        OriginalTS = originalTransferSyntax;
    }

    public override string ToString() => $"data set of {Count} attribute(s)";
}
