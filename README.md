# DICOM-CS

A DICOM library for .NET, written as a C# port of the Rust
[DICOM-rs](https://github.com/Enet4/dicom-rs) `dicom-object` crate and the crates it builds on,
plus pixel codecs and a compatibility facade that lets existing callers replace DicomObjects.Core.

- **Product:** DICOM for .NET 10
- **Version:** 0.10.0
- **Targets:** `net48` and `net10.0` (every library carries both)
- **Language:** C# `latest`, nullable enabled, implicit usings enabled
- **Licence:** Apache-2.0 or MIT, at your option

## Why this exists

Applications that read and write DICOM through DicomObjects.Core 10 are blocked on .NET 10:
DicomObjects is licensed per machine and carries no .NET 10 build. `DicomObjects.Compat`
presents the same API under the same `DicomObjects` namespace, so those projects compile and
run against this library instead of the commercial one.

`AUGGIE_FINDINGS.md` records the verification of this library against DicomObjects v8 on real
studies. `done.txt` and `AUGGIE_TASKS.md` record the work itself.

## Project layout

```text
dicom-cs/
├── Dicom.slnx                     Solution (.slnx format)
├── Directory.Build.props          net10.0, langversion, nullability, package metadata
├── src/
│   ├── Directory.Build.props      Overrides to net48;net10.0, pulls in the polyfills
│   ├── Dicom.Core/                Tags, VRs, values, lengths, dictionary contract
│   ├── Dicom.Dictionary.Std/      Part 6 attribute dictionary (embedded TSV)
│   ├── Dicom.Encoding/            Transfer syntaxes, character sets, value bytes
│   ├── Dicom.Parser/              Byte and token level reading and writing
│   ├── Dicom.Object/              In-memory data sets and Part 10 files
│   ├── Dicom.PixelData/           Image pixel attributes, frame access, transcoding
│   ├── Dicom.Codecs/              JPEG, JPEG-LS, JPEG 2000 and RLE codecs
│   ├── DicomObjects.Compat/       Drop-in facade over the above
│   ├── Polyfills/                 Types .NET Framework lacks
│   └── Shared/                    MathEx, compiled into several libraries
└── tests/
    └── Dicom.Tests/               xUnit, run against both targets
```

### Dependency graph

```text
Dicom.Core
  └─ Dicom.Dictionary.Std
       └─ Dicom.Encoding
            └─ Dicom.Parser
                 └─ Dicom.Object
                      ├─ Dicom.PixelData ── Dicom.Codecs
                      └─ DicomObjects.Compat (references all of the above)
```

`Dicom.Core` has no project or package references at all.

## Getting started

```bash
dotnet build Dicom.slnx
dotnet test tests/Dicom.Tests/Dicom.Tests.csproj
```

The test project multi-targets `net48;net10.0` deliberately, so `dotnet test` exercises both the
polyfilled .NET Framework build and the modern one. There are 58 `[Fact]`/`[Theory]` methods,
which expand to a larger number of test cases through `[Theory]` data.

### Reading a file

```csharp
using Dicom.Core;
using Dicom.Object;
using Dicom.PixelData;

FileDicomObject file = DicomFile.Open(@"C:\studies\lumbar.dcm");

string? patient = file.GetString(new Tag(0x0010, 0x0010));
int? rows = file.GetInt32(Tags.Rows);

NativePixelData pixels = NativePixelData.From(file);
int[] samples = pixels.GetFrameSamples(0);
byte[] display = pixels.RenderGrayscale8(0);
```

### Writing a file

```csharp
file.SetTransferSyntax(TransferSyntaxRegistry.ExplicitVrLittleEndian);
file.Save(@"C:\studies\lumbar-uncompressed.dcm");
```

### Through the compatibility facade

```csharp
using DicomObjects;

DicomImage image = new(File.ReadAllBytes(@"C:\studies\lumbar.dcm"));
Array values = image.RawPixelValuesByFrame(1);

image.Write(out byte[] jpeg, UIDs.TransferSyntaxes.JPEGBaseline, quality: 100);
```

## Architecture

The libraries are layered, each one knowing only about the layer beneath it.

| Layer | Project | Works in terms of |
| --- | --- | --- |
| Model | `Dicom.Core` | `Tag`, `Vr`, `Length`, `PrimitiveValue`, `DicomValue`, `DataElement` |
| Catalogue | `Dicom.Dictionary.Std` | `DictionaryEntry` per attribute, keyed by tag and keyword |
| Encoding | `Dicom.Encoding` | `TransferSyntax`, text encodings, value bytes |
| Bytes and tokens | `Dicom.Parser` | `DataElementHeader`, `DataSetToken` |
| Objects | `Dicom.Object` | `InMemDicomObject`, `FileDicomObject`, `FileMetaTable` |
| Images | `Dicom.PixelData` | `PixelDataDescription`, frames, samples |
| Compression | `Dicom.Codecs` | `FrameFormat`, `EncodedFrame`, `DecodedFrame` |
| Facade | `DicomObjects.Compat` | `DicomDataSet`, `DicomImage`, `DicomAttribute` |

### The read path

1. `DicomFile.Open(path)` opens the stream.
2. `FileMetaTable.Read` consumes the 128 byte preamble, checks for `DICM`, then reads group
   0002 with a `DataSetReader` fixed to Explicit VR Little Endian.
3. `FileMetaTable.RequireTransferSyntax()` resolves the transfer syntax UID through
   `TransferSyntaxRegistry`, and `DicomStreamReader.SwitchTo` applies it to the rest of the
   stream. A deflated syntax wraps the stream in a `DeflateStream`.
4. `DataSetReader.Read()` yields a lazy stream of `DataSetToken` values. `DicomStreamReader`
   does the byte work: tag, VR (explicit) or dictionary lookup (implicit), and length.
5. `ValueDecoder.Decode` turns the value bytes into a `PrimitiveValue`, swapping byte order for
   big endian syntaxes and applying the text encoding in force.
6. `DataSetBuilder.BuildInto` consumes the tokens and assembles an `InMemDicomObject`, using a
   stack of frames to nest sequence items and collect pixel data fragments.

### The write path

`FileDicomObject.Save` writes the preamble and `DICM`, then the file meta group with its group
length recalculated from a buffered copy, then hands the data set to a `DataSetWriter` in the
transfer syntax the meta table states. `DataSetWriter` streams elements in ascending tag order.
Sequences are buffered in memory so they can be written with a defined length; encapsulated
pixel data is always written with an undefined length, an offset table item, one item per
fragment, and a sequence delimitation item.

### How values are represented

`PrimitiveValue` keeps the value in whichever representation it was decoded as — text, a list of
strings, tags, or an array of numbers of a given width — and `PrimitiveValueKind` names which.
The conversion methods translate between representations on demand, so a DS attribute decoded as
strings can still be read as doubles. `DicomValue` has three shapes: `PrimitiveDicomValue`,
`SequenceDicomValue` (nested data sets), and `PixelSequenceDicomValue` (an offset table and the
compressed fragments). This mirrors how `dicom-rs` models values.

### Character sets

`DataSetReader` starts in ISO-8859-1 and switches encoding whenever it decodes Specific
Character Set (0008,0005), resolving the declared name through `DicomCharacterSet.Resolve` to a
.NET code page. The new encoding applies to the character set aware VRs — LO, LT, PN, SH, ST,
UC and UT — for the remainder of the data set. Multi-valued declarations take the first named
set; code extension escape sequences are not interpreted.

### Sequences and encapsulated pixel data

A sequence is read as `SequenceStart`, then `ItemStart`/nested tokens/`ItemEnd` per item, then
`SequenceEnd`, whether the lengths are defined or undefined. An element with an undefined length
and VR UN is treated as a sequence too, because some writers emit sequences that way.

Pixel Data (7FE0,0010) with an undefined length becomes a `PixelSequenceStart` followed by one
`ItemValue` per item. The first item is the basic offset table, read as little endian 32-bit
offsets regardless of the data set's byte order; the rest are fragments.

## Pixel data and codecs

`PixelDataDescription.From` reads the image pixel attributes — rows, columns, samples per pixel,
frames, bits allocated and stored, high bit, pixel representation, planar configuration,
photometric interpretation, rescale and window — and derives `BytesPerSample`, `SamplesPerFrame`
and `FrameLength`. `NativePixelData.From` then gives access to the samples:

- `GetFrameBytes(frame)` — the raw bytes of one frame, decompressing on first use and caching
  the result per frame.
- `GetFrameSamples(frame)` — stored values, shifted by the high bit, masked to the stored bits,
  and sign extended when the pixel representation says so.
- `GetFrameValues(frame)` — those values after rescale slope and intercept.
- `RenderGrayscale8(frame, windowCenter, windowWidth)` — an 8-bit rendering, using the data
  set's window or, when none is stated, the range of values present, inverted for MONOCHROME1.

Fragments are assigned to frames by `NativePixelData.Split`: one fragment per frame is the usual
case, and when there are more the basic offset table says where each frame starts.

`PixelDataTranscoder.ToTransferSyntax` moves pixel data between syntaxes. Going to a native
syntax it decodes every frame and stores them back to back as OW or OB; going to an encapsulated
syntax it encodes one fragment per frame, builds the offset table, and updates Photometric
Interpretation, Planar Configuration and, for lossy codecs, Lossy Image Compression (0028,2110),
its ratio (0028,2112) and method (0028,2114).

### Codec registry

Codecs implement `IPixelCodec` and are held in `PixelCodecRegistry`, a `ConcurrentDictionary`
keyed by transfer syntax UID. `Lookup`, `RequireDecoder` and `RequireEncoder` resolve a codec;
`Register` adds or replaces one, which callers may do while other threads are reading. The
codecs hold no state between calls.

| Transfer syntax | UID | Decode | Encode | Implementation |
| --- | --- | --- | --- | --- |
| JPEG Lossless, Non-Hierarchical (Process 14) | `…1.2.4.57` | yes | yes | in repo |
| JPEG Lossless, Selection Value 1 | `…1.2.4.70` | yes | yes | in repo |
| JPEG Baseline (Process 1) | `…1.2.4.50` | yes | yes | in repo |
| JPEG Extended (Process 2 and 4) | `…1.2.4.51` | yes | no | in repo (baseline decoder) |
| JPEG-LS Lossless | `…1.2.4.80` | yes | no | CharLS.Managed |
| JPEG-LS Lossy (Near-Lossless) | `…1.2.4.81` | yes | no | CharLS.Managed |
| JPEG 2000 (Lossless Only) | `…1.2.4.90` | yes | no | CoreJ2K |
| JPEG 2000 | `…1.2.4.91` | yes | no | CoreJ2K |
| RLE Lossless | `…1.2.5` | yes | yes | in repo |

UIDs are abbreviated; each is prefixed `1.2.840.10008`. Encoding is offered for lossless JPEG,
baseline JPEG and RLE only. Extended sequential JPEG raises a `CodecException` directing callers
to baseline instead.

The hand-written JPEG work lives in `src/Dicom.Codecs/Jpeg/`: `JpegBitReader` and
`JpegBitWriter` for bit-level I/O, `JpegHuffmanTable` and `JpegTables` for the entropy tables,
`JpegTransform` for the DCT, `JpegBaselineScan`, `JpegPlane` and `JpegFrameSamples` for the
frame model, and the four encoder and decoder entry points.

`TransferSyntaxRegistry` knows about more syntaxes than there are codecs for. MPEG-2, H.264,
HEVC and the JPEG 2000 Part 2 multi-component syntaxes are recognised so that such files can be
read and rewritten as fragments, but their pixel data cannot be decoded.

## The compatibility facade

`DicomObjects.Compat` compiles into the `DicomObjects` namespace on purpose, so a consuming
project changes a reference rather than its source.

| Facade type | Wraps |
| --- | --- |
| `DicomDataSet` | `InMemDicomObject` |
| `DicomImage` | a `DicomDataSet`, with pixels through `NativePixelData` |
| `DicomAttribute` | a view onto one `DataElement` of a data set |
| `DicomDataSetCollection` | the items of a sequence attribute |
| `DicomGlobal.Dictionary` | `ExtensibleDataDictionary` over `StandardDataDictionary.Instance` |

Loosely typed caller values are converted by the internal `ValueInterop`, which maps strings,
numbers and `DateTime` onto the typed `DicomValue` representations and back, including returning
`DateTime` for DA, TM and DT attributes as DicomObjects callers expect.

`Read` detects the `DICM` magic at offset 128 to tell a Part 10 file from a bare data set, and
falls back to `OverrideTS` or the default syntax for the latter. `Write` resolves the syntax from
its argument, then `OverrideTS`, then `OriginalTS`, then Explicit VR Little Endian, transcodes
the pixel data if the target needs it, and passes `quality` through to the encoder.

Some DicomObjects members exist only so that calling code compiles and behaves sensibly:
`ValidationOptions` and `ValidateAll()` are recorded but no validation is performed, and
`CacheTypes` and `CloseFilesAfterReading` are recorded but everything is held in memory.

## Multi-targeting and polyfills

The root `Directory.Build.props` sets `net10.0`; `src/Directory.Build.props` replaces that with
`net48;net10.0` for the libraries only, leaving the solution-level properties intact. For the
`net48` build it adds `System.Memory` and `Microsoft.Bcl.HashCode`, and compiles
`src/Polyfills/*.cs` into every library: `IndexAndRange.cs`, `NullableAttributes.cs` and
`StreamExtensions.cs`. `src/Polyfills/Core/` — `DateOnly.cs` and `TimeOnly.cs` — is compiled into
`Dicom.Core` alone, because those types appear in its public value API and must be declared once.

`src/Shared/MathEx.cs` is linked into `Dicom.Codecs` and `Dicom.PixelData` to supply `Clamp`,
which .NET Framework does not have, so both builds read the same.

`Dicom.Codecs` drops `JpegLsCodec.cs` on `net48` and omits the `CharLS.Managed` package, which
ships a .NET 8 assembly only. On that target the registry returns no codec for JPEG-LS.

## Tests

| File | Covers |
| --- | --- |
| `tests/Dicom.Tests/CoreValueTests.cs` | Tag parsing and formatting, primitive value conversions, date and time parsing, dictionary lookup |
| `tests/Dicom.Tests/RoundTripTests.cs` | File round trips across transfer syntaxes, nested sequences, encapsulated pixel data, character sets |
| `tests/Dicom.Tests/CodecTests.cs` | Codec round trips and sample-exact reproduction for the supported syntaxes |
| `tests/Dicom.Tests/CompatFacadeTests.cs` | The facade API end to end, including eight-thread concurrency |

## File reference

### Dicom.Core (namespace `Dicom.Core`)

| File | Responsibility |
| --- | --- |
| `Tag.cs` | The `Tag` readonly struct: group and element numbers, private and delimiter tests, repeating-group canonicalisation, parsing and formatting. |
| `Vr.cs` | The `Vr` enum, each member encoding its two ASCII letters in a `ushort`. |
| `VrExtensions.cs` | Per-VR metadata: two-letter code, long explicit header form, text and character-set awareness, multi-valuedness, padding byte, item size, parsing. |
| `Length.cs` | The `Length` readonly struct, including the undefined-length sentinel and even-length rounding. |
| `PrimitiveValue.cs` | The non-sequence value, its factories and its typed accessors. |
| `PrimitiveValue.Conversions.cs` | On-demand conversion between representations, and short-string formatting. |
| `PrimitiveValueKind.cs` | Which representation a `PrimitiveValue` holds. |
| `DicomValue.cs` | The three value shapes: primitive, sequence, pixel sequence. |
| `DataElement.cs` | One attribute (tag, VR, value), and the `IDicomDataSet` read contract. |
| `DataElementHeader.cs` | The header preceding a value on the wire, plus the `Tags` constants the encoding layer needs by name. |
| `DicomDateTime.cs` | Parsing and formatting of DA, TM and DT values, accepting partial precision. |
| `IDataDictionary.cs` | The dictionary contract, `DictionaryEntry`, and the VR-for-tag fallback rules. |
| `DicomValueException.cs` | `DicomValueException` and `DicomParseException`. |

### Dicom.Dictionary.Std (namespace `Dicom.Dictionary.Std`)

| File | Responsibility |
| --- | --- |
| `StandardDataDictionary.cs` | The Part 6 attribute registry, loaded lazily and thread-safely from the embedded table and indexed by tag and keyword. |
| `Resources/attributes.tsv` | 5,119 tab-separated rows of tag, keyword, VR, VM and name. |

### Dicom.Encoding (namespace `Dicom.Encoding`)

| File | Responsibility |
| --- | --- |
| `TransferSyntax.cs` | The immutable description of one transfer syntax. |
| `TransferSyntaxRegistry.cs` | The concurrent registry of the eighteen recognised syntaxes, with lookup and runtime registration. |
| `DicomCharacterSet.cs` | Mapping of Specific Character Set names to .NET encodings. |
| `ValueDecoder.cs` | Raw value bytes to `PrimitiveValue`, with byte swapping and text splitting. |
| `ValueEncoder.cs` | `PrimitiveValue` back to padded value bytes. |

### Dicom.Parser (namespace `Dicom.Parser`)

| File | Responsibility |
| --- | --- |
| `DicomStreamReader.cs` | Byte-level reading of tags, element headers and item headers, honouring the syntax in force and able to switch syntax mid-stream. |
| `DataSetToken.cs` | One parse step, as a readonly struct discriminated union. |
| `DataSetReader.cs` | The pull parser yielding tokens, tracking position and the active character set. |
| `DataSetWriter.cs` | The streaming writer for elements, sequences and pixel sequences. |

### Dicom.Object (namespace `Dicom.Object`)

| File | Responsibility |
| --- | --- |
| `InMemDicomObject.cs` | A data set as a `SortedDictionary<Tag, DataElement>`, with typed accessors and deep cloning. |
| `DataSetBuilder.cs` | Assembly of a token stream into a nested in-memory data set. |
| `FileMetaTable.cs` | The file meta group: preamble, `DICM` magic, group length recalculation, transfer syntax resolution. |
| `FileDicomObject.cs` | A data set plus its meta table, with the `DicomFile` entry points for Part 10 reading and writing. |

### Dicom.PixelData (namespace `Dicom.PixelData`)

| File | Responsibility |
| --- | --- |
| `PixelDataDescription.cs` | The image pixel attributes and the sizes derived from them. |
| `NativePixelData.cs` | Frame bytes, stored samples, rescaled values and 8-bit rendering, decompressing and caching per frame. |
| `PixelDataTranscoder.cs` | Moving pixel data between native and encapsulated syntaxes and keeping the describing attributes in line. |

### Dicom.Codecs (namespace `Dicom.Codecs`)

| File | Responsibility |
| --- | --- |
| `PixelCodec.cs` | The `IPixelCodec` contract and the `PixelCodecRegistry`. |
| `FrameFormat.cs` | The uncompressed frame layout, `EncodedFrame`, `DecodedFrame` and `CodecException`. |
| `RleCodec.cs` | RLE Lossless, segment per sample byte, PackBits encoded. |
| `JpegLosslessCodec.cs` | Lossless JPEG for `.57` and `.70`, plus the shared `Codec.Concatenate` helper. |
| `JpegBaselineCodec.cs` | Baseline JPEG, with YBR_FULL_422 on encode and RGB on decode for colour frames. |
| `JpegExtendedCodec.cs` | Extended sequential JPEG, decode only. |
| `JpegLsCodec.cs` | JPEG-LS decode through CharLS.Managed, re-laying samples to the stored planar configuration. |
| `Jpeg2000Codec.cs` | JPEG 2000 decode through CoreJ2K, undoing the decoder's level shift for signed data. |
| `Jpeg/JpegBitReader.cs`, `Jpeg/JpegBitWriter.cs` | Bit-level reading and writing of entropy-coded data. |
| `Jpeg/JpegHuffmanTable.cs`, `Jpeg/JpegTables.cs` | Huffman table construction and the standard tables. |
| `Jpeg/JpegTransform.cs` | The forward and inverse discrete cosine transform. |
| `Jpeg/JpegPlane.cs`, `Jpeg/JpegFrameSamples.cs` | The per-component and per-frame sample model, including colour conversion and frame byte output. |
| `Jpeg/JpegBaselineScan.cs` | Scan and component bookkeeping for baseline streams. |
| `Jpeg/JpegBaselineDecoder.cs`, `Jpeg/JpegBaselineEncoder.cs` | Baseline and extended sequential decoding, and baseline encoding. |
| `Jpeg/JpegLosslessDecoder.cs`, `Jpeg/JpegLosslessEncoder.cs` | Lossless decoding with whichever predictor the stream states, and encoding with the first-order predictor. |

### DicomObjects.Compat (namespace `DicomObjects`)

| File | Responsibility |
| --- | --- |
| `DicomDataSet.cs` | The data set facade: attribute access by group and element, add, remove, copy, enumerate. |
| `DicomDataSet.Io.cs` | Reading from files, byte arrays and streams, and writing in a chosen transfer syntax at a chosen quality. |
| `DicomImage.cs` | The image facade: study, series and instance UIDs, frame count, size. |
| `DicomImage.Pixels.cs` | Pixel access and assignment, including `RawPixelValuesByFrame` and the `SetPixelData` overloads. |
| `DicomAttribute.cs` | One attribute as DicomObjects presents it: VR, VM, description, value. |
| `DicomDataSetCollection.cs` | The value of a sequence attribute. |
| `DicomGlobal.cs` | Process-wide settings, the extensible private-attribute dictionary, and the `IsDICOM` helpers. |
| `UIDs.cs` | The `UID` type and the commonly named SOP class and transfer syntax constants. |
| `Validation.cs` | The `ValidationOptions` flags, recorded but not enforced. |
| `Enums.cs` | `CacheTypes` and the `Keyword` placeholder. |
| `ValueInterop.cs` | Conversion between loosely typed caller values and typed `DicomValue` content. |

### Shared and polyfills

| File | Responsibility |
| --- | --- |
| `src/Shared/MathEx.cs` | `Clamp` for both targets, linked into the libraries that need it. |
| `src/Polyfills/IndexAndRange.cs` | `Index` and `Range` for `net48`. |
| `src/Polyfills/NullableAttributes.cs` | The nullability annotation attributes for `net48`. |
| `src/Polyfills/StreamExtensions.cs` | The span-based `Stream` read and write overloads for `net48`. |
| `src/Polyfills/Core/DateOnly.cs`, `Core/TimeOnly.cs` | `DateOnly` and `TimeOnly` for `net48`, declared in `Dicom.Core` only. |

## Known limitations

- **JPEG Baseline and JPEG Extended decoding.** `AUGGIE_FINDINGS.md` records that single
  component scans with sampling factors above one misalign the MCU blocks, so decoded samples
  differ from DicomObjects v8 by far more than DCT rounding explains. This is the one open
  correctness issue; the other syntaxes verified sample-identical.
- **Encode support is deliberately narrow.** JPEG-LS, JPEG 2000 and JPEG Extended decode only.
- **No DICOM networking.** There is no equivalent of `DicomServer` or `DicomAssociation`.
- **No conformance validation.** IODs and modules are not checked.
- **JPEG-LS is absent from the `net48` build**, as described above.
- **CI is not wired up for .NET.** `.github/workflows/rust.yml` is inherited from `dicom-rs` and
  runs `cargo` commands; it needs replacing with `dotnet build` and `dotnet test`.

## Licensing and third-party code

This library is dual-licensed Apache-2.0 or MIT, following `dicom-rs`, whose design and naming
the six core modules are ported from. See `LICENSE`, `LICENSE-APACHE`, `LICENSE-MIT` and
`THIRD-PARTY-NOTICES.md`.

| Component | Licence |
| --- | --- |
| CoreJ2K 2.4.1.108 (JPEG 2000) | BSD-3-Clause |
| CharLS.Managed 0.8.0 (JPEG-LS) | BSD-3-Clause |
| Attribute dictionary (5,119 tags), adapted from DCMTK | OFFIS e.V. copyright |

The dictionary excludes command group 0000, file meta group 0002, directory group 0004, and the
repeating range tags, which are handled in code instead.
