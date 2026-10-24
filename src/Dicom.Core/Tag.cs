using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Dicom.Core;

/// <summary>
/// The data type for DICOM data element tags, identifying an attribute by its group and element numbers.
/// </summary>
public readonly struct Tag : IEquatable<Tag>, IComparable<Tag>, IFormattable
{
    /// <summary>The item start tag (FFFE,E000).</summary>
    public static readonly Tag Item = new(0xFFFE, 0xE000);

    /// <summary>The item delimitation tag (FFFE,E00D).</summary>
    public static readonly Tag ItemDelimitationItem = new(0xFFFE, 0xE00D);

    /// <summary>The sequence delimitation tag (FFFE,E0DD).</summary>
    public static readonly Tag SequenceDelimitationItem = new(0xFFFE, 0xE0DD);

    public Tag(ushort group, ushort element)
    {
        Group = group;
        Element = element;
    }

    /// <summary>The group number.</summary>
    public ushort Group { get; }

    /// <summary>The element number.</summary>
    public ushort Element { get; }

    /// <summary>The tag as a single 32-bit value, with the group in the high half.</summary>
    public uint Value => ((uint)Group << 16) | Element;

    /// <summary>Whether the group number is odd, which marks the attribute as private.</summary>
    public bool IsPrivate => (Group & 1) == 1;

    /// <summary>Whether this is a group length attribute, with an element number of zero.</summary>
    public bool IsGroupLength => Element == 0;

    /// <summary>
    /// Whether this is one of the three item or sequence delimiter tags, which carry no value representation.
    /// </summary>
    public bool IsDelimiter => Group == 0xFFFE;

    /// <summary>
    /// Whether this is a private creator attribute, which reserves a block of private element numbers.
    /// </summary>
    public bool IsPrivateCreator => IsPrivate && Element >= 0x0010 && Element <= 0x00FF;

    /// <summary>
    /// The canonical form of a repeating group tag, where the variable part of the group is zeroed.
    /// Overlay tags use group 60xx and curve tags use group 50xx.
    /// </summary>
    public Tag Canonical()
    {
        if (Group is >= 0x6000 and <= 0x60FF && (Group & 1) == 0)
            return new Tag(0x6000, Element);

        if (Group is >= 0x5000 and <= 0x50FF && (Group & 1) == 0)
            return new Tag(0x5000, Element);

        return this;
    }

    public static Tag FromValue(uint value) => new((ushort)(value >> 16), (ushort)(value & 0xFFFF));

    /// <summary>
    /// Parses a tag written as <c>(gggg,eeee)</c> or <c>ggggeeee</c>.
    /// </summary>
    public static bool TryParse(string? text, out Tag tag)
    {
        tag = default;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        ReadOnlySpan<char> span = text.AsSpan().Trim();

        if (span.Length > 1 && span[0] == '(' && span[^1] == ')')
            span = span[1..^1];

        int comma = span.IndexOf(',');

        ReadOnlySpan<char> groupSpan;
        ReadOnlySpan<char> elementSpan;

        if (comma >= 0)
        {
            groupSpan = span[..comma].Trim();
            elementSpan = span[(comma + 1)..].Trim();
        }
        else if (span.Length == 8)
        {
            groupSpan = span[..4];
            elementSpan = span[4..];
        }
        else
        {
            return false;
        }

        if (!ushort.TryParse(groupSpan, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort group))
            return false;

        if (!ushort.TryParse(elementSpan, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort element))
            return false;

        tag = new Tag(group, element);
        return true;
    }

    public static Tag Parse(string text) =>
        TryParse(text, out Tag tag) ? tag : throw new FormatException($"'{text}' is not a valid DICOM tag.");

    public bool Equals(Tag other) => Group == other.Group && Element == other.Element;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Tag other && Equals(other);

    public override int GetHashCode() => (int)Value;

    public int CompareTo(Tag other) => Value.CompareTo(other.Value);

    /// <summary>Formats the tag. Use "G" for <c>(gggg,eeee)</c> and "X" for <c>ggggeeee</c>.</summary>
    public string ToString(string? format, IFormatProvider? formatProvider) => format switch
    {
        "X" or "x" => $"{Group:X4}{Element:X4}",
        _ => $"({Group:X4},{Element:X4})",
    };

    public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

    public static bool operator ==(Tag left, Tag right) => left.Equals(right);

    public static bool operator !=(Tag left, Tag right) => !left.Equals(right);

    public static bool operator <(Tag left, Tag right) => left.CompareTo(right) < 0;

    public static bool operator <=(Tag left, Tag right) => left.CompareTo(right) <= 0;

    public static bool operator >(Tag left, Tag right) => left.CompareTo(right) > 0;

    public static bool operator >=(Tag left, Tag right) => left.CompareTo(right) >= 0;
}
