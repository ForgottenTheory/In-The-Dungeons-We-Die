namespace Dungeons.Items;

/// <summary>Broad category of an item, used to route it in inventory/equip/craft flows.</summary>
public enum ItemType
{
    Material,
    Weapon,
    Armor,
    Consumable,
}

/// <summary>Crafted quality tiers (docs/crafting.md §12). Affects derived effectiveness.</summary>
public enum ItemQuality
{
    Poor,
    Normal,
    Fine,
    Exceptional,
    Masterwork,
}
