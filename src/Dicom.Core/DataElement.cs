namespace Dicom.Core;

/// <summary>
/// An attribute of a data set: a tag, the value representation its value is encoded with,
/// and the value itself.
/// </summary>
public sealed class DataElement
{
    public DataElement(Tag tag, Vr vr, DicomValue value)
    {
        Tag = tag;
        Vr = vr;
        Value = value;
    }

    public DataElement(Tag tag, Vr vr, PrimitiveValue value)
        : this(tag, vr, DicomValue.Of(value))
    {
    }

    /// <summary>The attribute's tag.</summary>
    public Tag Tag { get; }

    /// <summary>The attribute's value representation.</summary>
    public Vr Vr { get; }

    /// <summary>The attribute's value.</summary>
    public DicomValue Value { get; }

    /// <summary>Whether the attribute has no value, which the standard permits for any attribute.</summary>
    public bool IsEmpty => Value.IsEmpty;

    /// <summary>The number of individual values held.</summary>
    public int Multiplicity => Value.Multiplicity;

    /// <summary>Whether the attribute is a sequence of nested data sets.</summary>
    public bool IsSequence => Value.Kind == DicomValueKind.Sequence;

    /// <summary>The items of a sequence attribute, or an empty list for any other attribute.</summary>
    public IReadOnlyList<IDicomDataSet> Items =>
        Value is SequenceDicomValue sequence ? sequence.Items : [];

    /// <summary>The primitive content, raising <see cref="DicomValueException"/> for a sequence.</summary>
    public PrimitiveValue Primitive => Value.RequirePrimitive();

    /// <summary>Creates an element with an empty value.</summary>
    public static DataElement CreateEmpty(Tag tag, Vr vr) => new(tag, vr, DicomValue.Empty);

    public override string ToString() => $"{Tag} {Vr.Code()} {Value}";
}

/// <summary>
/// The read side of a data set, shared by the top level object and the items nested inside sequences.
/// </summary>
public interface IDicomDataSet : IEnumerable<DataElement>
{
    /// <summary>The number of attributes present.</summary>
    int Count { get; }

    /// <summary>The attribute with the given tag, or null when it is absent.</summary>
    DataElement? GetElement(Tag tag);

    /// <summary>Whether an attribute with the given tag is present.</summary>
    bool Contains(Tag tag);
}
