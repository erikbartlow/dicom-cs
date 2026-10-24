using Dicom.Core;
using Dicom.Encoding;
using Dicom.PixelData;

namespace DicomObjects;

/// <summary>
/// Pixel data access for an image. Pixel data stored in fragments is decompressed by the
/// codec for the transfer syntax it was read in.
/// </summary>
public partial class DicomImage
{
    /// <summary>
    /// The pixel data of the image. Setting it accepts a byte or unsigned short array and
    /// chooses the representation to match.
    /// </summary>
    public Array? Pixels
    {
        get => RawPixels;
        set
        {
            switch (value)
            {
                case null:
                    DataSet.Remove(0x7FE0, 0x0010);
                    return;

                case byte[] bytes:
                    DataSet.Data.Put(new DataElement(Tags.PixelData, Vr.OB, PrimitiveValue.FromBytes(bytes)));
                    return;

                case ushort[] words:
                    DataSet.Data.Put(new DataElement(Tags.PixelData, Vr.OW, PrimitiveValue.FromUInt16(words)));
                    return;

                case short[] words:
                    DataSet.Data.Put(new DataElement(Tags.PixelData, Vr.OW, PrimitiveValue.FromInt16(words)));
                    return;

                default:
                    throw new NotSupportedException(
                        $"Pixel data of type {value.GetType().Name} cannot be stored.");
            }
        }
    }

    /// <summary>
    /// The stored values of one frame, numbered from one as the standard does. The array
    /// is of bytes, signed shorts or unsigned shorts to match how the samples are stored.
    /// </summary>
    public Array RawPixelValuesByFrame(int frame)
    {
        NativePixelData pixels = NativePixelData.From(Data, OverrideTS ?? OriginalTS);
        PixelDataDescription description = pixels.Description;
        int[] samples = pixels.GetFrameSamples(frame - 1);

        if (description.BytesPerSample == 1)
        {
            byte[] bytes = new byte[samples.Length];

            for (int i = 0; i < samples.Length; i++)
                bytes[i] = (byte)samples[i];

            return bytes;
        }

        if (description.IsSigned)
        {
            short[] signed = new short[samples.Length];

            for (int i = 0; i < samples.Length; i++)
                signed[i] = (short)samples[i];

            return signed;
        }

        ushort[] unsigned = new ushort[samples.Length];

        for (int i = 0; i < samples.Length; i++)
            unsigned[i] = (ushort)samples[i];

        return unsigned;
    }

    /// <summary>
    /// Replaces the pixel data with the contents of a file, optionally only part of it.
    /// A length of zero means the rest of the file.
    /// </summary>
    public void SetPixelFile(string fileName, string transferSyntax, long offset, long length)
    {
        using FileStream stream = File.OpenRead(fileName);
        stream.Position = offset;

        long count = length > 0 ? length : stream.Length - offset;
        byte[] bytes = new byte[count];
        stream.ReadExactly(bytes);

        SetPixelData(transferSyntax, bytes);
    }

    /// <summary>
    /// Replaces the pixel data with the bytes given, recording the transfer syntax they are
    /// encoded in. Bytes for a compressed syntax are carried as a single fragment per frame.
    /// </summary>
    public void SetPixelData(string transferSyntax, byte[] bytes)
    {
        TransferSyntax syntax = TransferSyntaxRegistry.Require(transferSyntax);

        if (!syntax.Encapsulated)
        {
            DataSet.Data.Put(new DataElement(Tags.PixelData, Vr.OW, PrimitiveValue.FromBytes(bytes)));
            DataSet.OverrideTS = syntax.Uid;
            return;
        }

        DataSet.Data.Put(new DataElement(Tags.PixelData, Vr.OB, DicomValue.OfFragments([bytes])));
        DataSet.OverrideTS = syntax.Uid;
    }

    /// <summary>
    /// Replaces the pixel data with one fragment per frame, obtained from the callback.
    /// </summary>
    public void SetPixelData(string transferSyntax, Func<int, byte[]> fragmentForFrame)
    {
        TransferSyntax syntax = TransferSyntaxRegistry.Require(transferSyntax);
        List<byte[]> fragments = [];

        for (int frame = 0; frame < FrameCount; frame++)
            fragments.Add(fragmentForFrame(frame));

        if (!syntax.Encapsulated)
        {
            DataSet.Data.Put(new DataElement(
                Tags.PixelData,
                Vr.OW,
                PrimitiveValue.FromBytes([.. fragments.SelectMany(static f => f)])));
        }
        else
        {
            DataSet.Data.Put(new DataElement(Tags.PixelData, Vr.OB, DicomValue.OfFragments(fragments)));
        }

        DataSet.OverrideTS = syntax.Uid;
    }
}
