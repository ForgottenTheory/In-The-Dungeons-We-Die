namespace Dungeons.Items;

/// <summary>
/// The two property keys authored equipment may carry — the combat-unit channel the
/// resolver reads (mass → damage/windup, hardness → armour). The wider property vocabulary
/// died with the property crafting system (Phase 7, D54).
/// </summary>
public static class ItemProperties
{
    public const string Hardness = "hardness";
    public const string Mass = "mass";
}
