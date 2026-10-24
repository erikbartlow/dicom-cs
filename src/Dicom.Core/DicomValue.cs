namespace Dicom.Core;

/// <summary>
/// The three shapes a data element's value can take.
/// </summary>
public enum DicomValueKind
{
    /// <summary>Text, numbers or opaque bytes.</summary>
    Primitive,

    /// <summary>A sequence of nested data sets.</summary>
    Sequence,

    /// <summary>Encapsulated pixel data, held as an offset table and a list of fragments.</summary>
    PixelSequence,
}

/// <summary>
/// The value carried by a data element.
/// </summary>
public abstract class DicomValue
{
    /// <summary>Which shape this value takes.</summary>
    public abstract DicomValueKind Kind { get; }

    /// <summary>Whether the value carries no content.</summary>
    public abstract bool IsEmpty { get; }

    /// <summary>
    /// The number of individual values, which for a sequence is the number of items
    /// and for encapsulated pixel data the number of fragments.
    /// </summary>
    public abstract int Multiplicity { get; }

    /// <summary>The primitive content, or null when this value is a sequence.</summary>
    public virtual PrimitiveValue? Primitive => null;

    /// <summary>
    /// The primitive content, raising <see cref="DicomValueException"/> when this value is a sequence.
    /// </summary>
    public PrimitiveValue RequirePrimitive() =>
        Primitive ?? throw new DicomValueException($"A {Kind} value has no primitive content.");

    public static DicomValue Of(PrimitiveValue value) => new PrimitiveDicomValue(value);

    public static DicomValue Of(string value) => new PrimitiveDicomValue(PrimitiveValue.FromString(value));

    public static DicomValue OfSequence(IEnumerable<IDicomDataSet> items) => new SequenceDicomValue(items);

    public static DicomValue OfFragments(IEnumerable<byte[]> fragments, IEnumerable<uint>? offsetTable = null) =>
        new PixelSequenceDicomValue(fragments, offsetTable);

    /// <summary>An empty primitive value.</summary>
    public static DicomValue Empty { get; } = new PrimitiveDicomValue(PrimitiveValue.Empty);
}

/// <summary>
/// A value holding text, numbers or opaque bytes.
/// </summary>
public sealed class PrimitiveDicomValue : DicomValue
{
    public PrimitiveDicomValue(PrimitiveValue value) => Value = value;

    /// <summary>The primitive content.</summary>
    public PrimitiveValue Value { get; }

    public override DicomValueKind Kind => DicomValueKind.Primitive;

    public override bool IsEmpty => Value.IsEmpty;

    public override int Multiplicity => Value.Multiplicity;

    public override PrimitiveValue Primitive => Value;

    public override string ToString() => Value.ToShortString();
}

/// <summary>
/// A value holding a sequence of nested data sets.
/// </summary>
public sealed class SequenceDicomValue : DicomValue
{
    public SequenceDicomValue(IEnumerable<IDicomDataSet> items) =>
        Items = items as IReadOnlyList<IDicomDataSet> ?? [.. items];

    /// <summary>The items of the sequence, in order.</summary>
    public IReadOnlyList<IDicomDataSet> Items { get; }

    public override DicomValueKind Kind => DicomValueKind.Sequence;

    public override bool IsEmpty => Items.Count == 0;

    public override int Multiplicity => Items.Count;

    public override string ToString() => $"sequence of {Items.Count} item(s)";
}

/// <summary>
/// Encapsulated pixel data: a basic offset table followed by the compressed fragments.
/// </summary>
public sealed class PixelSequenceDicomValue : DicomValue
{
    public PixelSequenceDicomValue(IEnumerable<byte[]> fragments, IEnumerable<uint>? offsetTable = null)
    {
        Fragments = fragments as IReadOnlyList<byte[]> ?? [.. fragments];
        OffsetTable = offsetTable is null
            ? []
            : offsetTable as IReadOnlyList<uint> ?? [.. offsetTable];
    }

    /// <summary>The basic offset table, which may be empty.</summary>
    public IReadOnlyList<uint> OffsetTable { get; }

    /// <summary>The pixel data fragments, in order.</summary>
    public IReadOnlyList<byte[]> Fragments { get; }

    public override DicomValueKind Kind => DicomValueKind.PixelSequence;

    public override bool IsEmpty => Fragments.Count == 0;

    public override int Multiplicity => Fragments.Count;

    public override string ToString() => $"{Fragments.Count} pixel data fragment(s)";
}
