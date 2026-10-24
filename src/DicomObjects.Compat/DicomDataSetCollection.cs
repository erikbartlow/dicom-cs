using System.Collections;
using System.Collections.ObjectModel;
using Dicom.Core;
using Dicom.Object;

namespace DicomObjects;

/// <summary>
/// The items of a sequence attribute.
/// </summary>
public sealed class DicomDataSetCollection : Collection<DicomDataSet>
{
    public DicomDataSetCollection()
    {
    }

    public DicomDataSetCollection(IEnumerable<DicomDataSet> items)
    {
        foreach (DicomDataSet item in items)
            Add(item);
    }

    internal static DicomDataSetCollection Wrap(IEnumerable<IDicomDataSet> items)
    {
        DicomDataSetCollection collection = [];

        foreach (IDicomDataSet item in items)
        {
            collection.Add(item is InMemDicomObject inMemory
                ? new DicomDataSet(inMemory)
                : new DicomDataSet(Copy(item)));
        }

        return collection;
    }

    private static InMemDicomObject Copy(IDicomDataSet source)
    {
        InMemDicomObject copy = new();

        foreach (DataElement element in source)
            copy.Put(element);

        return copy;
    }
}
