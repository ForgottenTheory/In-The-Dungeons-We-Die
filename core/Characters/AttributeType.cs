namespace Dungeons.Characters;

/// <summary>The seven core character attributes (see docs/vertical-slice.md §4).</summary>
public enum AttributeType
{
    Strength,
    Dexterity,
    Intelligence,
    Constitution,
    Wisdom,
    Endurance,
    Luck,
}

public static class AttributeTypes
{
    /// <summary>All attributes in canonical order. Handy for iteration and display.</summary>
    public static readonly IReadOnlyList<AttributeType> All = new[]
    {
        AttributeType.Strength,
        AttributeType.Dexterity,
        AttributeType.Intelligence,
        AttributeType.Constitution,
        AttributeType.Wisdom,
        AttributeType.Endurance,
        AttributeType.Luck,
    };
}
