namespace Dicom.Core;

/// <summary>
/// Identifies which representation a <see cref="PrimitiveValue"/> is holding internally.
/// </summary>
public enum PrimitiveValueKind
{
    /// <summary>No value.</summary>
    Empty,

    /// <summary>A list of backslash separated text values.</summary>
    Strings,

    /// <summary>A single text value that is not split on backslashes.</summary>
    Text,

    /// <summary>A list of attribute tags.</summary>
    Tags,

    /// <summary>Raw bytes, used for opaque and unknown values.</summary>
    U8,

    /// <summary>Signed 16-bit integers.</summary>
    I16,

    /// <summary>Unsigned 16-bit integers.</summary>
    U16,

    /// <summary>Signed 32-bit integers.</summary>
    I32,

    /// <summary>Unsigned 32-bit integers.</summary>
    U32,

    /// <summary>Signed 64-bit integers.</summary>
    I64,

    /// <summary>Unsigned 64-bit integers.</summary>
    U64,

    /// <summary>Single precision floating point numbers.</summary>
    F32,

    /// <summary>Double precision floating point numbers.</summary>
    F64,
}
