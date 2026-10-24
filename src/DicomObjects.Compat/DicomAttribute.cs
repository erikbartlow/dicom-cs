using Dicom.Core;
using Dicom.Dictionary.Std;
using DicomObjects.Enums;

namespace DicomObjects;

/// <summary>
/// One attribute of a data set, addressed by its group and element numbers.
/// An instance is a view onto the owning data set, so assigning to <see cref="Value"/>
/// stores the value, and reading it from an absent attribute gives null.
/// </summary>
public sealed class DicomAttribute
{
    private readonly DicomDataSet _owner;

    internal DicomAttribute(DicomDataSet owner, int group, int element)
    {
        _owner = owner;
        Group = group;
        Element = element;
    }

    /// <summary>The group number.</summary>
    public int Group { get; }

    /// <summary>The element number.</summary>
    public int Element { get; }

    internal Tag Tag => new((ushort)Group, (ushort)Element);

    /// <summary>Whether the attribute is absent or holds no value.</summary>
    public bool IsNull
    {
        get
        {
            DataElement? element = _owner.Find(Tag);

            return element is null || element.IsEmpty;
        }
    }

    /// <summary>Whether the attribute is present, whether or not it holds a value.</summary>
    public bool Exists => _owner.Find(Tag) is not null;

    /// <summary>The two letter value representation of the attribute.</summary>
    public string VR
    {
        get => (_owner.Find(Tag)?.Vr ?? _owner.ResolveVr(Tag)).Code();
        set => _owner.SetVr(Tag, value);
    }

    /// <summary>The number of individual values held.</summary>
    public int VM => _owner.Find(Tag)?.Multiplicity ?? 0;

    /// <summary>
    /// Whether more than one value is held, so that <see cref="Value"/> gives an array.
    /// </summary>
    public bool IsMultiple => VM > 1;

    /// <summary>The name the dictionary gives this attribute, or an empty string for an unknown tag.</summary>
    public string Description => DicomGlobal.Dictionary.Lookup(Tag)?.Name ?? string.Empty;

    /// <summary>
    /// The attribute's value: text, a number, an array of either, a byte array for binary data,
    /// or a <see cref="DicomDataSetCollection"/> for a sequence.
    /// </summary>
    public object? Value
    {
        get
        {
            DataElement? element = _owner.Find(Tag);

            return element is null ? null : ValueInterop.ToObject(element);
        }
        set => _owner.Add(Group, Element, value);
    }

    /// <summary>The value multiplicity the standard defines for an attribute, such as <c>1-n</c>.</summary>
    public static string DefinedVM(Keyword keyword)
    {
        Tag tag = Tag.FromValue((uint)keyword);

        return StandardDataDictionary.Instance.Lookup(tag)?.ValueMultiplicity ?? string.Empty;
    }

    /// <summary>The value representation the standard defines for an attribute.</summary>
    public static string DefinedVR(Keyword keyword)
    {
        Tag tag = Tag.FromValue((uint)keyword);

        return (StandardDataDictionary.Instance.Lookup(tag)?.Vr ?? Vr.UN).Code();
    }

    public override string ToString() => $"({Group:X4},{Element:X4}) {VR}";
}
