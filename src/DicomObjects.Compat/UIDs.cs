namespace DicomObjects;

/// <summary>
/// A unique identifier. The storage classes are identifiers of this type rather than plain
/// strings, as they are in the original product, so that a caller can read <see cref="Value"/>
/// or pass one wherever a string is expected.
/// </summary>
public sealed class UID : IEquatable<UID>
{
    public UID(string value) => Value = value;

    /// <summary>The identifier itself.</summary>
    public string Value { get; }

    public static implicit operator string(UID uid) => uid.Value;

    public bool Equals(UID? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj switch
    {
        UID uid => Equals(uid),
        string text => string.Equals(Value, text, StringComparison.Ordinal),
        _ => false,
    };

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}

/// <summary>
/// The unique identifiers of the storage classes and transfer syntaxes referred to by callers.
/// </summary>
public static class UIDs
{
    /// <summary>The storage classes of the images this library handles.</summary>
    public static class SOPClasses
    {
        public static readonly UID ComputedRadiography = new("1.2.840.10008.5.1.4.1.1.1");
        public static readonly UID DigitalXRay_Presentation = new("1.2.840.10008.5.1.4.1.1.1.1");
        public static readonly UID DigitalXRay_Processing = new("1.2.840.10008.5.1.4.1.1.1.1.1");
        public static readonly UID DigitalMammographyXRay_Presentation = new("1.2.840.10008.5.1.4.1.1.1.2");
        public static readonly UID CT = new("1.2.840.10008.5.1.4.1.1.2");
        public static readonly UID EnhancedCTImage = new("1.2.840.10008.5.1.4.1.1.2.1");
        public static readonly UID Ultrasound = new("1.2.840.10008.5.1.4.1.1.6.1");
        public static readonly UID SecondaryCapture = new("1.2.840.10008.5.1.4.1.1.7");
        public static readonly UID MultiFrameGrayscaleByteSecondaryCapture = new("1.2.840.10008.5.1.4.1.1.7.2");
        public static readonly UID MultiFrameGrayscaleWordSecondaryCapture = new("1.2.840.10008.5.1.4.1.1.7.3");
        public static readonly UID MultiFrameTrueColorSecondaryCapture = new("1.2.840.10008.5.1.4.1.1.7.4");
        public static readonly UID MultiFrameTrueColorSC = MultiFrameTrueColorSecondaryCapture;
        public static readonly UID EncapsulatedPDF = new("1.2.840.10008.5.1.4.1.1.104.1");
        public static readonly UID MR = new("1.2.840.10008.5.1.4.1.1.4");
        public static readonly UID EnhancedMRImage = new("1.2.840.10008.5.1.4.1.1.4.1");
        public static readonly UID NuclearMedicine = new("1.2.840.10008.5.1.4.1.1.20");
        public static readonly UID PositronEmissionTomography = new("1.2.840.10008.5.1.4.1.1.128");
        public static readonly UID XRayAngiographic = new("1.2.840.10008.5.1.4.1.1.12.1");
        public static readonly UID EnhancedXAImage = new("1.2.840.10008.5.1.4.1.1.12.1.1");
        public static readonly UID XRayRadiofluoroscopic = new("1.2.840.10008.5.1.4.1.1.12.2");
        public static readonly UID EnhancedXRFImage = new("1.2.840.10008.5.1.4.1.1.12.2.1");
        public static readonly UID XRay3DAngiographicImage = new("1.2.840.10008.5.1.4.1.1.13.1.1");
        public static readonly UID VideoEndoscopic = new("1.2.840.10008.5.1.4.1.1.77.1.1.1");
        public static readonly UID GrayscaleSoftcopyPresentationState = new("1.2.840.10008.5.1.4.1.1.11.1");
        public static readonly UID XA_XRFGrayscaleSoftcopyPresentationState = new("1.2.840.10008.5.1.4.1.1.11.5");
        public static readonly UID Verification = new("1.2.840.10008.1.1");
    }

    /// <summary>The transfer syntaxes referred to by callers.</summary>
    public static class TransferSyntaxes
    {
        public const string ImplicitVRLittleEndian = "1.2.840.10008.1.2";
        public const string ExplicitVRLittleEndian = "1.2.840.10008.1.2.1";
        public const string DeflatedExplicitVRLittleEndian = "1.2.840.10008.1.2.1.99";
        public const string ExplicitVRBigEndian = "1.2.840.10008.1.2.2";
        public const string JPEGBaseline = "1.2.840.10008.1.2.4.50";
        public const string JPEGExtended = "1.2.840.10008.1.2.4.51";
        public const string JPEGLossless = "1.2.840.10008.1.2.4.57";
        public const string JPEGLosslessFirstOrder = "1.2.840.10008.1.2.4.70";
        public const string JPEGLSLossless = "1.2.840.10008.1.2.4.80";
        public const string JPEGLSLossy = "1.2.840.10008.1.2.4.81";
        public const string JPEG2000Lossless = "1.2.840.10008.1.2.4.90";
        public const string JPEG2000 = "1.2.840.10008.1.2.4.91";
        public const string RLELossless = "1.2.840.10008.1.2.5";
    }
}
