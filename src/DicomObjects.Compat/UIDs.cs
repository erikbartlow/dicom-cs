namespace DicomObjects;

/// <summary>
/// The unique identifiers of the storage classes and transfer syntaxes referred to by callers.
/// </summary>
public static class UIDs
{
    /// <summary>The storage classes of the images this library handles.</summary>
    public static class SOPClasses
    {
        public const string ComputedRadiography = "1.2.840.10008.5.1.4.1.1.1";
        public const string DigitalXRay_Presentation = "1.2.840.10008.5.1.4.1.1.1.1";
        public const string DigitalXRay_Processing = "1.2.840.10008.5.1.4.1.1.1.1.1";
        public const string DigitalMammographyXRay_Presentation = "1.2.840.10008.5.1.4.1.1.1.2";
        public const string CT = "1.2.840.10008.5.1.4.1.1.2";
        public const string EnhancedCTImage = "1.2.840.10008.5.1.4.1.1.2.1";
        public const string Ultrasound = "1.2.840.10008.5.1.4.1.1.6.1";
        public const string SecondaryCapture = "1.2.840.10008.5.1.4.1.1.7";
        public const string MultiFrameGrayscaleByteSecondaryCapture = "1.2.840.10008.5.1.4.1.1.7.2";
        public const string MultiFrameGrayscaleWordSecondaryCapture = "1.2.840.10008.5.1.4.1.1.7.3";
        public const string MultiFrameTrueColorSecondaryCapture = "1.2.840.10008.5.1.4.1.1.7.4";
        public const string MultiFrameTrueColorSC = MultiFrameTrueColorSecondaryCapture;
        public const string EncapsulatedPDF = "1.2.840.10008.5.1.4.1.1.104.1";
        public const string MR = "1.2.840.10008.5.1.4.1.1.4";
        public const string EnhancedMRImage = "1.2.840.10008.5.1.4.1.1.4.1";
        public const string NuclearMedicine = "1.2.840.10008.5.1.4.1.1.20";
        public const string PositronEmissionTomography = "1.2.840.10008.5.1.4.1.1.128";
        public const string XRayAngiographic = "1.2.840.10008.5.1.4.1.1.12.1";
        public const string EnhancedXAImage = "1.2.840.10008.5.1.4.1.1.12.1.1";
        public const string XRayRadiofluoroscopic = "1.2.840.10008.5.1.4.1.1.12.2";
        public const string EnhancedXRFImage = "1.2.840.10008.5.1.4.1.1.12.2.1";
        public const string XRay3DAngiographicImage = "1.2.840.10008.5.1.4.1.1.13.1.1";
        public const string VideoEndoscopic = "1.2.840.10008.5.1.4.1.1.77.1.1.1";
        public const string GrayscaleSoftcopyPresentationState = "1.2.840.10008.5.1.4.1.1.11.1";
        public const string XA_XRFGrayscaleSoftcopyPresentationState = "1.2.840.10008.5.1.4.1.1.11.5";
        public const string Verification = "1.2.840.10008.1.1";
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
