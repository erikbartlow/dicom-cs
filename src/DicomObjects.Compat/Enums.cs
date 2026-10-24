namespace DicomObjects.Enums;

/// <summary>
/// How a data set holds on to the data it has read.
/// </summary>
public enum CacheTypes
{
    /// <summary>Nothing is cached; values are kept in memory only while in use.</summary>
    None = 0,

    /// <summary>Values stay in memory for the lifetime of the data set.</summary>
    Memory = 1,
}

/// <summary>
/// An attribute identified by its tag, with the group in the high half of the value.
/// The catalogue of names is open ended, so any tag may be cast to this type.
/// </summary>
public enum Keyword : uint
{
    /// <summary>No attribute.</summary>
    Undefined = 0,
}
