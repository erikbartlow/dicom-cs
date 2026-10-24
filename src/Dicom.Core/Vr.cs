namespace Dicom.Core;

/// <summary>
/// An enumeration of the DICOM value representations, which describe how an attribute's value is encoded.
/// </summary>
public enum Vr : ushort
{
    /// <summary>Application Entity.</summary>
    AE = ('A' << 8) | 'E',
    /// <summary>Age String.</summary>
    AS = ('A' << 8) | 'S',
    /// <summary>Attribute Tag.</summary>
    AT = ('A' << 8) | 'T',
    /// <summary>Code String.</summary>
    CS = ('C' << 8) | 'S',
    /// <summary>Date.</summary>
    DA = ('D' << 8) | 'A',
    /// <summary>Decimal String.</summary>
    DS = ('D' << 8) | 'S',
    /// <summary>Date Time.</summary>
    DT = ('D' << 8) | 'T',
    /// <summary>Floating Point Single.</summary>
    FL = ('F' << 8) | 'L',
    /// <summary>Floating Point Double.</summary>
    FD = ('F' << 8) | 'D',
    /// <summary>Integer String.</summary>
    IS = ('I' << 8) | 'S',
    /// <summary>Long String.</summary>
    LO = ('L' << 8) | 'O',
    /// <summary>Long Text.</summary>
    LT = ('L' << 8) | 'T',
    /// <summary>Other Byte.</summary>
    OB = ('O' << 8) | 'B',
    /// <summary>Other Double.</summary>
    OD = ('O' << 8) | 'D',
    /// <summary>Other Float.</summary>
    OF = ('O' << 8) | 'F',
    /// <summary>Other Long.</summary>
    OL = ('O' << 8) | 'L',
    /// <summary>Other Very Long.</summary>
    OV = ('O' << 8) | 'V',
    /// <summary>Other Word.</summary>
    OW = ('O' << 8) | 'W',
    /// <summary>Person Name.</summary>
    PN = ('P' << 8) | 'N',
    /// <summary>Short String.</summary>
    SH = ('S' << 8) | 'H',
    /// <summary>Signed Long.</summary>
    SL = ('S' << 8) | 'L',
    /// <summary>Sequence of Items.</summary>
    SQ = ('S' << 8) | 'Q',
    /// <summary>Signed Short.</summary>
    SS = ('S' << 8) | 'S',
    /// <summary>Short Text.</summary>
    ST = ('S' << 8) | 'T',
    /// <summary>Signed Very Long.</summary>
    SV = ('S' << 8) | 'V',
    /// <summary>Time.</summary>
    TM = ('T' << 8) | 'M',
    /// <summary>Unlimited Characters.</summary>
    UC = ('U' << 8) | 'C',
    /// <summary>Unique Identifier.</summary>
    UI = ('U' << 8) | 'I',
    /// <summary>Unsigned Long.</summary>
    UL = ('U' << 8) | 'L',
    /// <summary>Unknown.</summary>
    UN = ('U' << 8) | 'N',
    /// <summary>Universal Resource Identifier or Universal Resource Locator.</summary>
    UR = ('U' << 8) | 'R',
    /// <summary>Unsigned Short.</summary>
    US = ('U' << 8) | 'S',
    /// <summary>Unlimited Text.</summary>
    UT = ('U' << 8) | 'T',
    /// <summary>Unsigned Very Long.</summary>
    UV = ('U' << 8) | 'V',
}
