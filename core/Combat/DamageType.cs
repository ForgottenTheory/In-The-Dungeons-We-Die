namespace Dungeons.Combat;

/// <summary>Initial damage categories (docs/combat-spec.md §15). Magic may gain subtypes later.</summary>
public enum DamageType
{
    Slashing,
    Crushing,
    Piercing,
    Magic,
}

public static class DamageTypes
{
    public static bool IsPhysical(DamageType type) => type != DamageType.Magic;
}
