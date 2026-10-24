using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Dicom.Core;

namespace Dicom.Encoding;

/// <summary>
/// Turns the raw bytes of a data element's value into a <see cref="PrimitiveValue"/>,
/// honouring the byte order of the transfer syntax and the data set's character set.
/// </summary>
public static class ValueDecoder
{
    /// <summary>
    /// Decodes a value. Text is split on backslashes for the representations that permit
    /// multiple values, and binary values are byte swapped when the stream order differs
    /// from this machine's.
    /// </summary>
    public static PrimitiveValue Decode(
        Vr vr,
        ReadOnlySpan<byte> bytes,
        ByteOrder byteOrder = ByteOrder.LittleEndian,
        System.Text.Encoding? textEncoding = null)
    {
        if (bytes.Length == 0)
            return PrimitiveValue.Empty;

        bool swap = byteOrder == ByteOrder.BigEndian;

        switch (vr)
        {
            case Vr.AT:
                return PrimitiveValue.FromTags(DecodeTags(bytes, swap));

            case Vr.US:
                return PrimitiveValue.FromUInt16(Numbers<ushort>(bytes, swap));

            case Vr.SS:
                return PrimitiveValue.FromInt16(Numbers<short>(bytes, swap));

            case Vr.UL:
                return PrimitiveValue.FromUInt32(Numbers<uint>(bytes, swap));

            case Vr.SL:
                return PrimitiveValue.FromInt32(Numbers<int>(bytes, swap));

            case Vr.UV:
                return PrimitiveValue.FromUInt64(Numbers<ulong>(bytes, swap));

            case Vr.SV:
                return PrimitiveValue.FromInt64(Numbers<long>(bytes, swap));

            case Vr.FL:
            case Vr.OF:
                return PrimitiveValue.FromSingle(Numbers<float>(bytes, swap));

            case Vr.FD:
            case Vr.OD:
                return PrimitiveValue.FromDouble(Numbers<double>(bytes, swap));

            case Vr.OW:
                return PrimitiveValue.FromUInt16(Numbers<ushort>(bytes, swap));

            case Vr.OL:
                return PrimitiveValue.FromUInt32(Numbers<uint>(bytes, swap));

            case Vr.OV:
                return PrimitiveValue.FromUInt64(Numbers<ulong>(bytes, swap));

            case Vr.OB:
            case Vr.UN:
                return PrimitiveValue.FromBytes(bytes.ToArray());

            default:
                return DecodeText(vr, bytes, textEncoding);
        }
    }

    private static PrimitiveValue DecodeText(Vr vr, ReadOnlySpan<byte> bytes, System.Text.Encoding? textEncoding)
    {
        System.Text.Encoding encoding = vr.IsCharacterSetAware()
            ? textEncoding ?? DicomCharacterSet.Default
            : DicomCharacterSet.Default;

        string text = encoding.GetString(bytes).TrimEnd('\0');

        if (!vr.IsMultiValued())
            return PrimitiveValue.FromString(text.TrimEnd(' '));

        if (!text.Contains(PrimitiveValue.ValueSeparator))
            return PrimitiveValue.FromString(text.Trim(' '));

        string[] parts = text.Split(PrimitiveValue.ValueSeparator);

        for (int i = 0; i < parts.Length; i++)
            parts[i] = parts[i].Trim(' ');

        return PrimitiveValue.FromStrings(parts);
    }

    private static Tag[] DecodeTags(ReadOnlySpan<byte> bytes, bool swap)
    {
        Tag[] tags = new Tag[bytes.Length / 4];

        for (int i = 0; i < tags.Length; i++)
        {
            ReadOnlySpan<byte> slot = bytes.Slice(i * 4, 4);

            ushort group = swap
                ? BinaryPrimitives.ReadUInt16BigEndian(slot)
                : BinaryPrimitives.ReadUInt16LittleEndian(slot);

            ushort element = swap
                ? BinaryPrimitives.ReadUInt16BigEndian(slot[2..])
                : BinaryPrimitives.ReadUInt16LittleEndian(slot[2..]);

            tags[i] = new Tag(group, element);
        }

        return tags;
    }

    private static T[] Numbers<T>(ReadOnlySpan<byte> bytes, bool swap)
        where T : unmanaged
    {
        int size = Marshal.SizeOf<T>();
        int count = bytes.Length / size;
        T[] values = new T[count];

        Span<byte> destination = MemoryMarshal.AsBytes(values.AsSpan());
        bytes[..(count * size)].CopyTo(destination);

        if (swap == BitConverter.IsLittleEndian && size > 1)
            SwapBytes(destination, size);

        return values;
    }

    private static void SwapBytes(Span<byte> buffer, int itemSize)
    {
        for (int offset = 0; offset + itemSize <= buffer.Length; offset += itemSize)
            buffer.Slice(offset, itemSize).Reverse();
    }
}
