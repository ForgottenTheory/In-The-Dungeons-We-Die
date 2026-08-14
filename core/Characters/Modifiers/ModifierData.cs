namespace Dungeons.Characters.Modifiers;

/// <summary>
/// JSON-facing representation of a modifier on a component definition, e.g.
/// <c>{ "stat": "Strength", "op": "Add", "value": 2 }</c>. Enums parse from
/// strings (case-insensitive) via the DataStore's converters.
/// </summary>
public sealed class ModifierData
{
    public StatId Stat { get; init; }
    public ModifierOperation Op { get; init; } = ModifierOperation.Add;
    public double Value { get; init; }

    public StatModifier ToModifier() => new(Stat, Op, Value);
}
