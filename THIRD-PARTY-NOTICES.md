# Third-party notices

This library includes, or derives from, the work listed below. The notices here are
in addition to the licence of the library itself, which is in `LICENSE`.

## DICOM-rs

- Project: DICOM-rs, <https://github.com/Enet4/dicom-rs>
- Copyright (c) 2016 Eduardo Pinho and the DICOM-rs contributors
- Licence: dual licensed, Apache License 2.0 or MIT, at your option

These projects are ports of the DICOM-rs crates of the same name and derive from
them in design, naming and behaviour:

| Project | DICOM-rs crate |
|---|---|
| `src/Dicom.Core` | `dicom-core` |
| `src/Dicom.Dictionary.Std` | `dicom-dictionary-std` |
| `src/Dicom.Encoding` | `dicom-encoding` |
| `src/Dicom.Parser` | `dicom-parser` |
| `src/Dicom.Object` | `dicom-object` |
| `src/Dicom.PixelData` | `dicom-pixeldata` |

`src/Dicom.Codecs` and `src/DicomObjects.Compat` do not derive from DICOM-rs.
`Dicom.Codecs` holds in-house JPEG, lossless JPEG and RLE implementations, together
with the adapters for the two decoder packages listed further down.
`DicomObjects.Compat` is an in-house facade over the projects above.

The full text of both DICOM-rs licences is kept in this repository, as
`LICENSE-APACHE` and `LICENSE-MIT`. The MIT text reads:

```text
Copyright (c) 2016 Eduardo Pinho

Permission is hereby granted, free of charge, to any
person obtaining a copy of this software and associated
documentation files (the "Software"), to deal in the
Software without restriction, including without
limitation the rights to use, copy, modify, merge,
publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software
is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice
shall be included in all copies or substantial portions
of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF
ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED
TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT
SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR
IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.
```

## The bundled attribute dictionary

`src/Dicom.Dictionary.Std/Resources/attributes.tsv` holds 5,119 attributes, one per
line, as tag, keyword, value representation, value multiplicity and name.

It was generated from the DICOM-rs tag table
(`dictionary-std/src/tags.rs`, commit `5ba49ae2` of this repository's history),
which that project generates with its `dictionary-builder` tool and documents as:

> Adapted from the DCMTK project.
> URL: <https://raw.githubusercontent.com/DCMTK/dcmtk/master/dcmdata/data/dicom.dic>
> License: <https://github.com/DCMTK/dcmtk/blob/master/COPYRIGHT>
> Copyright (C) 1994-2026, OFFIS e.V.

The command group (0000), file meta (0002) and directory record (0004) groups are
not in the file, nor are the repeating group ranges such as the overlay groups.

## CoreJ2K

Used by `src/Dicom.Codecs/Jpeg2000Codec.cs` to decode JPEG 2000.

- Package: `CoreJ2K` 2.4.1.108, <https://github.com/cinderblocks/CoreJ2K>
- Copyright (c) 1999-2000 JJ2000 Partners; (c) 2007-2012 Jason S. Clary;
  (c) 2013-2018 Anders Gustafsson, Cureos AB; (c) 2007-2016 CSJ2K contributors;
  (c) 2024-2026 Sjofn LLC
- Licence: BSD-3-Clause, with the JJ2000 copyright notice the package carries as
  `COPYRIGHT-JJ2000-5.1`

The package is referenced from NuGet and is not redistributed in source form here.

## CharLS.Managed

Used by `src/Dicom.Codecs/JpegLsCodec.cs` to decode JPEG-LS.

- Package: `CharLS.Managed` 0.8.0, <https://github.com/team-charls/charls-dotnet>
- Copyright 2024 Team CharLS
- Licence: BSD-3-Clause

The package is referenced from NuGet and is not redistributed in source form here.
