using System.Collections;

namespace Dicom.Core;

/// <summary>
/// The value of a data element that is not a sequence: either text, a list of tags, or an array
/// of numbers of a given width. The representation chosen mirrors how the value was decoded,
/// and the conversion methods translate between representations on demand.
/// </summary>
public sealed partial class PrimitiveValue : IEquatable<PrimitiveValue>
{
    private static readonly string[] NoStrings = [];

    private readonly object? _payload;

    private PrimitiveValue(PrimitiveValueKind kind, object? payload)
    {
        Kind = kind;
        _payload = payload;
    }

    /// <summary>A value with no content.</summary>
    public static PrimitiveValue Empty { get; } = new(PrimitiveValueKind.Empty, null);

    /// <summary>Which representation this value is holding.</summary>
    public PrimitiveValueKind Kind { get; }

    /// <summary>Whether this value has no content.</summary>
    public bool IsEmpty => Kind == PrimitiveValueKind.Empty || Multiplicity == 0;

    /// <summary>
    /// The number of individual values held, which corresponds to the attribute's value multiplicity.
    /// </summary>
    public int Multiplicity => Kind switch
    {
        PrimitiveValueKind.Empty => 0,
        PrimitiveValueKind.Text => 1,
        _ => ((Array)_payload!).Length,
    };

    #region Factories

    public static PrimitiveValue FromString(string? value) =>
        value is null ? Empty : new PrimitiveValue(PrimitiveValueKind.Text, value);

    public static PrimitiveValue FromStrings(IEnumerable<string>? values)
    {
        if (values is null)
            return Empty;

        string[] array = values as string[] ?? [.. values];

        return array.Length == 0 ? Empty : new PrimitiveValue(PrimitiveValueKind.Strings, array);
    }

    public static PrimitiveValue FromTags(IEnumerable<Tag>? values) =>
        Create(PrimitiveValueKind.Tags, values);

    public static PrimitiveValue FromBytes(byte[]? values) =>
        values is null || values.Length == 0 ? Empty : new PrimitiveValue(PrimitiveValueKind.U8, values);

    public static PrimitiveValue FromInt16(IEnumerable<short>? values) =>
        Create(PrimitiveValueKind.I16, values);

    public static PrimitiveValue FromUInt16(IEnumerable<ushort>? values) =>
        Create(PrimitiveValueKind.U16, values);

    public static PrimitiveValue FromInt32(IEnumerable<int>? values) =>
        Create(PrimitiveValueKind.I32, values);

    public static PrimitiveValue FromUInt32(IEnumerable<uint>? values) =>
        Create(PrimitiveValueKind.U32, values);

    public static PrimitiveValue FromInt64(IEnumerable<long>? values) =>
        Create(PrimitiveValueKind.I64, values);

    public static PrimitiveValue FromUInt64(IEnumerable<ulong>? values) =>
        Create(PrimitiveValueKind.U64, values);

    public static PrimitiveValue FromSingle(IEnumerable<float>? values) =>
        Create(PrimitiveValueKind.F32, values);

    public static PrimitiveValue FromDouble(IEnumerable<double>? values) =>
        Create(PrimitiveValueKind.F64, values);

    private static PrimitiveValue Create<T>(PrimitiveValueKind kind, IEnumerable<T>? values)
    {
        if (values is null)
            return Empty;

        T[] array = values as T[] ?? [.. values];

        return array.Length == 0 ? Empty : new PrimitiveValue(kind, array);
    }

    #endregion

    #region Typed access

    /// <summary>The underlying array, or null when the value is empty or a single text value.</summary>
    public Array? RawArray => _payload as Array;

    /// <summary>The underlying single text value, or null when another representation is held.</summary>
    public string? RawText => Kind == PrimitiveValueKind.Text ? (string)_payload! : null;

    private T[] Payload<T>(PrimitiveValueKind kind) =>
        Kind == kind ? (T[])_payload! : throw new DicomValueException($"The value holds {Kind}, not {kind}.");

    public string[] AsStrings() =>
        Kind switch
        {
            PrimitiveValueKind.Empty => NoStrings,
            PrimitiveValueKind.Text => [(string)_payload!],
            PrimitiveValueKind.Strings => (string[])_payload!,
            _ => throw new DicomValueException($"The value holds {Kind}, not text."),
        };

    public Tag[] AsTags() => Payload<Tag>(PrimitiveValueKind.Tags);

    public byte[] AsBytes() => Payload<byte>(PrimitiveValueKind.U8);

    public short[] AsInt16() => Payload<short>(PrimitiveValueKind.I16);

    public ushort[] AsUInt16() => Payload<ushort>(PrimitiveValueKind.U16);

    public int[] AsInt32() => Payload<int>(PrimitiveValueKind.I32);

    public uint[] AsUInt32() => Payload<uint>(PrimitiveValueKind.U32);

    public long[] AsInt64() => Payload<long>(PrimitiveValueKind.I64);

    public ulong[] AsUInt64() => Payload<ulong>(PrimitiveValueKind.U64);

    public float[] AsSingle() => Payload<float>(PrimitiveValueKind.F32);

    public double[] AsDouble() => Payload<double>(PrimitiveValueKind.F64);

    #endregion

    public bool Equals(PrimitiveValue? other)
    {
        if (other is null || Kind != other.Kind)
            return false;

        if (Kind == PrimitiveValueKind.Empty)
            return true;

        if (Kind == PrimitiveValueKind.Text)
            return (string)_payload! == (string)other._payload!;

        return StructuralComparisons.StructuralEqualityComparer.Equals(_payload, other._payload);
    }

    public override bool Equals(object? obj) => Equals(obj as PrimitiveValue);

    public override int GetHashCode() => Kind == PrimitiveValueKind.Empty
        ? 0
        : HashCode.Combine(Kind, Multiplicity, ToShortString());
}
