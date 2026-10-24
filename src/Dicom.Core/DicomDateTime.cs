using System.Globalization;

namespace Dicom.Core;

/// <summary>
/// Reading and writing the date, time and date time formats defined by the standard.
/// Partial precision is accepted on input: a date may state only a year or a year and month,
/// and a time may state only an hour, or an hour and minute, with an optional fraction.
/// </summary>
public static class DicomDateTime
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Parses a DA value, in the form <c>YYYYMMDD</c>. Older files may separate with dots.</summary>
    public static bool TryParseDate(string? text, out DateOnly date)
    {
        date = default;

        ReadOnlySpan<char> span = Clean(text);

        if (span.Length == 10 && span[4] == '.' && span[7] == '.')
            span = string.Concat(span[..4], span[5..7], span[8..]).AsSpan();

        if (span.Length < 4)
            return false;

        if (!int.TryParse(span[..4], NumberStyles.None, Invariant, out int year))
            return false;

        int month = 1;
        int day = 1;

        if (span.Length >= 6 && !int.TryParse(span.Slice(4, 2), NumberStyles.None, Invariant, out month))
            return false;

        if (span.Length >= 8 && !int.TryParse(span.Slice(6, 2), NumberStyles.None, Invariant, out day))
            return false;

        if (year < 1 || year > 9999 || month is < 1 or > 12)
            return false;

        if (day < 1 || day > DateTime.DaysInMonth(year, month))
            return false;

        date = new DateOnly(year, month, day);
        return true;
    }

    /// <summary>Parses a TM value, in the form <c>HHMMSS.FFFFFF</c>. Older files may separate with colons.</summary>
    public static bool TryParseTime(string? text, out TimeOnly time)
    {
        time = default;

        ReadOnlySpan<char> span = Clean(text);

        if (span.Contains(':'))
            span = span.ToString().Replace(":", string.Empty).AsSpan();

        if (span.Length < 2)
            return false;

        int fractionStart = span.IndexOf('.');
        ReadOnlySpan<char> whole = fractionStart < 0 ? span : span[..fractionStart];
        ReadOnlySpan<char> fraction = fractionStart < 0 ? default : span[(fractionStart + 1)..];

        if (!int.TryParse(whole[..2], NumberStyles.None, Invariant, out int hour))
            return false;

        int minute = 0;
        int second = 0;

        if (whole.Length >= 4 && !int.TryParse(whole.Slice(2, 2), NumberStyles.None, Invariant, out minute))
            return false;

        if (whole.Length >= 6 && !int.TryParse(whole.Slice(4, 2), NumberStyles.None, Invariant, out second))
            return false;

        if (hour > 23 || minute > 59 || second > 60)
            return false;

        // A leap second is folded back into the last second of the minute.
        if (second == 60)
            second = 59;

        double fractionOfSecond = 0;

        if (fraction.Length > 0
            && !double.TryParse(string.Concat("0.", fraction), NumberStyles.Float, Invariant, out fractionOfSecond))
            return false;

        time = new TimeOnly(hour, minute, second).Add(TimeSpan.FromSeconds(fractionOfSecond));
        return true;
    }

    /// <summary>
    /// Parses a DT value, in the form <c>YYYYMMDDHHMMSS.FFFFFF&amp;ZZXX</c>, where the offset is optional.
    /// </summary>
    public static bool TryParseDateTime(string? text, out DateTimeOffset moment)
    {
        moment = default;

        ReadOnlySpan<char> span = Clean(text);

        if (span.Length < 4)
            return false;

        TimeSpan offset = TimeSpan.Zero;
        bool hasOffset = false;

        int sign = span.LastIndexOfAny('+', '-');

        if (sign >= 8 && span.Length - sign == 5)
        {
            ReadOnlySpan<char> offsetSpan = span[(sign + 1)..];

            if (!int.TryParse(offsetSpan[..2], NumberStyles.None, Invariant, out int offsetHours)
                || !int.TryParse(offsetSpan[2..], NumberStyles.None, Invariant, out int offsetMinutes))
                return false;

            offset = new TimeSpan(offsetHours, offsetMinutes, 0);

            if (span[sign] == '-')
                offset = offset.Negate();

            hasOffset = true;
            span = span[..sign];
        }

        ReadOnlySpan<char> datePart = span[..Math.Min(8, span.Length)];
        ReadOnlySpan<char> timePart = span.Length > 8 ? span[8..] : default;

        if (!TryParseDate(datePart.ToString(), out DateOnly date))
            return false;

        TimeOnly time = default;

        if (timePart.Length > 0 && !TryParseTime(timePart.ToString(), out time))
            return false;

        moment = hasOffset
            ? new DateTimeOffset(date.ToDateTime(time), offset)
            : new DateTimeOffset(date.ToDateTime(time), TimeSpan.Zero);

        return true;
    }

    /// <summary>Formats a date as a DA value.</summary>
    public static string Format(DateOnly date) => date.ToString("yyyyMMdd", Invariant);

    /// <summary>Formats a time as a TM value, including a fraction only when one is present.</summary>
    public static string Format(TimeOnly time) =>
        time.Millisecond == 0 && time.Microsecond == 0
            ? time.ToString("HHmmss", Invariant)
            : time.ToString("HHmmss.ffffff", Invariant).TrimEnd('0');

    /// <summary>Formats an instant as a DT value, including its offset from UTC.</summary>
    public static string Format(DateTimeOffset moment)
    {
        string offset = moment.Offset == TimeSpan.Zero
            ? "+0000"
            : string.Create(Invariant, $"{(moment.Offset < TimeSpan.Zero ? '-' : '+')}{Math.Abs(moment.Offset.Hours):D2}{Math.Abs(moment.Offset.Minutes):D2}");

        return moment.ToString("yyyyMMddHHmmss.ffffff", Invariant) + offset;
    }

    private static ReadOnlySpan<char> Clean(string? text) =>
        text is null ? default : text.AsSpan().Trim().TrimEnd('\0');
}
