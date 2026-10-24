using System.Globalization;
using System.Text;

namespace Dicom.Core;

/// <summary>
/// Conversions from the stored representation to the type a caller asks for.
/// Text values are parsed, numbers are widened or narrowed, and failures raise
/// <see cref="DicomValueException"/> rather than returning a silent default.
/// </summary>
public sealed partial class PrimitiveValue
{
    /// <summary>The character separating the individual values of a multi valued text attribute.</summary>
    public const char ValueSeparator = '\\';

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// The value at the given position, boxed. Returns null when the position is out of range.
    /// </summary>
    public object? ElementAt(int index)
    {
        if (index < 0 || index >= Multiplicity)
            return null;

        return Kind == PrimitiveValueKind.Text ? _payload : ((Array)_payload!).GetValue(index);
    }

    #region Text

    /// <summary>
    /// The first value as text, with trailing padding removed. Numbers are formatted
    /// using the invariant culture.
    /// </summary>
    public string ToStringValue() => ToStringValue(0);

    /// <summary>The value at the given position as text, with trailing padding removed.</summary>
    public string ToStringValue(int index) => Format(ElementAt(index)).TrimEnd('\0', ' ');

    /// <summary>All values as text, joined by a backslash as they appear on the wire.</summary>
    public string ToMultiStringValue()
    {
        if (Kind == PrimitiveValueKind.Text)
            return ((string)_payload!).TrimEnd('\0');

        int count = Multiplicity;

        if (count == 0)
            return string.Empty;

        if (count == 1)
            return ToStringValue(0);

        StringBuilder builder = new();

        for (int i = 0; i < count; i++)
        {
            if (i > 0)
                builder.Append(ValueSeparator);

            builder.Append(ToStringValue(i));
        }

        return builder.ToString();
    }

    /// <summary>Every value as a separate string, with trailing padding removed.</summary>
    public string[] ToStringArray()
    {
        int count = Multiplicity;

        if (count == 0)
            return NoStrings;

        string[] result = new string[count];

        for (int i = 0; i < count; i++)
            result[i] = ToStringValue(i);

        return result;
    }

    private static string Format(object? element) => element switch
    {
        null => string.Empty,
        string s => s,
        Tag t => t.ToString(),
        float f => f.ToString("R", Invariant),
        double d => d.ToString("R", Invariant),
        IFormattable f => f.ToString(null, Invariant),
        _ => element.ToString() ?? string.Empty,
    };

    #endregion

    #region Numbers

    public long ToInt64(int index = 0) =>
        TryToInt64(index, out long value) ? value : throw Failure(index, "an integer");

    public int ToInt32(int index = 0) => checked((int)ToInt64(index));

    public short ToInt16(int index = 0) => checked((short)ToInt64(index));

    public ushort ToUInt16(int index = 0) => checked((ushort)ToInt64(index));

    public uint ToUInt32(int index = 0) => checked((uint)ToInt64(index));

    public double ToDouble(int index = 0) =>
        TryToDouble(index, out double value) ? value : throw Failure(index, "a number");

    public float ToSingle(int index = 0) => (float)ToDouble(index);

    /// <summary>Attempts to read the value at the given position as an integer.</summary>
    public bool TryToInt64(int index, out long value)
    {
        value = 0;
        object? element = ElementAt(index);

        switch (element)
        {
            case null:
                return false;
            case string s:
                return long.TryParse(s.Trim().TrimEnd('\0'), NumberStyles.Integer, Invariant, out value)
                    || (double.TryParse(s.Trim().TrimEnd('\0'), NumberStyles.Float, Invariant, out double d)
                        && TryNarrow(d, out value));
            case Tag tag:
                value = tag.Value;
                return true;
            case float f:
                return TryNarrow(f, out value);
            case double d2:
                return TryNarrow(d2, out value);
            case ulong u when u > long.MaxValue:
                return false;
            case IConvertible convertible:
                value = convertible.ToInt64(Invariant);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Attempts to read the value at the given position as a number.</summary>
    public bool TryToDouble(int index, out double value)
    {
        value = 0;
        object? element = ElementAt(index);

        switch (element)
        {
            case null:
                return false;
            case string s:
                return double.TryParse(s.Trim().TrimEnd('\0'), NumberStyles.Float, Invariant, out value);
            case Tag tag:
                value = tag.Value;
                return true;
            case IConvertible convertible:
                value = convertible.ToDouble(Invariant);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Every value as an integer.</summary>
    public int[] ToInt32Array() => ToArray(static (v, i) => v.ToInt32(i));

    /// <summary>Every value as a signed 16-bit integer.</summary>
    public short[] ToInt16Array() => ToArray(static (v, i) => v.ToInt16(i));

    /// <summary>Every value as an unsigned 16-bit integer.</summary>
    public ushort[] ToUInt16Array() => ToArray(static (v, i) => v.ToUInt16(i));

    /// <summary>Every value as an unsigned 32-bit integer.</summary>
    public uint[] ToUInt32Array() => ToArray(static (v, i) => v.ToUInt32(i));

    /// <summary>Every value as a signed 64-bit integer.</summary>
    public long[] ToInt64Array() => ToArray(static (v, i) => v.ToInt64(i));

    /// <summary>Every value as an unsigned 64-bit integer.</summary>
    public ulong[] ToUInt64Array() => ToArray(static (v, i) => checked((ulong)v.ToInt64(i)));

    /// <summary>Every value as a number.</summary>
    public double[] ToDoubleArray() => ToArray(static (v, i) => v.ToDouble(i));

    /// <summary>Every value as a single precision number.</summary>
    public float[] ToSingleArray() => ToArray(static (v, i) => v.ToSingle(i));

    private T[] ToArray<T>(Func<PrimitiveValue, int, T> convert)
    {
        int count = Multiplicity;
        T[] result = new T[count];

        for (int i = 0; i < count; i++)
            result[i] = convert(this, i);

        return result;
    }

    private static bool TryNarrow(double source, out long value)
    {
        if (double.IsNaN(source) || source < long.MinValue || source > long.MaxValue)
        {
            value = 0;
            return false;
        }

        value = (long)source;
        return true;
    }

    #endregion

    #region Bytes

    /// <summary>
    /// The value as raw bytes in little endian order. Text is encoded with the default repertoire
    /// and padded to an even length is left to the writer.
    /// </summary>
    public byte[] ToBytes()
    {
        switch (Kind)
        {
            case PrimitiveValueKind.Empty:
                return [];
            case PrimitiveValueKind.U8:
                return (byte[])_payload!;
            case PrimitiveValueKind.Text:
            case PrimitiveValueKind.Strings:
                return VrExtensions.DefaultEncoding.GetBytes(ToMultiStringValue());
            case PrimitiveValueKind.Tags:
                return TagBytes((Tag[])_payload!);
            default:
                Array array = (Array)_payload!;
                byte[] buffer = new byte[Buffer.ByteLength(array)];
                Buffer.BlockCopy(array, 0, buffer, 0, buffer.Length);
                return buffer;
        }
    }

    private static byte[] TagBytes(Tag[] tags)
    {
        byte[] buffer = new byte[tags.Length * 4];

        for (int i = 0; i < tags.Length; i++)
        {
            Span<byte> slot = buffer.AsSpan(i * 4, 4);
            BitConverter.TryWriteBytes(slot[..2], tags[i].Group);
            BitConverter.TryWriteBytes(slot[2..], tags[i].Element);
        }

        return buffer;
    }

    #endregion

    #region Dates and times

    /// <summary>The value at the given position parsed as a DICOM date.</summary>
    public DateOnly ToDate(int index = 0) =>
        DicomDateTime.TryParseDate(ToStringValue(index), out DateOnly date)
            ? date
            : throw Failure(index, "a date");

    /// <summary>The value at the given position parsed as a DICOM time.</summary>
    public TimeOnly ToTime(int index = 0) =>
        DicomDateTime.TryParseTime(ToStringValue(index), out TimeOnly time)
            ? time
            : throw Failure(index, "a time");

    /// <summary>The value at the given position parsed as a DICOM date time.</summary>
    public DateTimeOffset ToDateTime(int index = 0) =>
        DicomDateTime.TryParseDateTime(ToStringValue(index), out DateTimeOffset moment)
            ? moment
            : throw Failure(index, "a date time");

    #endregion

    /// <summary>A short description of the content, used in diagnostics.</summary>
    public string ToShortString()
    {
        if (Kind == PrimitiveValueKind.Empty)
            return string.Empty;

        string text = ToMultiStringValue();

        return text.Length <= 64 ? text : string.Concat(text.AsSpan(0, 61), "...");
    }

    public override string ToString() => ToShortString();

    private DicomValueException Failure(int index, string target) =>
        new($"The value at index {index} of a {Kind} value is not {target}.");
}
