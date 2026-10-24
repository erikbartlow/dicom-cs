namespace Dicom.Core;

/// <summary>
/// The length in bytes of a data element's value, which may be undefined for sequences
/// and encapsulated pixel data that are terminated by a delimiter instead.
/// </summary>
public readonly struct Length : IEquatable<Length>
{
    /// <summary>The sentinel value marking a length as undefined.</summary>
    public const uint UndefinedValue = 0xFFFF_FFFFu;

    /// <summary>A length that is not stated, so the value is read until its delimiter.</summary>
    public static readonly Length Undefined = new(UndefinedValue);

    /// <summary>A length of zero bytes.</summary>
    public static readonly Length Zero = new(0);

    public Length(uint value) => Value = value;

    /// <summary>The raw length value as encoded.</summary>
    public uint Value { get; }

    /// <summary>Whether the length is undefined.</summary>
    public bool IsUndefined => Value == UndefinedValue;

    /// <summary>Whether the length is stated.</summary>
    public bool IsDefined => Value != UndefinedValue;

    /// <summary>Whether the length is stated and odd, which is invalid in a conforming data set.</summary>
    public bool IsOdd => IsDefined && (Value & 1) == 1;

    /// <summary>The length as a count of bytes, or null when undefined.</summary>
    public int? AsInt32 => IsUndefined ? null : checked((int)Value);

    /// <summary>The length as a count of bytes, throwing when undefined.</summary>
    public int Require() => AsInt32 ?? throw new InvalidOperationException("The data element length is undefined.");

    /// <summary>The length rounded up to the next even number of bytes.</summary>
    public Length Even() => IsOdd ? new Length(Value + 1) : this;

    public bool Equals(Length other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is Length other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => IsUndefined ? "undefined" : Value.ToString();

    public static implicit operator Length(uint value) => new(value);

    public static bool operator ==(Length left, Length right) => left.Equals(right);

    public static bool operator !=(Length left, Length right) => !left.Equals(right);
}
