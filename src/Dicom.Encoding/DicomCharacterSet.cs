using System.Text;

namespace Dicom.Encoding;

/// <summary>
/// Resolves the text encoding named by the Specific Character Set attribute.
/// A data set that states no character set, or one this runtime cannot provide,
/// is read with the default repertoire.
/// </summary>
public static class DicomCharacterSet
{
    private static readonly Dictionary<string, int> CodePages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ISO_IR 6"] = 28591,
        ["ISO_IR 100"] = 28591,
        ["ISO_IR 101"] = 28592,
        ["ISO_IR 109"] = 28593,
        ["ISO_IR 110"] = 28594,
        ["ISO_IR 144"] = 28595,
        ["ISO_IR 127"] = 28596,
        ["ISO_IR 126"] = 28597,
        ["ISO_IR 138"] = 28598,
        ["ISO_IR 148"] = 28599,
        ["ISO_IR 166"] = 874,
        ["ISO_IR 13"] = 932,
        ["ISO_IR 192"] = 65001,
        ["GB18030"] = 54936,
        ["GBK"] = 936,
        ["ISO 2022 IR 6"] = 28591,
        ["ISO 2022 IR 100"] = 28591,
        ["ISO 2022 IR 101"] = 28592,
        ["ISO 2022 IR 109"] = 28593,
        ["ISO 2022 IR 110"] = 28594,
        ["ISO 2022 IR 144"] = 28595,
        ["ISO 2022 IR 127"] = 28596,
        ["ISO 2022 IR 126"] = 28597,
        ["ISO 2022 IR 138"] = 28598,
        ["ISO 2022 IR 148"] = 28599,
        ["ISO 2022 IR 13"] = 932,
        ["ISO 2022 IR 87"] = 50222,
        ["ISO 2022 IR 159"] = 50222,
        ["ISO 2022 IR 149"] = 949,
        ["ISO 2022 IR 58"] = 936,
    };

    /// <summary>The encoding used when no character set is stated.</summary>
    public static System.Text.Encoding Default { get; } = System.Text.Encoding.Latin1;

    /// <summary>
    /// The encoding for a Specific Character Set value. Multi valued declarations select the
    /// first named set, since code extension escape sequences are not interpreted here.
    /// </summary>
    public static System.Text.Encoding Resolve(string? specificCharacterSet)
    {
        if (string.IsNullOrWhiteSpace(specificCharacterSet))
            return Default;

        foreach (string term in specificCharacterSet.Split('\\'))
        {
            string name = term.Trim().TrimEnd('\0');

            if (name.Length == 0)
                continue;

            if (!CodePages.TryGetValue(name, out int codePage))
                continue;

            System.Text.Encoding? encoding = TryGet(codePage);

            if (encoding is not null)
                return encoding;
        }

        return Default;
    }

    private static System.Text.Encoding? TryGet(int codePage)
    {
        if (codePage == 65001)
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        if (codePage == 28591)
            return System.Text.Encoding.Latin1;

        try
        {
            return System.Text.Encoding.GetEncoding(codePage);
        }
        catch (NotSupportedException)
        {
            // The runtime provides only a few code pages unless an encoding provider is registered.
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
