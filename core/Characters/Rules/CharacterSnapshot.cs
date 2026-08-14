namespace Dungeons.Characters.Rules;

/// <summary>
/// An immutable read-only view of a character's current state, handed to rule
/// hooks so they can contribute conditional effects without mutating anything.
/// </summary>
public readonly struct CharacterSnapshot
{
    public AttributeSet BaseAttributes { get; init; }
    public int Health { get; init; }
    public int MaxHealth { get; init; }
    public int Mana { get; init; }
    public int MaxMana { get; init; }
    public int Stamina { get; init; }
    public int MaxStamina { get; init; }

    /// <summary>Current health as a fraction of max in [0, 1].</summary>
    public double HealthFraction => MaxHealth <= 0 ? 0.0 : (double)Health / MaxHealth;
}
