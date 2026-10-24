namespace Dicom.Core;

/// <summary>
/// Thrown when a data element's value cannot be converted to the requested type.
/// </summary>
public class DicomValueException : Exception
{
    public DicomValueException(string message)
        : base(message)
    {
    }

    public DicomValueException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when DICOM encoded data is malformed and cannot be interpreted.
/// </summary>
public class DicomParseException : Exception
{
    public DicomParseException(string message)
        : base(message)
    {
    }

    public DicomParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
