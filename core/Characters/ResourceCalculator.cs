namespace Dungeons.Characters;

/// <summary>
/// Derives base maximum resources from an attribute set. Coefficients are
/// placeholder balance values for the vertical slice; docs/damage-and-defense.md keeps the exact
/// formulas as configurable balance data, so these live behind one seam.
/// </summary>
public static class ResourceCalculator
{
    public static int MaxHealth(AttributeSet a) => 20 + (a.Constitution * 6) + (a.Endurance * 3);

    public static int MaxStamina(AttributeSet a) => 20 + (a.Endurance * 5) + (a.Dexterity * 2);

    public static int MaxMana(AttributeSet a) => 10 + (a.Intelligence * 5) + (a.Wisdom * 3);

    public static int MaxFor(ResourceType type, AttributeSet a) => type switch
    {
        ResourceType.Health => MaxHealth(a),
        ResourceType.Mana => MaxMana(a),
        ResourceType.Stamina => MaxStamina(a),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}
