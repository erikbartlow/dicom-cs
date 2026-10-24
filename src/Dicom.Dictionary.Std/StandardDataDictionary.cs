using System.Globalization;
using System.Reflection;
using Dicom.Core;

namespace Dicom.Dictionary.Std;

/// <summary>
/// The registry of attributes published in part 6 of the standard, loaded from an embedded table
/// the first time it is used. Repeating group attributes are catalogued in their canonical form,
/// so an overlay attribute in group 601C resolves through group 6000.
/// </summary>
public sealed class StandardDataDictionary : IDataDictionary
{
    private const string ResourceName = "Dicom.Dictionary.Std.Resources.attributes.tsv";

    private static readonly Lazy<Tables> Loaded = new(Load, isThreadSafe: true);

    private StandardDataDictionary()
    {
    }

    /// <summary>The shared instance of the standard dictionary.</summary>
    public static StandardDataDictionary Instance { get; } = new();

    /// <summary>Every catalogued attribute, ordered by tag.</summary>
    public static IReadOnlyCollection<DictionaryEntry> Entries => Loaded.Value.ByTag.Values;

    public DictionaryEntry? Lookup(Tag tag)
    {
        Tables tables = Loaded.Value;

        if (tables.ByTag.TryGetValue(tag, out DictionaryEntry? entry))
            return entry;

        Tag canonical = tag.Canonical();

        return canonical != tag && tables.ByTag.TryGetValue(canonical, out entry) ? entry : null;
    }

    public DictionaryEntry? Lookup(string keyword) =>
        Loaded.Value.ByKeyword.TryGetValue(keyword, out DictionaryEntry? entry) ? entry : null;

    private static Tables Load()
    {
        SortedDictionary<Tag, DictionaryEntry> byTag = [];
        Dictionary<string, DictionaryEntry> byKeyword = new(StringComparer.OrdinalIgnoreCase);

        using Stream stream = typeof(StandardDataDictionary).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded attribute table '{ResourceName}' is missing.");

        using StreamReader reader = new(stream);

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0)
                continue;

            string[] fields = line.Split('\t');

            if (fields.Length < 5)
                continue;

            if (!uint.TryParse(fields[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint raw))
                continue;

            if (!VrExtensions.TryParse(fields[2].AsSpan(), out Vr vr))
                vr = Vr.UN;

            DictionaryEntry entry = new(Tag.FromValue(raw), fields[1], fields[4], vr, fields[3]);

            byTag[entry.Tag] = entry;
            byKeyword[entry.Keyword] = entry;
        }

        return new Tables(byTag, byKeyword);
    }

    private sealed class Tables
    {
        public Tables(
            SortedDictionary<Tag, DictionaryEntry> byTag,
            Dictionary<string, DictionaryEntry> byKeyword)
        {
            ByTag = byTag;
            ByKeyword = byKeyword;
        }

        public SortedDictionary<Tag, DictionaryEntry> ByTag { get; }

        public Dictionary<string, DictionaryEntry> ByKeyword { get; }
    }
}
