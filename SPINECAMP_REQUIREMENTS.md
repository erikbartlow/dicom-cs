# SpineCAMP requirements for DicomObjects.Compat

This note lists what DICOM_NET10 needs before it can replace DicomObjects.Core 10 in the SpineCAMP analysis path (AISpineAnalysis and the projects it calls: DCMMCProcessor, DICOMHelper, ReportPostProcessor) on the `8609_NET10_Upgrade` branch. Items are in priority order. Items 1 to 3 block running an analysis at all; the rest can follow.

## How this was assessed (2026-10-23)

- The source of AISpineAnalysis, DCMMCProcessor, DICOMHelper, and ReportPostProcessor was compiled in one scratch project against `DicomObjects.Compat` instead of the DicomObjects.Core package, excluding `DCMMCProcessor\DCMTransfer.cs` (DICOM networking, not used by the analysis). That produced 35 compile errors, listed under items 4 and 6.
- The three lumbar test studies in `C:\Public Access\MMDx\Sites\TestingSource` (`Lumbar Flexion`, `Lumbar Extension`, `Lumbar Neutral Lateral [01-Jan-2024 08.00.00].dcm`) were read with the facade. Headers parse correctly (35 attributes each). All three are 16-bit MONOCHROME2 Computed Radiography stored as JPEG Lossless, Non-Hierarchical, First-Order Prediction (`1.2.840.10008.1.2.4.70`), the same syntax SpineCAMP stores source images in.
- The solution builds with no warnings and its 44 tests pass.

Line numbers below refer to `D:\_work\SpineCAMP\Src` on `8609_NET10_Upgrade` as of this date.

## 1. Decode JPEG Lossless SV1 pixel data (blocking)

**Need:** `DicomImage.RawPixelValuesByFrame(frame)` must return the decoded sample values for pixel data encapsulated as `1.2.840.10008.1.2.4.70`, as it already does for native pixel data. Today it throws `NotSupportedException: The pixel data is encapsulated and needs a decoder.`

**Where SpineCAMP calls it:** `DCMMCProcessor\DCMReader.cs:1348` (`ReaderImage.RawPixelValuesByFrame(iFrame + 1)`). Every image the analysis looks at goes through this call: `DCMReader.GetImage` builds the Bitmaps used for image identification, stabilization, overlays, and the report images (for example `AISpineAnalysis\ReportBuilder.cs:503`).

**Acceptance:**
- The three test studies decode without error, returning `ushort[]` of Rows x Columns samples (for example 2819 x 1758 for Lumbar Flexion).
- The decoded values match DicomObjects exactly. A reference can be produced with DicomObjects v8 under .NET Framework (Windows PowerShell 5.1), which is licensed on developer machines.
- Bits Stored 16, High Bit 15, Pixel Representation 0, single frame, one fragment per frame are the cases in the test data. Multi-fragment frames and the basic offset table should also be handled, because other modalities and vendors send them.

**Later, lower priority:** other syntaxes facilities may send. `DCMMCProcessor\DCMUtility.cs:3130` has special handling for JPEG 2000 Lossless (`1.2.840.10008.1.2.4.90`) at 16 bits, so that syntax has been seen in practice. JPEG Lossless Process 14 (`.57`), JPEG Baseline (`.50`), and RLE Lossless (`.5`) are the other likely ones.

## 2. Encode JPEG Baseline on write (blocking for report output)

**Need:** `DicomDataSet.Write(...)` and `DicomImage.Write(...)` with `UIDs.TransferSyntaxes.JPEGBaseline` (`1.2.840.10008.1.2.4.50`) must compress native 8-bit RGB pixel data, including multi-frame data. Today `Write` throws `NotSupportedException` ("this library does not transcode") when the target syntax is encapsulated and the pixel data is native.

**Where SpineCAMP calls it:** `DCMWriter.Create(Bitmap[], string)` (`DCMMCProcessor\DCMWriter.cs:816`) puts uncompressed RGB bytes in `RawPixels` (line 858), sets Number of Frames, and calls `WriterImage.Write(out byte[] arrDCM, sTransferSyntax, 100)` (line 862). It is called with JPEG Baseline for every DICOM report series:
- `AISpineAnalysis\DICOMReports.cs:236` (`BuildDICOMSeries`, multi-frame true color secondary capture)
- `AISpineAnalysis\DICOMReports.cs:336` (`BuildDICOMAlignment`)
- `AISpineAnalysis\DICOMReports.cs:449` (`BuildDICOMSeparatedImages`)
- `ReportPostProcessor\ReportProcessor.cs:291` (report pages as images)

**Acceptance:**
- Samples per Pixel 3, Bits Allocated 8, Planar Configuration 0, one or more frames, encoded as one fragment per frame.
- The `quality` argument (100 in SpineCAMP) is honored.
- Photometric Interpretation is set to match the encoded data (PS3.5 section 8.2.1 expects YBR_FULL_422 for lossy JPEG of color images; follow whatever DicomObjects produces so viewers behave the same).
- Lossy Image Compression (0028,2110) and, where DicomObjects sets them, the ratio and method attributes are written.
- The output opens in a standard viewer and matches a DicomObjects-produced file visually.

## 3. Never write encapsulated fragments under a native transfer syntax (blocking, data integrity)

**Need:** when the pixel data is encapsulated (for example after reading a JPEG Lossless file) and `Write` is asked for a native syntax such as Explicit VR Little Endian, the library must decode the pixels first (using item 1). If it can't decode them, it must throw. Today it writes the compressed fragments under the uncompressed transfer syntax, which produces an invalid file without any error.

**Where SpineCAMP does this:**
- `ReportLayoutSampleRunner\Program.cs:164` and `RunAISpineAnalysis\Program.cs:167`: each submitted image is rewritten as Explicit VR Little Endian before analysis.
- `DCMMCProcessor\DCMWriter.cs:708` and `:739` (`Save` with Explicit VR Little Endian)
- `AISpineAnalysis\ReportBuilder.cs:262` and `AISpineAnalysis\DICOMReports.cs:130` write Explicit VR Little Endian. These carry no image pixels (encapsulated PDF), but they show that native writes are routine.

**Acceptance:** read a test study, write it as Explicit VR Little Endian, read it back, and get the same `RawPixelValuesByFrame` values as decoding the original, with Transfer Syntax UID `1.2.840.10008.1.2.1` and Pixel Data stored natively (VR OW, defined length).

## 4. API members SpineCAMP compiles against

These are the remaining compile errors (other than the licence and networking types in items 6 and 7). Most can be small additions.

| Member | Used at | Suggested behavior |
|---|---|---|
| `DicomDataSet.ValidationOptions` and `DicomObjects.Validation.ValidationOptions.None` | `AISpineAnalysis\DICOMReports.cs:124, 232, 332, 445`; `AISpineAnalysis\ProcessImages.cs:1320, 1405`; `ReportPostProcessor\ReportProcessor.cs:286` | A settable property and enum; no validation is performed, so it can be recorded only |
| `UIDs.SOPClasses.EncapsulatedPDF` (`1.2.840.10008.5.1.4.1.1.104.1`) | `AISpineAnalysis\DICOMReports.cs:76` | Constant |
| `UIDs.SOPClasses.MultiFrameTrueColorSC` (`1.2.840.10008.5.1.4.1.1.7.4`) | `AISpineAnalysis\DICOMReports.cs:172`; `ReportPostProcessor\ReportProcessor.cs:226` | Same value as the existing `MultiFrameTrueColorSecondaryCapture`, under the DicomObjects name |
| `DicomImage[int group, int element]` indexer | `DCMMCProcessor\DCMAnonymizer.cs:1777`; `DCMMCProcessor\DCMReader.cs:3244 to 3246` | Returns `DataSet[group, element]` |
| `DicomImage` usable where a `DicomDataSet` is expected | `DCMMCProcessor\DCMReader.cs:1125`; `DCMMCProcessor\DCMWriter.cs:659, 674, 1230` (passed to `DCMUtility.SetTags(DicomDataSet, ...)`) | In DicomObjects, `DicomImage` derives from `DicomDataSet`. Either derive the same way or add an implicit conversion to `DicomDataSet` that returns `DataSet` |
| `DicomAttribute.IsMultiple` | `DCMMCProcessor\DCMUtility.cs:487, 3534` | True when VM is greater than 1, so that `Value` returns an array |
| `DicomAttribute.Description` | `DCMMCProcessor\DCMUtility.cs:599` (on the data set from `DicomGlobal.MakeDictionary()`) | The dictionary name of the attribute |
| `ReadBehaviours.CloseFilesAfterReading` | `DCMMCProcessor\DCMUtility.cs:2858` | Settable property; files are already read fully and closed |

## 5. Return DA, TM, and DT values as DateTime

**Need:** DicomObjects returns `DateTime` from `DicomAttribute.Value` and `DicomDataSet.Value` for DA, TM, and DT attributes. The facade returns the raw string (for example `"20240101"` and `"080000.0000"`).

**Effect in SpineCAMP:** `DCMUtility.GetDateTime` (`DCMMCProcessor\DCMUtility.cs:444`) and `GetSafeDateTime` (line 462) only accept a `DateTime`, so with the facade they return null or 1 January 1900. That affects the dates `DCMReader` exposes (for example birth date, study, series, and acquisition times) and the dates copied into anonymized and report data sets. AISpineAnalysis's own `Utility.GetTag` converts DA and TM strings, so its study date and time are unaffected.

**Acceptance:** match DicomObjects for single and multi-valued DA, TM, and DT, including partial times (`0800`, `080000`, `080000.0000`) and empty values (null). IS and DS strings can stay as they are: `GetSafeInt` and `GetSafeDouble` convert strings correctly, as long as the strings are trimmed (they are today).

## 6. Licensing types (can be dropped from SpineCAMP)

`DCMMCProcessor\DCMLicenseChecker.cs:33, 64, 105` use `DicomLicence`. The facade needs no licence, so SpineCAMP can make `ValidLicense()` return true (and the install and remove methods no-ops) when built against it. No library work is needed unless a drop-in stub is preferred.

## 7. Encode JPEG Lossless SV1 on write (production storage, not needed for the dry-run sample)

**Need:** `Write` with `UIDs.TransferSyntaxes.JPEGLosslessFirstOrder` (`1.2.840.10008.1.2.4.70`) must compress native 8 and 16-bit monochrome pixel data, so SpineCAMP can store source images as it does today.

**Where SpineCAMP calls it:** `AISpineAnalysis\ProcessImages.cs:1325` (`StoreSourceImages`) and `:1406` (`StoreSourceImage`), and `DCMMCProcessor\DCMWriter.cs:737` (`Save` with compression). The layout sample runs in a dry run that skips these writes, so this item can come last.

**Acceptance:** a round trip (decode with item 1, encode, decode again) reproduces the samples exactly, and DicomObjects can read the output.

## Follow-up after the codec update (2026-10-23 evening)

Retested against the updated solution (60 tests passing):

- **Items 1, 3, and 7 verified.** On the three test studies, decoded JPEG Lossless pixels are identical to DicomObjects v8 (SHA-256 over all samples). Writing as Explicit VR Little Endian now decodes first. Re-encoding as JPEG Lossless round-trips exactly, and DicomObjects v8 reads the re-encoded files to the same values.
- **Item 4 is complete apart from one member.** `ReportPostProcessor\ReportProcessor.cs:226` reads `UIDs.SOPClasses.MultiFrameTrueColorSC.Value`. In DicomObjects.Core 10 the SOP class constants are UID objects with a `Value` property; the facade defines them as `const string`. Adding a small UID type with `Value` and an implicit conversion to `string` would cover both usages. Everything else in AISpineAnalysis, DCMMCProcessor, DICOMHelper, and ReportPostProcessor compiles, apart from `DicomLicence` (item 6, handled in SpineCAMP) and the networking file.
- A dry-run analysis of the three studies for facility 2091 ran end to end through the analysis on the facade, with all three images usable and the AI service returning measurements.

## Not needed for the analysis

DICOM networking (`DicomServer`, `DicomAssociation`, `DicomObjects.EventArguments`) is used only by `DCMMCProcessor\DCMTransfer.cs`, which AISpineAnalysis does not call. Other applications (Ranger, Kitchen, Pathfinder, TrailGuide) use DicomObjects too and were not assessed.

## Done when

The SpineCAMP report layout sample runner (`Src\ReportLayoutSampleRunner`) can switch from DicomObjects.Core to DicomObjects.Compat. It can then run a dry-run analysis of the three test studies for facility 2091 against CAMP_EKB, producing the same reports DicomObjects would. Items 1 to 5 are enough for that, with item 6 handled on the SpineCAMP side; item 7 is needed only before production use.
