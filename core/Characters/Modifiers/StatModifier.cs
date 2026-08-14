namespace Dungeons.Characters.Modifiers;

/// <summary>How a modifier combines with the running value.</summary>
public enum ModifierOperation
{
    /// <summary>Added to the base before any multiplications.</summary>
    Add,

    /// <summary>Multiplies the additive subtotal (e.g. 1.1 for +10%).</summary>
    Multiply,
}

/// <summary>A single typed numeric modifier against one <see cref="StatId"/>.</summary>
public readonly record struct StatModifier(StatId Stat, ModifierOperation Op, double Value);
