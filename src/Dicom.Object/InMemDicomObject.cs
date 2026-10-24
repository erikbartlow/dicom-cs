using System.Collections;
using Dicom.Core;
using Dicom.Dictionary.Std;

namespace Dicom.Object;

/// <summary>
/// A data set held in memory, keyed by tag and kept in ascending tag order.
/// This is the type produced when reading a file and the type written back out.
/// </summary>
public class InMemDicomObject : IDicomDataSet, IEnumerable<DataElement>
{
    private readonly SortedDictionary<Tag, DataElement> _elements = [];

    public InMemDicomObject()
        : this(StandardDataDictionary.Instance)
    {
    }

    public InMemDicomObject(IDataDictionary dictionary) => Dictionary = dictionary;

    /// <summary>The dictionary used to resolve keywords and to supply missing representations.</summary>
    public IDataDictionary Dictionary { get; }

    /// <summary>The number of attributes present.</summary>
    public int Count => _elements.Count;

    /// <summary>The attribute with the given tag.</summary>
    public DataElement? this[Tag tag]
    {
        get => GetElement(tag);
        set
        {
            if (value is null)
                Remove(tag);
            else
                Put(value);
        }
    }

    /// <summary>The attribute with the given keyword, such as <c>PatientName</c>.</summary>
    public DataElement? this[string keyword] =>
        Dictionary.TagFor(keyword) is { } tag ? GetElement(tag) : null;

    public DataElement? GetElement(Tag tag) =>
        _elements.TryGetValue(tag, out DataElement? element) ? element : null;

    public bool Contains(Tag tag) => _elements.ContainsKey(tag);

    /// <summary>The attribute with the given tag, raising when it is absent.</summary>
    public DataElement RequireElement(Tag tag) =>
        GetElement(tag) ?? throw new DicomValueException($"The data set has no attribute {tag}.");

    /// <summary>Adds or replaces an attribute.</summary>
    public DataElement Put(DataElement element)
    {
        _elements[element.Tag] = element;
        return element;
    }

    /// <summary>Adds or replaces an attribute, taking the representation from the dictionary.</summary>
    public DataElement Put(Tag tag, PrimitiveValue value) =>
        Put(new DataElement(tag, Dictionary.VrFor(tag), value));

    /// <summary>Adds or replaces an attribute with an explicit representation.</summary>
    public DataElement Put(Tag tag, Vr vr, PrimitiveValue value) => Put(new DataElement(tag, vr, value));

    /// <summary>Adds or replaces a text attribute.</summary>
    public DataElement PutString(Tag tag, string? value) =>
        Put(tag, PrimitiveValue.FromString(value));

    /// <summary>Removes an attribute, reporting whether it was present.</summary>
    public bool Remove(Tag tag) => _elements.Remove(tag);

    /// <summary>Removes every attribute.</summary>
    public void Clear() => _elements.Clear();

    #region Typed reads

    /// <summary>The attribute's first value as text, or null when the attribute is absent or empty.</summary>
    public string? GetString(Tag tag)
    {
        DataElement? element = GetElement(tag);

        return element is null || element.IsEmpty ? null : element.Primitive.ToStringValue();
    }

    /// <summary>The attribute's values as text, or an empty array when the attribute is absent.</summary>
    public string[] GetStrings(Tag tag) => GetElement(tag)?.Primitive.ToStringArray() ?? [];

    /// <summary>The attribute's first value as an integer, or null when it is absent or not a number.</summary>
    public int? GetInt32(Tag tag)
    {
        DataElement? element = GetElement(tag);

        return element is not null && element.Value.Primitive is { } value && value.TryToInt64(0, out long result)
            ? checked((int)result)
            : null;
    }

    /// <summary>The attribute's first value as a number, or null when it is absent or not a number.</summary>
    public double? GetDouble(Tag tag)
    {
        DataElement? element = GetElement(tag);

        return element is not null && element.Value.Primitive is { } value && value.TryToDouble(0, out double result)
            ? result
            : null;
    }

    /// <summary>The items of a sequence attribute, or an empty list when it is absent.</summary>
    public IReadOnlyList<IDicomDataSet> GetItems(Tag tag) => GetElement(tag)?.Items ?? [];

    /// <summary>The items of a sequence attribute as in memory data sets.</summary>
    public IReadOnlyList<InMemDicomObject> GetObjects(Tag tag) =>
        [.. GetItems(tag).OfType<InMemDicomObject>()];

    #endregion

    /// <summary>A copy of this data set, with nested sequences copied as well.</summary>
    public InMemDicomObject Clone()
    {
        InMemDicomObject copy = new(Dictionary);

        foreach (DataElement element in _elements.Values)
        {
            copy.Put(element.Value is SequenceDicomValue sequence
                ? new DataElement(element.Tag, element.Vr, DicomValue.OfSequence(
                    sequence.Items.Select(static item => item is InMemDicomObject inMemory
                        ? inMemory.Clone()
                        : item)))
                : element);
        }

        return copy;
    }

    public IEnumerator<DataElement> GetEnumerator() => _elements.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => $"data set of {Count} attribute(s)";
}
