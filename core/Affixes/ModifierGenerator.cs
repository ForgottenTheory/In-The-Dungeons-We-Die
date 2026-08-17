using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Modifiers;
using Dungeons.Randomness;
using Dungeons.Rules;

namespace Dungeons.Affixes;

public static class AffixTuning
{
    /// <summary>Innate cap (§3.1: 1–3, the item potential speaking directly).</summary>
    public const int MaxInnates = 3;

    public const int MaxPrefixes = 3;
    public const int MaxSuffixes = 3;

    /// <summary>Weights for rolling 1/2/3 affixes per slot side. Provisional until C2c.</summary>
    public static readonly double[] CountWeights = { 0.35, 0.40, 0.25 };

    /// <summary>±band around the material strength-positioned roll (§3.3's "± variance").</summary>
    public const double RollVariance = 0.10;

    /// <summary>An innate must clear this weight to exist — trace material influence earns nothing.</summary>
    public const double InnateChanceWeightFloor = 25.0;

    /// <summary>
    /// Where in a tier's [lo, hi] range an item of zero material strength lands. A floor rather
    /// than zero, so a weak item still gets a usable modifier — material strength is roll
    /// <i>quality</i>, never a gate.
    /// </summary>
    public const double MinRollPosition = 0.35;

    /// <summary>How much of the tier range material strength buys, on top of
    /// <see cref="MinRollPosition"/>. The two sum to 1.0, so material strength 100 reaches the
    /// top of the tier.</summary>
    public const double MaterialStrengthRollSpan = 0.65;
}

/// <summary>
/// Rolls the modifiers an assembled item gets, reading nothing but its
/// <see cref="ItemPotential"/> (§4 rolling, §3.1 innates).
///
/// <para>Four questions, four pure functions: <see cref="IsAvailableFor"/> (can it roll at all),
/// <see cref="ChanceWeightFor"/> (how likely), <see cref="MaximumModifierTier"/> (how strong it
/// may get) and <see cref="RollPositionFor"/> (where in that tier the value lands).</para>
///
/// <para>Deterministic given the seed; pure apart from the injected
/// <see cref="IRandomSource"/>. Exotic, signature and anomalous classes never enter these pools
/// (R4 decision — they arrive with E7/P4).</para>
/// </summary>
public static class ModifierGenerator
{
    private static readonly IReadOnlySet<string> RollableClasses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "standard", "trigger" };

    /// <summary>Innates: availability → weight rank → top ≤3, material strength-positioned value with
    /// zero variance, never rerollable (U-7). The floor keeps trace material influence from mattering.</summary>
    public static IReadOnlyList<RolledAffix> Innates(ItemPotential itemPotential, IEnumerable<AffixDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(itemPotential);
        ArgumentNullException.ThrowIfNull(definitions);

        return definitions
            .Where(affix => string.Equals(affix.Slot, "innate", StringComparison.OrdinalIgnoreCase))
            .Where(affix => IsAvailableFor(affix, itemPotential, Array.Empty<string>()))
            .Select(affix => (Definition: affix, Weight: ChanceWeightFor(affix, itemPotential)))
            .Where(candidate => candidate.Weight >= AffixTuning.InnateChanceWeightFloor)
            .OrderByDescending(candidate => candidate.Weight)
            .ThenBy(candidate => candidate.Definition.Id, StringComparer.Ordinal)
            .Take(AffixTuning.MaxInnates)
            .Select(candidate => RollValue(
                candidate.Definition, itemPotential, positionInTierRange: RollPositionFor(itemPotential.MaterialStrength)))
            .Where(rolled => rolled is not null)
            .Select(rolled => rolled!)
            .ToList();
    }

    /// <summary>The §4 pipeline for one slot side ("prefix"/"suffix"), rolling count then picks.</summary>
    public static IReadOnlyList<RolledAffix> Roll(
        ItemPotential itemPotential, string slot, IEnumerable<AffixDefinition> definitions, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(itemPotential);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(random);

        var maxForSlot = string.Equals(slot, "prefix", StringComparison.OrdinalIgnoreCase)
            ? AffixTuning.MaxPrefixes
            : AffixTuning.MaxSuffixes;
        var affixCount = Math.Min(maxForSlot, RollAffixCount(random));

        var rolledAffixes = new List<RolledAffix>();
        var familiesAlreadyRolled = new List<string>();

        for (var i = 0; i < affixCount; i++)
        {
            // Step 1 — the pool: slot matches, availability passes, family not already present.
            var pool = definitions
                .Where(affix => string.Equals(affix.Slot, slot, StringComparison.OrdinalIgnoreCase))
                .Where(affix => RollableClasses.Contains(affix.Class))
                .Where(affix => !familiesAlreadyRolled.Contains(affix.Family, StringComparer.OrdinalIgnoreCase))
                .Where(affix => IsAvailableFor(affix, itemPotential, familiesAlreadyRolled))
                .Select(affix => (Definition: affix, Weight: ChanceWeightFor(affix, itemPotential)))
                .Where(candidate => candidate.Weight > 0)
                .OrderBy(candidate => candidate.Definition.Id, StringComparer.Ordinal)
                .ToList();

            if (pool.Count == 0)
                break;

            // Step 3 — weighted choice.
            var totalWeight = pool.Sum(candidate => candidate.Weight);
            var weightedRoll = random.NextDouble() * totalWeight;
            var chosen = pool[^1].Definition;
            foreach (var candidate in pool)
            {
                weightedRoll -= candidate.Weight;
                if (weightedRoll <= 0)
                {
                    chosen = candidate.Definition;
                    break;
                }
            }

            // Steps 4–5 — tier ceiling by item potential, value by material strength ± variance.
            var variance = ((random.NextDouble() * 2.0) - 1.0) * AffixTuning.RollVariance;
            var affixRoll = RollValue(chosen, itemPotential, RollPositionFor(itemPotential.MaterialStrength) + variance);
            if (affixRoll is null)
                break;

            rolledAffixes.Add(affixRoll);
            familiesAlreadyRolled.Add(chosen.Family);
        }

        return rolledAffixes;
    }

    // ---- The three genetic levers (§3.3) -------------------------------------------------------

    public static bool IsAvailableFor(
        AffixDefinition affix,
        ItemPotential itemPotential,
        IReadOnlyList<string> familiesAlreadyPresent)
    {
        ArgumentNullException.ThrowIfNull(affix);
        ArgumentNullException.ThrowIfNull(itemPotential);

        var availability = affix.Availability;

        if (availability.FormsAny.Count > 0
            && !availability.FormsAny.Any(f =>
                string.Equals(f, itemPotential.BlueprintId, StringComparison.OrdinalIgnoreCase)
                || itemPotential.Tags.Contains(f, StringComparer.OrdinalIgnoreCase)))
            return false;

        if (availability.Requires.Any(r => itemPotential.InfluenceOf(r.Property) < r.Min))
            return false;

        if (availability.RequiresAnyEssence.Count > 0
            && !availability.RequiresAnyEssence.Any(e => itemPotential.EssenceOf(e) > 0))
            return false;

        if (availability.ExcludesFamily.Any(f => familiesAlreadyPresent.Contains(f, StringComparer.OrdinalIgnoreCase)))
            return false;

        return true;
    }

    public static double ChanceWeightFor(AffixDefinition affix, ItemPotential itemPotential)
    {
        ArgumentNullException.ThrowIfNull(affix);
        ArgumentNullException.ThrowIfNull(itemPotential);

        var weight = affix.ChanceWeight.Base;
        foreach (var scale in affix.ChanceWeight.Scale)
        {
            if (scale.Property is { Length: > 0 } property)
                weight += itemPotential.InfluenceOf(property) / 10.0 * scale.PerTenInfluence;
            if (scale.Essence is { Length: > 0 } essence)
                weight += itemPotential.EssenceOf(essence) / 10.0 * scale.PerTenInfluence;
        }

        return Math.Max(0, weight);
    }

    /// <summary>The highest tier (lowest number) whose requirements the item potential meets; null when
    /// even the lowest tier is out of reach.</summary>
    public static AffixTier? MaximumModifierTier(AffixDefinition affix, ItemPotential itemPotential)
    {
        ArgumentNullException.ThrowIfNull(affix);
        ArgumentNullException.ThrowIfNull(itemPotential);

        return affix.Tiers
            .Where(t => ItemPotentialMeetsTier(t, itemPotential))
            .OrderBy(t => t.Tier)
            .FirstOrDefault();
    }

    private static bool ItemPotentialMeetsTier(AffixTier tier, ItemPotential itemPotential) =>
        tier.Requires.All(req =>
            req.Key.StartsWith("essence.", StringComparison.OrdinalIgnoreCase)
                ? itemPotential.EssenceOf(req.Key["essence.".Length..]) >= req.Value
                : itemPotential.InfluenceOf(req.Key) >= req.Value);

    /// <summary>
    /// Turns a definition into a concrete rolled affix: pick the best tier the item potential qualifies
    /// for, then land the value at <paramref name="positionInTierRange"/> within that tier's
    /// [lo, hi]. Null when even the lowest tier is out of the item potential's reach.
    /// </summary>
    /// <param name="positionInTierRange">0 = the bottom of the tier, 1 = the top. Clamped.</param>
    private static RolledAffix? RollValue(AffixDefinition affix, ItemPotential itemPotential, double positionInTierRange)
    {
        var tier = MaximumModifierTier(affix, itemPotential);
        if (tier is null || tier.Range.Count < 2)
            return null;

        var clampedPosition = Math.Clamp(positionInTierRange, 0.0, 1.0);
        var value = tier.Range[0] + ((tier.Range[1] - tier.Range[0]) * clampedPosition);
        return new RolledAffix(affix.Id, tier.Tier, Math.Round(value, 4));
    }

    /// <summary>§3.3's roll-quality lever: material strength decides where in the tier the value lands.</summary>
    public static double RollPositionFor(int materialStrength) =>
        AffixTuning.MinRollPosition
        + (AffixTuning.MaterialStrengthRollSpan * Math.Clamp(materialStrength, 0, 100) / 100.0);

    /// <summary>How many affixes this slot side rolls, drawn from <see cref="AffixTuning.CountWeights"/>.</summary>
    private static int RollAffixCount(IRandomSource random)
    {
        var roll = random.NextDouble() * AffixTuning.CountWeights.Sum();
        for (var i = 0; i < AffixTuning.CountWeights.Length; i++)
        {
            roll -= AffixTuning.CountWeights[i];
            if (roll <= 0)
                return i + 1;
        }

        return AffixTuning.CountWeights.Length;
    }
}

/// <summary>Turns rolled affixes back into live grants and player text — the one place `$roll`
/// is substituted, so the tooltip and the mechanics can never drift (§8's parity rule).</summary>
public static class ModifierGrants
{
    public static IEnumerable<ModifierContribution> Contributions(
        RolledAffix rolled, AffixDefinition definition, string source)
    {
        ArgumentNullException.ThrowIfNull(rolled);
        ArgumentNullException.ThrowIfNull(definition);

        foreach (var grant in definition.Grants)
        {
            if (!string.Equals(grant.Type, "stat", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = string.Equals(grant.Value, "$roll", StringComparison.OrdinalIgnoreCase)
                ? rolled.Roll
                : double.Parse(grant.Value, System.Globalization.CultureInfo.InvariantCulture);

            // A scope is authored as "dimension:value", e.g. "lane:heat". The value half may
            // itself contain colons ("move_tag:mech:heavy"), so only the FIRST colon splits.
            ModifierScope? scope = null;
            if (grant.Scope is { Length: > 0 } scopeText)
            {
                var dimensionEnd = scopeText.IndexOf(':');
                if (dimensionEnd > 0)
                    scope = new ModifierScope(scopeText[..dimensionEnd], scopeText[(dimensionEnd + 1)..]);
            }

            yield return new ModifierContribution(grant.Key, value, source, scope);
        }
    }

    public static IEnumerable<TriggerRule> Rules(RolledAffix rolled, AffixDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(rolled);
        ArgumentNullException.ThrowIfNull(definition);

        foreach (var grant in definition.Grants)
        {
            if (!string.Equals(grant.Type, "rule", StringComparison.OrdinalIgnoreCase) || grant.Rule is null)
                continue;

            yield return grant.RollInto.ToLowerInvariant() switch
            {
                "chance" => CloneWithChance(grant.Rule, rolled.Roll),
                "amount" => CloneWithAmount(grant.Rule, rolled.Roll),
                _ => grant.Rule,
            };
        }
    }

    /// <summary>The player line: "$roll%" renders ×100 ("12% chance to Bleed"); bare "$roll"
    /// renders the mechanical value. Never shown without substitution.</summary>
    public static string Describe(RolledAffix rolled, AffixDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(rolled);
        ArgumentNullException.ThrowIfNull(definition);

        var text = definition.Description;
        if (text.Contains("$roll%", StringComparison.Ordinal))
            return text.Replace("$roll%", $"{Math.Round(rolled.Roll * 100):0}%", StringComparison.Ordinal);

        // Flats read best at one decimal; factors keep two. Display precision only — the
        // mechanical value is always the full roll.
        var formatted = Math.Abs(rolled.Roll) >= 3
            ? rolled.Roll.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
            : rolled.Roll.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        return text.Replace("$roll", formatted, StringComparison.Ordinal);
    }

    private static TriggerRule CloneWithChance(TriggerRule rule, double chance) => new()
    {
        Id = rule.Id,
        Event = rule.Event,
        When = rule.When,
        Effect = rule.Effect,
        Effects = rule.Effects,
        Target = rule.Target,
        Proc = rule.Proc,
        CooldownTicks = rule.CooldownTicks,
        Chance = chance,
        Description = rule.Description,
    };

    private static TriggerRule CloneWithAmount(TriggerRule rule, double amount) => new()
    {
        Id = rule.Id,
        Event = rule.Event,
        When = rule.When,
        Effect = new EffectSpec
        {
            Kind = rule.Effect.Kind,
            Amount = amount,
            ScalesWith = rule.Effect.ScalesWith,
            Text = rule.Effect.Text,
            DurationTicks = rule.Effect.DurationTicks,
            Chance = rule.Effect.Chance,
            Target = rule.Effect.Target,
        },
        Effects = rule.Effects,
        Target = rule.Target,
        Proc = rule.Proc,
        CooldownTicks = rule.CooldownTicks,
        Chance = rule.Chance,
        Description = rule.Description,
    };
}
