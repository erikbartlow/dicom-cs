using Dicom.Core;
using Dicom.Dictionary.Std;

namespace DicomObjects;

/// <summary>
/// Process wide settings and the extensible data dictionary.
/// </summary>
public static class DicomGlobal
{
    /// <summary>
    /// Whether files are closed as soon as they have been read. This library always reads a file
    /// into memory and closes it, so the setting is recorded but has no effect.
    /// </summary>
    public static bool CloseFilesAfterReading { get; set; } = true;

    /// <summary>
    /// The dictionary private attributes are added to, which supplies representations for
    /// attributes the standard does not define.
    /// </summary>
    public static ExtensibleDataDictionary Dictionary { get; } = new(StandardDataDictionary.Instance);

    /// <summary>
    /// Adds a private attribute to the dictionary so that it can be read from implicitly
    /// encoded data sets and written with the right representation.
    /// </summary>
    public static void AddDictionaryElement(int group, int element, string vr, string valueMultiplicity, string name)
    {
        Vr representation = VrExtensions.TryParse(vr.AsSpan(), out Vr parsed) ? parsed : Vr.UN;
        Tag tag = new((ushort)group, (ushort)element);

        Dictionary.Add(new DictionaryEntry(tag, name, name, representation, valueMultiplicity));
    }

    /// <summary>
    /// A data set holding one empty attribute for every catalogued tag, which callers use as a
    /// reference list of the attributes the dictionary knows.
    /// </summary>
    public static DicomDataSet MakeDictionary()
    {
        DicomDataSet reference = new();

        foreach (DictionaryEntry entry in StandardDataDictionary.Entries)
            reference.Data.Put(DataElement.CreateEmpty(entry.Tag, entry.Vr));

        return reference;
    }

    /// <summary>Whether a file is a DICOM part 10 file.</summary>
    public static bool IsDICOM(string fileName)
    {
        using FileStream stream = File.OpenRead(fileName);

        return IsDICOM(stream);
    }

    /// <summary>Whether a stream holds a DICOM part 10 file.</summary>
    public static bool IsDICOM(Stream stream)
    {
        if (!stream.CanSeek)
            return false;

        long start = stream.Position;

        try
        {
            if (stream.Length - start < 132)
                return false;

            Span<byte> prologue = stackalloc byte[132];
            stream.ReadExactly(prologue);

            return prologue[128..].SequenceEqual(Dicom.Object.FileMetaTable.Magic);
        }
        finally
        {
            stream.Position = start;
        }
    }

    /// <summary>
    /// Records a named setting. The settings of the original product do not apply here,
    /// so values are kept only so that callers can read back what they set.
    /// </summary>
    public static void SetRegWord(string name, int value) => Settings[name] = value;

    /// <summary>The settings recorded through <see cref="SetRegWord"/>.</summary>
    public static Dictionary<string, int> Settings { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// The standard dictionary extended with privately defined attributes.
/// </summary>
public sealed class ExtensibleDataDictionary : IDataDictionary
{
    private readonly IDataDictionary _standard;
    private readonly Dictionary<Tag, DictionaryEntry> _private = [];
    private readonly Dictionary<string, DictionaryEntry> _byKeyword = new(StringComparer.OrdinalIgnoreCase);

    public ExtensibleDataDictionary(IDataDictionary standard) => _standard = standard;

    /// <summary>Adds or replaces a privately defined attribute.</summary>
    public void Add(DictionaryEntry entry)
    {
        _private[entry.Tag] = entry;
        _byKeyword[entry.Keyword] = entry;
    }

    public DictionaryEntry? Lookup(Tag tag) =>
        _private.TryGetValue(tag, out DictionaryEntry? entry) ? entry : _standard.Lookup(tag);

    public DictionaryEntry? Lookup(string keyword) =>
        _byKeyword.TryGetValue(keyword, out DictionaryEntry? entry) ? entry : _standard.Lookup(keyword);
}
