using System.Buffers.Binary;
using Dicom.Core;

namespace Dicom.Encoding;

/// <summary>
/// Turns a <see cref="PrimitiveValue"/> back into the bytes of a data element's value,
/// padded to an even length as the standard requires.
/// </summary>
public static class ValueEncoder
{
    /// <summary>
    /// Encodes a value for the given representation. The result always has an even length.
    /// </summary>
    public static byte[] Encode(
        Vr vr,
        PrimitiveValue value,
        ByteOrder byteOrder = ByteOrder.LittleEndian,
        System.Text.Encoding? textEncoding = null)
    {
        if (value.IsEmpty)
            return [];

        byte[] bytes = EncodeCore(vr, value, byteOrder, textEncoding);

        return Pad(bytes, vr);
    }

    private static byte[] EncodeCore(
        Vr vr,
        PrimitiveValue value,
        ByteOrder byteOrder,
        System.Text.Encoding? textEncoding)
    {
        bool swap = byteOrder == ByteOrder.BigEndian;

        // Bulk data handed over as raw bytes is written through unchanged, whatever its representation.
        if (value.Kind == PrimitiveValueKind.U8 && !vr.IsText())
            return value.AsBytes();

        switch (vr)
        {
            case Vr.AT:
                return EncodeTags(value, swap);

            case Vr.US:
            case Vr.OW:
                return Numbers(value.ToUInt16Array(), 2, swap);

            case Vr.SS:
                return Numbers(value.ToInt16Array(), 2, swap);

            case Vr.UL:
            case Vr.OL:
                return Numbers(value.ToUInt32Array(), 4, swap);

            case Vr.SL:
                return Numbers(value.ToInt32Array(), 4, swap);

            case Vr.UV:
            case Vr.OV:
                return Numbers(value.ToUInt64Array(), 8, swap);

            case Vr.SV:
                return Numbers(value.ToInt64Array(), 8, swap);

            case Vr.FL:
            case Vr.OF:
                return Numbers(value.ToSingleArray(), 4, swap);

            case Vr.FD:
            case Vr.OD:
                return Numbers(value.ToDoubleArray(), 8, swap);

            case Vr.OB:
            case Vr.UN:
                return value.ToBytes();

            default:
                return EncodeText(vr, value, textEncoding);
        }
    }

    private static byte[] EncodeText(Vr vr, PrimitiveValue value, System.Text.Encoding? textEncoding)
    {
        System.Text.Encoding encoding = vr.IsCharacterSetAware()
            ? textEncoding ?? DicomCharacterSet.Default
            : DicomCharacterSet.Default;

        return encoding.GetBytes(value.ToMultiStringValue());
    }

    private static byte[] EncodeTags(PrimitiveValue value, bool swap)
    {
        Tag[] tags = value.Kind == PrimitiveValueKind.Tags
            ? value.AsTags()
            : [.. value.ToStringArray().Select(Tag.Parse)];

        byte[] buffer = new byte[tags.Length * 4];

        for (int i = 0; i < tags.Length; i++)
        {
            Span<byte> slot = buffer.AsSpan(i * 4, 4);

            if (swap)
            {
                BinaryPrimitives.WriteUInt16BigEndian(slot, tags[i].Group);
                BinaryPrimitives.WriteUInt16BigEndian(slot[2..], tags[i].Element);
            }
            else
            {
                BinaryPrimitives.WriteUInt16LittleEndian(slot, tags[i].Group);
                BinaryPrimitives.WriteUInt16LittleEndian(slot[2..], tags[i].Element);
            }
        }

        return buffer;
    }

    private static byte[] Numbers<T>(T[] values, int itemSize, bool swap)
        where T : unmanaged
    {
        byte[] buffer = new byte[values.Length * itemSize];
        Buffer.BlockCopy(values, 0, buffer, 0, buffer.Length);

        if (swap == BitConverter.IsLittleEndian && itemSize > 1)
        {
            for (int offset = 0; offset < buffer.Length; offset += itemSize)
                buffer.AsSpan(offset, itemSize).Reverse();
        }

        return buffer;
    }

    private static byte[] Pad(byte[] bytes, Vr vr)
    {
        if ((bytes.Length & 1) == 0)
            return bytes;

        byte[] padded = new byte[bytes.Length + 1];
        bytes.CopyTo(padded, 0);
        padded[^1] = vr.PaddingByte();

        return padded;
    }
}
