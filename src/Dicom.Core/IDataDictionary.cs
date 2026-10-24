namespace Dicom.Core;

/// <summary>
/// What the data dictionary knows about one attribute.
/// </summary>
public sealed class DictionaryEntry
{
    public DictionaryEntry(Tag tag, string keyword, string name, Vr vr, string valueMultiplicity)
    {
        Tag = tag;
        Keyword = keyword;
        Name = name;
        Vr = vr;
        ValueMultiplicity = valueMultiplicity;
    }

    /// <summary>The attribute's tag.</summary>
    public Tag Tag { get; }

    /// <summary>The attribute's keyword, such as <c>PatientName</c>.</summary>
    public string Keyword { get; }

    /// <summary>The attribute's descriptive name, such as <c>Patient's Name</c>.</summary>
    public string Name { get; }

    /// <summary>The value representation the attribute is encoded with.</summary>
    public Vr Vr { get; }

    /// <summary>The permitted value multiplicity, such as <c>1</c>, <c>1-n</c> or <c>3</c>.</summary>
    public string ValueMultiplicity { get; }

    public override string ToString() => $"{Tag} {Keyword} {Vr.Code()} {ValueMultiplicity}";
}

/// <summary>
/// A catalogue of known attributes, used to supply the value representation when reading
/// implicit VR encoded data and to resolve attributes referred to by keyword.
/// </summary>
public interface IDataDictionary
{
    /// <summary>The entry for the given tag, or null when the tag is not catalogued.</summary>
    DictionaryEntry? Lookup(Tag tag);

    /// <summary>The entry for the given keyword, or null when the keyword is not catalogued.</summary>
    DictionaryEntry? Lookup(string keyword);
}

/// <summary>
/// Convenience lookups layered over any dictionary implementation.
/// </summary>
public static class DataDictionaryExtensions
{
    /// <summary>
    /// The value representation to assume for an attribute read without one. Private attributes and
    /// uncatalogued attributes fall back to <see cref="Vr.UN"/>, and group lengths are always
    /// <see cref="Vr.UL"/>.
    /// </summary>
    public static Vr VrFor(this IDataDictionary dictionary, Tag tag)
    {
        if (tag.IsGroupLength)
            return Vr.UL;

        if (tag.IsDelimiter)
            return Vr.UN;

        return dictionary.Lookup(tag)?.Vr
            ?? dictionary.Lookup(tag.Canonical())?.Vr
            ?? Vr.UN;
    }

    /// <summary>The tag for the given keyword, or null when the keyword is not catalogued.</summary>
    public static Tag? TagFor(this IDataDictionary dictionary, string keyword) =>
        dictionary.Lookup(keyword)?.Tag;
}
