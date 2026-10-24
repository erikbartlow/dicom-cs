using System.Globalization;
using Dicom.Core;

namespace DicomObjects;

/// <summary>
/// Translates between the loosely typed values this interface exposes and the typed values
/// the underlying library stores.
/// </summary>
internal static class ValueInterop
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>The value of an element as the caller expects to see it.</summary>
    public static object? ToObject(DataElement element)
    {
        switch (element.Value)
        {
            case SequenceDicomValue sequence:
                return DicomDataSetCollection.Wrap(sequence.Items);

            case PixelSequenceDicomValue pixels:
                return pixels.Fragments.Count == 0 ? null : pixels.Fragments[0];
        }

        PrimitiveValue value = element.Primitive;

        if (value.IsEmpty)
            return null;

        if (element.Vr is Vr.DA or Vr.TM or Vr.DT)
        {
            if (IsBlank(value))
                return null;

            if (ToMoment(value, element.Vr) is { } moment)
                return moment;
        }

        return value.Kind switch
        {
            PrimitiveValueKind.Text => value.ToStringValue(),
            PrimitiveValueKind.Strings => Single(value.ToStringArray()),
            PrimitiveValueKind.Tags => Single(value.ToStringArray()),
            PrimitiveValueKind.U8 => value.AsBytes(),
            PrimitiveValueKind.I16 => Single(value.AsInt16()),
            PrimitiveValueKind.U16 => Single(value.AsUInt16()),
            PrimitiveValueKind.I32 => Single(value.AsInt32()),
            PrimitiveValueKind.U32 => Single(value.AsUInt32()),
            PrimitiveValueKind.I64 => Single(value.AsInt64()),
            PrimitiveValueKind.U64 => Single(value.AsUInt64()),
            PrimitiveValueKind.F32 => Single(value.AsSingle()),
            PrimitiveValueKind.F64 => Single(value.AsDouble()),
            _ => null,
        };
    }

    /// <summary>The value a caller supplied, converted for storage under the given representation.</summary>
    public static PrimitiveValue FromObject(object? value, Vr vr)
    {
        switch (value)
        {
            case null:
                return PrimitiveValue.Empty;

            case PrimitiveValue primitive:
                return primitive;

            case string text:
                return PrimitiveValue.FromString(text);

            case string[] texts:
                return PrimitiveValue.FromStrings(texts);

            case byte[] bytes when !vr.IsText():
                return PrimitiveValue.FromBytes(bytes);

            case DateTime moment:
                return PrimitiveValue.FromString(FormatMoment(moment, vr));

            case DateTimeOffset moment:
                return PrimitiveValue.FromString(FormatMoment(moment.DateTime, vr));
        }

        if (vr.IsText())
            return value is Array array ? PrimitiveValue.FromStrings(Texts(array)) : PrimitiveValue.FromString(Text(value));

        return value switch
        {
            short[] v => PrimitiveValue.FromInt16(v),
            ushort[] v => PrimitiveValue.FromUInt16(v),
            int[] v => PrimitiveValue.FromInt32(v),
            uint[] v => PrimitiveValue.FromUInt32(v),
            long[] v => PrimitiveValue.FromInt64(v),
            ulong[] v => PrimitiveValue.FromUInt64(v),
            float[] v => PrimitiveValue.FromSingle(v),
            double[] v => PrimitiveValue.FromDouble(v),
            byte[] v => PrimitiveValue.FromBytes(v),
            short v => PrimitiveValue.FromInt16([v]),
            ushort v => PrimitiveValue.FromUInt16([v]),
            int v => PrimitiveValue.FromInt32([v]),
            uint v => PrimitiveValue.FromUInt32([v]),
            long v => PrimitiveValue.FromInt64([v]),
            ulong v => PrimitiveValue.FromUInt64([v]),
            float v => PrimitiveValue.FromSingle([v]),
            double v => PrimitiveValue.FromDouble([v]),
            bool v => PrimitiveValue.FromUInt16([(ushort)(v ? 1 : 0)]),
            Array array => PrimitiveValue.FromStrings(Texts(array)),
            _ => PrimitiveValue.FromString(Text(value)),
        };
    }

    /// <summary>
    /// A date, time or date and time value as a <see cref="DateTime"/>, or null when the text
    /// does not parse, in which case the caller falls back to returning it as it is stored.
    /// A time carries no date, so it is returned on the earliest date.
    /// </summary>
    /// <summary>Whether a date or time attribute is present but holds nothing but padding.</summary>
    private static bool IsBlank(PrimitiveValue value)
    {
        foreach (string text in value.ToStringArray())
        {
            if (text.Trim().TrimEnd('\0').Length > 0)
                return false;
        }

        return true;
    }

    private static object? ToMoment(PrimitiveValue value, Vr vr)
    {
        string[] texts = value.ToStringArray();

        if (texts.Length == 0)
            return null;

        DateTime[] moments = new DateTime[texts.Length];

        for (int i = 0; i < texts.Length; i++)
        {
            string text = texts[i].Trim().TrimEnd('\0');

            if (text.Length == 0)
                return null;

            switch (vr)
            {
                case Vr.DA when DicomDateTime.TryParseDate(text, out DateOnly date):
                    moments[i] = date.ToDateTime(TimeOnly.MinValue);
                    break;

                case Vr.TM when DicomDateTime.TryParseTime(text, out TimeOnly time):
                    moments[i] = DateTime.MinValue.Date.Add(time.ToTimeSpan());
                    break;

                case Vr.DT when DicomDateTime.TryParseDateTime(text, out DateTimeOffset instant):
                    moments[i] = instant.DateTime;
                    break;

                default:
                    return null;
            }
        }

        return moments.Length == 1 ? moments[0] : moments;
    }

    private static object Single(Array values) => values.Length == 1 ? values.GetValue(0)! : values;

    private static string[] Texts(Array array)
    {
        string[] texts = new string[array.Length];

        for (int i = 0; i < array.Length; i++)
            texts[i] = Text(array.GetValue(i));

        return texts;
    }

    private static string Text(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        float f => f.ToString("R", Invariant),
        double d => d.ToString("R", Invariant),
        IFormattable formattable => formattable.ToString(null, Invariant),
        _ => value.ToString() ?? string.Empty,
    };

    private static string FormatMoment(DateTime moment, Vr vr) => vr switch
    {
        Vr.DA => DicomDateTime.Format(DateOnly.FromDateTime(moment)),
        Vr.TM => DicomDateTime.Format(TimeOnly.FromDateTime(moment)),
        Vr.DT => DicomDateTime.Format(new DateTimeOffset(moment)),
        _ => moment.ToString("yyyyMMddHHmmss", Invariant),
    };
}
