namespace Dungeons.Characters.Modifiers;

/// <summary>
/// Every numeric target a <see cref="StatModifier"/> can address: the seven
/// attributes plus the three derived resource maxima. Attribute-targeted
/// modifiers are applied before resources are derived; resource-max modifiers
/// are applied afterwards.
/// </summary>
public enum StatId
{
    Strength,
    Dexterity,
    Intelligence,
    Constitution,
    Wisdom,
    Endurance,
    Luck,
    MaxHealth,
    MaxMana,
    MaxStamina,
}

public static class StatIds
{
    /// <summary>The seven attribute-valued stat ids, in canonical order.</summary>
    public static readonly IReadOnlyList<StatId> Attributes = new[]
    {
        StatId.Strength,
        StatId.Dexterity,
        StatId.Intelligence,
        StatId.Constitution,
        StatId.Wisdom,
        StatId.Endurance,
        StatId.Luck,
    };

    public static bool IsAttribute(StatId stat) => stat <= StatId.Luck;

    /// <summary>Maps an attribute-valued <see cref="StatId"/> to its <see cref="AttributeType"/>.</summary>
    public static AttributeType ToAttribute(StatId stat) => stat switch
    {
        StatId.Strength => AttributeType.Strength,
        StatId.Dexterity => AttributeType.Dexterity,
        StatId.Intelligence => AttributeType.Intelligence,
        StatId.Constitution => AttributeType.Constitution,
        StatId.Wisdom => AttributeType.Wisdom,
        StatId.Endurance => AttributeType.Endurance,
        StatId.Luck => AttributeType.Luck,
        _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, "Not an attribute stat."),
    };
}
