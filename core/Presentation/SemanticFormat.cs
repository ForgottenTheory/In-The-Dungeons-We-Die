using Dungeons.Crafting.Identity;

namespace Dungeons.Presentation;

/// <summary>
/// Renders item readings as the player-facing text (docs/presentation-architecture.md).
/// This is the normal-play voice: names, identity words and gameplay numbers — never engine
/// ids. Pure functions of a reading — unit-tested in Core, because this wording is a rule,
/// not decoration.
///
/// <para>The property-model half of this class (material readings, craft projections, slot
/// fit, process labels) died with the old crafting system in migration Phase 7 (D54); the
/// identity system's other voices live in <see cref="SentenceReadings"/>,
/// <see cref="MintReadings"/>, <see cref="VerbReadings"/> and
/// <see cref="IdentityMaterialReadings"/>.</para>
/// </summary>
public static class SemanticFormat
{
    /// <summary>The full item card: identity → combat stats → effects → material influence.
    /// Damage, timing, armour and resistance values are the same numbers combat uses —
    /// gameplay language, not simulation values.</summary>
    public static string Item(ItemReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        var lines = new List<string> { $"{reading.Name} — {reading.Slot}" };

        foreach (var move in reading.Moves)
            lines.Add("Grants: " + MoveText(move));

        if (reading.Armor > 0 || reading.Resistances.Count > 0)
        {
            var resist = reading.Resistances.Count == 0
                ? string.Empty
                : " · resists " + string.Join(", ", reading.Resistances
                    .OrderBy(r => r.Key, StringComparer.Ordinal)
                    .Select(r => $"{r.Key} {r.Value:P0}"));
            lines.Add($"Armour {reading.Armor:0.#}{resist}");
        }

        // The identity layer (Phase 6). D50's taxonomy stays legible on the card: the floor
        // is labelled as the promise, the Signature as the earned layer, the drawback as the
        // price — the unlabelled lines are the roll.
        if (reading.ExpressedIdentityNames.Count > 0)
            lines.Add("Identities: " + string.Join(" · ", reading.ExpressedIdentityNames));

        foreach (var effect in reading.IdentityEffects)
            lines.Add(IdentityEffectLine(effect));

        if (reading.DormantNames.Count > 0)
            lines.Add($"Dormant: {string.Join(", ", reading.DormantNames)} — waits for a different form");

        if (reading.MadeOf.Count > 0)
            lines.Add("Made of: " + string.Join(", ", reading.MadeOf));

        return string.Join("\n", lines);
    }

    /// <summary>The one-line item label for lists — identity plus a compact gameplay strip.</summary>
    public static string ItemStrip(ItemReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        var parts = new List<string> { reading.Name };

        var attack = reading.Moves.FirstOrDefault();
        if (attack is not null && attack.Packets.Count > 0)
            parts.Add($"dmg {attack.Packets.Sum(p => p.Amount):0.#} · impact {attack.ImpactTicks}t");

        if (reading.Armor > 0)
            parts.Add($"armour {reading.Armor:0.#}");
        else if (reading.Resistances.Count > 0)
            parts.Add("resists " + string.Join(", ", reading.Resistances.Keys.OrderBy(k => k, StringComparer.Ordinal)));

        if (reading.ExpressedIdentityNames.Count > 0)
            parts.Add(string.Join(", ", reading.ExpressedIdentityNames));

        if (reading.IdentityEffects.Count > 0)
        {
            var effectCount = reading.IdentityEffects.Count;
            parts.Add($"{effectCount} effect{(effectCount == 1 ? "" : "s")}");
            if (reading.IdentityEffects.Any(e => e.Category == ItemEffectCategory.Signature))
                parts.Add("Signature");
            if (reading.IdentityEffects.Any(e => e.Category == ItemEffectCategory.Drawback))
                parts.Add("Drawback");
        }

        if (reading.DormantNames.Count > 0)
            parts.Add($"({reading.DormantNames.Count} dormant)");

        return string.Join("  —  ", parts);
    }

    /// <summary>One identity effect on the card. Category labels keep D50's taxonomy apart;
    /// the unlabelled generated lines are recognizable as the roll by their bareness.</summary>
    private static string IdentityEffectLine(SentenceReading effect) => effect.Category switch
    {
        ItemEffectCategory.Floor => $"Guaranteed: {effect.Text}",
        ItemEffectCategory.Signature => $"Signature: {effect.Text}",
        ItemEffectCategory.Drawback => $"Drawback: {effect.Text}",
        _ => effect.Text,
    };

    private static string MoveText(MoveReading move)
    {
        var damage = move.Packets.Count == 0
            ? "no damage"
            : string.Join(" + ", move.Packets.Select(p => $"{p.Amount:0.#} {p.Lane}"));

        var text = $"{move.Name} — {damage} · impact {move.ImpactTicks}t · recovery {move.RecoveryTicks}t";
        return move.Costs.Count == 0 ? text : $"{text} · costs {string.Join(", ", move.Costs)}";
    }
}
