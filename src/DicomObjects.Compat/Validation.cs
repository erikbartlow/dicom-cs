namespace DicomObjects.Validation;

/// <summary>
/// Which checks are applied to the values of a data set. This library stores what it is given
/// without checking it, so the setting is recorded and otherwise has no effect.
/// </summary>
[Flags]
public enum ValidationOptions
{
    /// <summary>No checking.</summary>
    None = 0,

    /// <summary>Values are checked against the length the representation allows.</summary>
    Length = 1,

    /// <summary>Values are checked against the characters the representation allows.</summary>
    Characters = 2,

    /// <summary>Values are checked against the multiplicity the dictionary states.</summary>
    Multiplicity = 4,

    /// <summary>Every check is applied.</summary>
    All = Length | Characters | Multiplicity,
}
