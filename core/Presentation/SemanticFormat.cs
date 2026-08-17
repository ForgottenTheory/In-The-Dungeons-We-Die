using System.Globalization;
using System.Text;
using Dungeons.Content;
using Dungeons.Crafting;

namespace Dungeons.Presentation;

/// <summary>What part of the pre-commit reading a line belongs to, so the client can colour
/// and group without parsing text back apart — the <c>ReactionLogKind</c> pattern.</summary>
public enum ProjectionLineKind
{
    Aim,
    Expression,
    Strengthening,
    Weakening,
    WashingOut,
    Opposition,
    TraitBirth,
    Nearby,
    Essence,
    StressWarning,
    Risk,
    Failure,
}

/// <summary>One line of the pre-commit panel.</summary>
public sealed record ProjectionLine(ProjectionLineKind Kind, string Text);

/// <summary>
/// Renders semantic readings as the player-facing text (docs/presentation-architecture.md §2–3).
/// This is the normal-play voice: qualitative words, glyphs, pips and directions — never raw
/// simulation values. The numeric voice lives in <see cref="AdvancedFormat"/>, one toggle away.
/// Pure functions of (reading, glossary) — unit-tested in Core for the same reason
/// <c>CraftFormat</c> always was: this wording is what makes destruction-at-zero fair, so it
/// is a rule, not decoration.
/// </summary>
public static class SemanticFormat
{
    // ---- The pre-commit projection (§3) ------------------------------------------------------

    /// <summary>The whole panel as plain text — the typed lines joined. One wording, two shapes.</summary>
    public static string Projection(CraftReading reading, PropertyGlossary glossary) =>
        string.Join("\n", ProjectionLines(reading, glossary).Select(l => l.Text));

    /// <summary>The pre-commit panel as typed lines, for a client that styles by kind.</summary>
    public static IReadOnlyList<ProjectionLine> ProjectionLines(CraftReading reading, PropertyGlossary glossary)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(glossary);

        if (!reading.CanCraft)
            return new[] { new ProjectionLine(ProjectionLineKind.Failure, CraftFormat.Failure(reading.Failure)) };

        var lines = new List<ProjectionLine>
        {
            new(ProjectionLineKind.Aim,
                $"Aiming at: {reading.ProjectedName}{(reading.FirstDiscovery ? "  ✦ never made before" : string.Empty)}"),
            new(ProjectionLineKind.Expression,
                $"Expression: {Tiers.Word(reading.Expression)} {ShiftArrow(reading.ExpressionShift)}"),
        };

        if (reading.Strengthening.Count > 0)
            lines.Add(new(ProjectionLineKind.Strengthening,
                "Strengthening: " + string.Join(" · ", reading.Strengthening.Select(m => MovementText(m, glossary)))));

        if (reading.Weakening.Count > 0)
            lines.Add(new(ProjectionLineKind.Weakening,
                "Weakening: " + string.Join(" · ", reading.Weakening.Select(m => MovementText(m, glossary)))));

        if (reading.WashingOut.Count > 0)
            lines.Add(new(ProjectionLineKind.WashingOut,
                "Washing out: " + string.Join(" · ", reading.WashingOut.Select(m => glossary.Label(m.Property)))));

        foreach (var opposed in reading.Opposition)
        {
            lines.Add(new(ProjectionLineKind.Opposition,
                $"Opposition: {glossary.Label(opposed.Movement.Property)} ⇄ {glossary.Name(opposed.Opposite)} — strain released"));
        }

        foreach (var birth in reading.TraitBirths)
        {
            lines.Add(new(ProjectionLineKind.TraitBirth, string.IsNullOrWhiteSpace(birth.Drawback)
                ? $"Trait born: {birth.Name}"
                : $"Trait born: {birth.Name} (drawback: {birth.Drawback})"));
        }

        foreach (var near in reading.NearbyTraits)
            lines.Add(new(ProjectionLineKind.Nearby, $"Within reach: {near.Trait.Name} — {Needs(near, glossary)}"));

        if (reading.Essence.Count > 0)
            lines.Add(new(ProjectionLineKind.Essence,
                "Essence: " + string.Join(" · ", reading.Essence.Select(e => $"{e.Name} {Tiers.Word(e.Tier)}"))));

        if (reading.VesselStressed)
            lines.Add(new(ProjectionLineKind.StressWarning,
                "⚠ The vessel is strained — more essence than its resonance can hold"));

        foreach (var risk in RiskLine(reading).Split('\n'))
            lines.Add(new(ProjectionLineKind.Risk, risk));

        return lines;
    }

    /// <summary>§2A+C compressed for a dropdown row: the leading glyphs with their pips —
    /// "▲●●●●●  !●●●●○". The inspector below the picker carries the full reading; a picker row
    /// may compress the grammar but never revert to numbers.</summary>
    public static string MaterialStrip(MaterialReading reading, PropertyGlossary glossary, int max = 3)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(glossary);

        return string.Join("  ", reading.Leading
            .Take(Math.Max(0, max))
            .Select(p => glossary.Glyph(p.Property) + Tiers.Pips(p.Tier)));
    }

    private static string MovementText(PropertyMovement movement, PropertyGlossary glossary)
    {
        var arrow = movement.Trend switch
        {
            Trend.Emerging => "✦",
            Trend.Rising => movement.CrossesTier ? "↑↑" : "↑",
            Trend.Falling => movement.CrossesTier ? "↓↓" : "↓",
            Trend.Fading => "▽",
            Trend.Vanishing => "✕",
            Trend.Conflicting => "⇄",
            Trend.Drifting => "≈",
            _ => "·",
        };

        // Emerging always crosses from None; "None → Moderate" would leak an internal word.
        var state = movement.Trend == Trend.Emerging || !movement.CrossesTier
            ? Tiers.Word(movement.TierAfter)
            : $"{Tiers.Word(movement.TierBefore)} → {Tiers.Word(movement.TierAfter)}";

        return $"{glossary.Label(movement.Property)} {arrow} {state}";
    }

    private static string RiskLine(CraftReading reading)
    {
        var substrate = reading.SubstrateName;
        var wear = Tiers.WearWord(reading.Workability.ProjectedWorkability);

        return reading.Risk switch
        {
            RiskBand.Destroys =>
                $"Risk: DESTROYS\n⚠ This will DESTROY the {substrate}. You will recover only byproducts.",
            RiskBand.Perilous =>
                $"Risk: PERILOUS — {Percent(reading.Workability.DestructionChance)} chance of destroying the {substrate}",
            RiskBand.Strained =>
                $"Risk: STRAINED — little budget would remain; the {substrate} would be {wear}",
            RiskBand.Costly =>
                $"Risk: COSTLY — a heavy bite of its budget; the {substrate} would be {wear}",
            _ =>
                $"Risk: SAFE — the {substrate} would remain {wear}",
        };
    }

    private static string Needs(NearbyTrait near, PropertyGlossary glossary) =>
        string.Join(" and ", near.Needs.Select(n =>
            n.NeedsMore ? $"needs more {glossary.Name(n.Property)}" : $"needs less {glossary.Name(n.Property)}"));

    private static string ShiftArrow(int shift) => shift > 0 ? "↑" : shift < 0 ? "↓" : "·";

    // ---- Material readings (§3 pickers and inspectors) ---------------------------------------

    public static string Material(MaterialReading reading, PropertyGlossary glossary)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(glossary);

        var builder = new StringBuilder();
        builder.Append(reading.Name).Append(" — ").Append(reading.Descriptor);

        if (reading.Leading.Count > 0)
        {
            builder.AppendLine();
            builder.Append(string.Join(" · ", reading.Leading.Select(p =>
                $"{glossary.Label(p.Property)} {Tiers.Word(p.Tier)} {Tiers.Pips(p.Tier)}")));
        }

        builder.AppendLine();
        builder.Append(BondingPhrase(reading.Bonding));

        var receptive = reading.Receptive
            .Where(r => r.Tier >= PropertyTier.Low)
            .OrderByDescending(r => r.Tier)
            .ToList();

        builder.Append(receptive.Count == 0
            ? " · inert under every medium"
            : " · " + string.Join(" · ", receptive.Select(ReceptivenessPhrase)));

        builder.AppendLine();
        builder.Append(Tiers.WearWord(reading.Workability))
            .Append(" · Expression ").Append(Tiers.Word(reading.Expression));

        foreach (var trait in reading.Traits)
        {
            builder.AppendLine();
            builder.Append("Trait: ").Append(trait.Name);
            if (!string.IsNullOrWhiteSpace(trait.Drawback))
                builder.Append(" (drawback: ").Append(trait.Drawback).Append(')');
        }

        if (reading.Essence.Count > 0)
        {
            builder.AppendLine();
            builder.Append("Essence: ").Append(string.Join(" · ",
                reading.Essence.Select(e => $"{e.Name} {Tiers.Word(e.Tier)}")));
            if (reading.VesselStressed)
                builder.Append(" — the vessel is strained");
        }

        return builder.ToString();
    }

    public static string BondingPhrase(PropertyTier bonding) => bonding switch
    {
        PropertyTier.Extreme => "Bonds ravenously",
        PropertyTier.Strong => "Bonds eagerly",
        PropertyTier.Moderate => "Accepts bonding",
        PropertyTier.Low => "Bonds reluctantly",
        _ => "Barely bonds",
    };

    public static string ReceptivenessPhrase(Receptiveness receptiveness)
    {
        ArgumentNullException.ThrowIfNull(receptiveness);

        var medium = receptiveness.Medium switch
        {
            TransferMedium.Solvent => "solvents",
            TransferMedium.Thermal => "heat",
            TransferMedium.Mechanical => "the mill",
            _ => "arcane work",
        };

        return receptiveness.Tier switch
        {
            >= PropertyTier.Strong => $"gives freely under {medium}",
            PropertyTier.Moderate => $"works under {medium}",
            _ => $"yields reluctantly to {medium}",
        };
    }

    // ---- Item cards — the §6 reveal hierarchy (R3) ---------------------------------------------

    /// <summary>The full item card: identity → combat stats → traits with drawbacks →
    /// material influence. Damage, timing, armour and resistance values are the same numbers
    /// combat uses — gameplay language, not simulation values.</summary>
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

        // §6 hierarchy positions 3–4: innates (the item potential speaking, D-21), then rolled mods.
        foreach (var innate in reading.Innates)
            lines.Add($"Innate: {innate.Text}{DrawbackSuffix(innate)}");

        foreach (var modifier in reading.Modifiers)
            lines.Add($"Mod (T{modifier.Tier}): {modifier.Text}{DrawbackSuffix(modifier)}");

        foreach (var trait in reading.Expressed)
        {
            lines.Add(string.IsNullOrWhiteSpace(trait.Drawback)
                ? $"Trait: {trait.Name}"
                : $"Trait: {trait.Name} (drawback: {trait.Drawback})");
        }

        if (reading.DormantNames.Count > 0)
            lines.Add($"Dormant: {string.Join(", ", reading.DormantNames)} — waits for a different form");

        if (reading.ComponentNames.Count > 0)
        {
            lines.Add("Made of: " + string.Join(" · ",
                reading.ComponentNames.Select(c => $"{c.Slot} {c.Material}")));
        }
        else if (reading.MadeOf.Count > 0)
        {
            lines.Add("Made of: " + string.Join(", ", reading.MadeOf));
        }

        return string.Join("\n", lines);
    }

    /// <summary>The one-line item label for lists — identity plus a compact gameplay strip.
    /// Retires <c>ItemFormat.InstanceLabel</c>'s property wall from player surfaces (D30).</summary>
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

        if (reading.Expressed.Count > 0)
            parts.Add(string.Join(", ", reading.Expressed.Select(t => t.Name)));

        var mods = reading.Innates.Count + reading.Modifiers.Count;
        if (mods > 0)
            parts.Add($"{mods} mod{(mods == 1 ? "" : "s")}");

        if (reading.DormantNames.Count > 0)
            parts.Add($"({reading.DormantNames.Count} dormant)");

        return string.Join("  —  ", parts);
    }

    private static string DrawbackSuffix(AffixLine line) =>
        line.Drawback is null ? string.Empty : $" (drawback: {line.Drawback})";

    /// <summary>"crit_chance" → "crit chance" — family slugs as words.</summary>
    public static string FamilyWord(string family) => family.Replace('_', ' ');

    /// <summary>The pre-roll supports line: what this item potential can carry, best ceiling first.
    /// The engineering half of "engineer the casino, then spin it".</summary>
    public static string Supports(IReadOnlyList<GenomeSupport> supports)
    {
        ArgumentNullException.ThrowIfNull(supports);

        return supports.Count == 0
            ? "Supports: nothing beyond its basics yet"
            : "Supports: " + string.Join(" · ", supports.Select(s => $"{FamilyWord(s.Family)} (T{s.BestTier})"));
    }

    private static string MoveText(MoveReading move)
    {
        var damage = move.Packets.Count == 0
            ? "no damage"
            : string.Join(" + ", move.Packets.Select(p => $"{p.Amount:0.#} {p.Lane}"));

        var text = $"{move.Name} — {damage} · impact {move.ImpactTicks}t · recovery {move.RecoveryTicks}t";
        return move.Costs.Count == 0 ? text : $"{text} · costs {string.Join(", ", move.Costs)}";
    }

    // ---- The fabrication preview (§16 fairness, extended by R3) --------------------------------

    /// <summary>The pre-commit fabrication card: what would be made, shown through the same
    /// reading the minted item will produce.</summary>
    public static string Fabrication(
        EquipmentAssemblyPreview projection, ItemReading reading, IReadOnlyList<GenomeSupport>? supports = null)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(reading);

        if (!projection.CanFabricate)
            return FabricationFailureText(projection.Failure);

        var head = $"Would make: {reading.Name}{(projection.WouldBeFirstOfItsKind ? "  ✦ first of its kind" : string.Empty)}";
        var body = Item(reading);
        var bodyWithoutName = body.IndexOf('\n') is var cut && cut >= 0 ? body[(cut + 1)..] : string.Empty;

        var text = bodyWithoutName.Length == 0 ? head : head + "\n" + bodyWithoutName;
        if (supports is { Count: > 0 })
            text += "\n" + Supports(supports);

        return text;
    }

    public static string FabricationFailureText(EquipmentAssemblyFailure failure) => failure switch
    {
        EquipmentAssemblyFailure.None => string.Empty,
        EquipmentAssemblyFailure.MissingSlot => "Choose a material for every slot.",
        EquipmentAssemblyFailure.SlotRejected => "A slot will not take the material chosen for it.",
        EquipmentAssemblyFailure.MissingInputs => "You do not have the materials.",
        _ => "Unknown form or material.",
    };

    // ---- Slot fit (§2E contextual meaning at the fabrication bench) -----------------------------

    /// <summary>Why this material suits (or doesn't suit) this slot, in one line — derived
    /// from the form's own stat_map, apertures and tag gates, never a generic string.</summary>
    public static string SlotFit(SlotReading reading, PropertyGlossary glossary)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(glossary);

        if (!reading.Eligible)
            return $"won't take this — needs {string.Join(" or ", reading.RequiredTags.Select(TagWord))}";

        var parts = new List<string>();
        if (reading.EligibleVia is { } via)
            parts.Add($"fits as {TagWord(via)}");
        parts.Add(MassShareWord(reading.MassShare));

        foreach (var read in reading.Reads.Where(r => !r.SharedAcrossSlots))
            parts.Add($"reads {glossary.Label(read.Property)} {WeightWord(read.Weight)} — {Tiers.Word(read.MaterialTier)} here");

        foreach (var fit in reading.Traits)
            parts.Add($"{fit.TraitName} would {ApertureWord(fit.Band)}");

        return string.Join(" · ", parts);
    }

    public static string MassShareWord(double share) => share switch
    {
        >= 0.5 => "bears most of the item",
        >= 0.25 => "a fair share of the item",
        _ => "a sliver of the item",
    };

    public static string WeightWord(ReadWeight weight) => weight switch
    {
        ReadWeight.Heavy => "heavily",
        ReadWeight.Moderate => "firmly",
        _ => "lightly",
    };

    public static string ApertureWord(TraitExpressionBand band) => band switch
    {
        TraitExpressionBand.Full => "express fully",
        TraitExpressionBand.Partial => "express partly",
        _ => "be muted here",
    };

    // ---- Process labels (§3 pickers) ----------------------------------------------------------

    public static string Process(CraftingActionDefinition craftingAction, string professionName)
    {
        ArgumentNullException.ThrowIfNull(craftingAction);

        var gate = craftingAction.IsUngated
            ? "any skill"
            : $"{professionName} L{craftingAction.Requires.ProfessionLevel}";

        var works = craftingAction.Requires.SubstrateTags.Count == 0
            ? "anything"
            : string.Join(" or ", craftingAction.Requires.SubstrateTags.Select(TagWord));

        return $"{craftingAction.Name} — {SeverityWord(craftingAction.Severity)}; works {works}. {gate}";
    }

    public static string AffectedQualities(CraftingActionDefinition craftingAction, PropertyGlossary glossary)
    {
        ArgumentNullException.ThrowIfNull(craftingAction);
        ArgumentNullException.ThrowIfNull(glossary);

        if (craftingAction.AffectedQualities.Count == 0)
            return "(drives nothing)";

        return "Drives " + string.Join(" · ", craftingAction.AffectedQualities
            .OrderByDescending(c => c.Rate)
            .ThenBy(c => c.Property, StringComparer.Ordinal)
            .Select(c => $"{glossary.Label(c.Property)} {RateWord(c.Rate)}"));
    }

    public static string SeverityWord(double severity) => severity switch
    {
        < PresentationTuning.GentleSeverity => "gentle",
        < PresentationTuning.FirmSeverity => "firm",
        < PresentationTuning.ForcefulSeverity => "forceful",
        _ => "violent",
    };

    public static string RateWord(double rate) => rate switch
    {
        >= 0.6 => "hard",
        >= 0.4 => "firmly",
        >= 0.25 => "gently",
        _ => "faintly",
    };

    private static string TagWord(string tag)
    {
        var index = tag.IndexOf(':');
        return index >= 0 && index < tag.Length - 1 ? tag[(index + 1)..] : tag;
    }

    private static string Percent(double fraction) =>
        Math.Round(fraction * 100, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "%";
}
