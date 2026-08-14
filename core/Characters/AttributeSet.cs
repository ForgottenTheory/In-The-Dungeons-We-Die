namespace Dungeons.Characters;

/// <summary>
/// An immutable set of the seven attribute values. Operations return new
/// instances, keeping attribute maths deterministic and side-effect free.
/// </summary>
public readonly struct AttributeSet : IEquatable<AttributeSet>
{
    public int Strength { get; init; }
    public int Dexterity { get; init; }
    public int Intelligence { get; init; }
    public int Constitution { get; init; }
    public int Wisdom { get; init; }
    public int Endurance { get; init; }
    public int Luck { get; init; }

    /// <summary>Creates a set with the same value for every attribute.</summary>
    public static AttributeSet Uniform(int value) => new()
    {
        Strength = value,
        Dexterity = value,
        Intelligence = value,
        Constitution = value,
        Wisdom = value,
        Endurance = value,
        Luck = value,
    };

    public int this[AttributeType attribute] => attribute switch
    {
        AttributeType.Strength => Strength,
        AttributeType.Dexterity => Dexterity,
        AttributeType.Intelligence => Intelligence,
        AttributeType.Constitution => Constitution,
        AttributeType.Wisdom => Wisdom,
        AttributeType.Endurance => Endurance,
        AttributeType.Luck => Luck,
        _ => throw new ArgumentOutOfRangeException(nameof(attribute), attribute, null),
    };

    /// <summary>Returns a copy with <paramref name="attribute"/> set to <paramref name="value"/>.</summary>
    public AttributeSet With(AttributeType attribute, int value) => attribute switch
    {
        AttributeType.Strength => this with { Strength = value },
        AttributeType.Dexterity => this with { Dexterity = value },
        AttributeType.Intelligence => this with { Intelligence = value },
        AttributeType.Constitution => this with { Constitution = value },
        AttributeType.Wisdom => this with { Wisdom = value },
        AttributeType.Endurance => this with { Endurance = value },
        AttributeType.Luck => this with { Luck = value },
        _ => throw new ArgumentOutOfRangeException(nameof(attribute), attribute, null),
    };

    /// <summary>Returns a copy with <paramref name="delta"/> added to <paramref name="attribute"/>.</summary>
    public AttributeSet Add(AttributeType attribute, int delta) => With(attribute, this[attribute] + delta);

    /// <summary>Adds two sets attribute-by-attribute.</summary>
    public AttributeSet Plus(AttributeSet other) => new()
    {
        Strength = Strength + other.Strength,
        Dexterity = Dexterity + other.Dexterity,
        Intelligence = Intelligence + other.Intelligence,
        Constitution = Constitution + other.Constitution,
        Wisdom = Wisdom + other.Wisdom,
        Endurance = Endurance + other.Endurance,
        Luck = Luck + other.Luck,
    };

    public bool Equals(AttributeSet other) =>
        Strength == other.Strength && Dexterity == other.Dexterity &&
        Intelligence == other.Intelligence && Constitution == other.Constitution &&
        Wisdom == other.Wisdom && Endurance == other.Endurance && Luck == other.Luck;

    public override bool Equals(object? obj) => obj is AttributeSet other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Strength);
        hash.Add(Dexterity);
        hash.Add(Intelligence);
        hash.Add(Constitution);
        hash.Add(Wisdom);
        hash.Add(Endurance);
        hash.Add(Luck);
        return hash.ToHashCode();
    }

    public static bool operator ==(AttributeSet left, AttributeSet right) => left.Equals(right);
    public static bool operator !=(AttributeSet left, AttributeSet right) => !left.Equals(right);
}
