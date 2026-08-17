using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Modifiers;
using Dungeons.Randomness;
using Dungeons.Rules;

namespace Dungeons.Affixes;

public static class AffixTuning
{
    /// <summary>Innate cap (§3.1: 1–3, the genome speaking directly).</summary>
    public const int MaxInnates = 3;

    public const int MaxPrefixes = 3;
    public const int MaxSuffixes = 3;

    /// <summary>Weights for rolling 1/2/3 affixes per slot side. Provisional until C2c.</summary>
    public static readonly double[] CountWeights = { 0.35, 0.40, 0.25 };

    /// <summary>±band around the potency-positioned roll (§3.3's "± variance").</summary>
    public const double RollVariance = 0.10;

    /// <summary>An innate must clear this weight to exist — trace pressure earns nothing.</summary>
    public const double InnateWeightFloor = 25.0;
}

/// <summary>
/// The §4 rolling pipeline and the §3.1 innate computation. Deterministic given the seed;
/// pure apart from the injected <see cref="IRandomSource"/>. Exotic, signature and anomalous
/// classes never enter these pools (R4 decision — they arrive with E7/P4).
/// </summary>
public static class AffixRoller
{
    private static readonly IReadOnlySet<string> RollableClasses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "standard", "trigger" };

    /// <summary>Innates: eligibility → weight rank → top ≤3, potency-positioned value with
    /// zero variance, never rerollable (U-7). The floor keeps trace pressure from mattering.</summary>
    public static IReadOnlyList<RolledAffix> Innates(Genome genome, IEnumerable<AffixDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(genome);
        ArgumentNullException.ThrowIfNull(definitions);

        return definitions
            .Where(d => string.Equals(d.Slot, "innate", StringComparison.OrdinalIgnoreCase))
            .Where(d => IsEligible(d, genome, Array.Empty<string>()))
            .Select(d => (Definition: d, Weight: WeightOf(d, genome)))
            .Where(x => x.Weight >= AffixTuning.InnateWeightFloor)
            .OrderByDescending(x => x.Weight)
            .ThenBy(x => x.Definition.Id, StringComparer.Ordinal)
            .Take(AffixTuning.MaxInnates)
            .Select(x => Materialise(x.Definition, genome, position: PotencyPosition(genome.Potency)))
            .Where(r => r is not null)
            .Select(r => r!)
            .ToList();
    }

    /// <summary>The §4 pipeline for one slot side ("prefix"/"suffix"), rolling count then picks.</summary>
    public static IReadOnlyList<RolledAffix> Roll(
        Genome genome, string slot, IEnumerable<AffixDefinition> definitions, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(genome);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(random);

        var max = string.Equals(slot, "prefix", StringComparison.OrdinalIgnoreCase)
            ? AffixTuning.MaxPrefixes
            : AffixTuning.MaxSuffixes;
        var count = Math.Min(max, WeightedCount(random));

        var rolled = new List<RolledAffix>();
        var families = new List<string>();

        for (var i = 0; i < count; i++)
        {
            // Step 1 — the pool: slot matches, eligibility passes, family not already present.
            var pool = definitions
                .Where(d => string.Equals(d.Slot, slot, StringComparison.OrdinalIgnoreCase))
                .Where(d => RollableClasses.Contains(d.Class))
                .Where(d => !families.Contains(d.Family, StringComparer.OrdinalIgnoreCase))
                .Where(d => IsEligible(d, genome, families))
                .Select(d => (Definition: d, Weight: WeightOf(d, genome)))
                .Where(x => x.Weight > 0)
                .OrderBy(x => x.Definition.Id, StringComparer.Ordinal)
                .ToList();

            if (pool.Count == 0)
                break;

            // Step 3 — weighted choice.
            var total = pool.Sum(x => x.Weight);
            var pick = random.NextDouble() * total;
            var chosen = pool[^1].Definition;
            foreach (var candidate in pool)
            {
                pick -= candidate.Weight;
                if (pick <= 0)
                {
                    chosen = candidate.Definition;
                    break;
                }
            }

            // Steps 4–5 — tier ceiling by genome, value by potency ± variance.
            var variance = (random.NextDouble() * 2.0 - 1.0) * AffixTuning.RollVariance;
            var affix = Materialise(chosen, genome, PotencyPosition(genome.Potency) + variance);
            if (affix is null)
                break;

            rolled.Add(affix);
            families.Add(chosen.Family);
        }

        return rolled;
    }

    // ---- The three genetic levers (§3.3) -------------------------------------------------------

    public static bool IsEligible(AffixDefinition affix, Genome genome, IReadOnlyList<string> presentFamilies)
    {
        ArgumentNullException.ThrowIfNull(affix);
        ArgumentNullException.ThrowIfNull(genome);

        var eligibility = affix.Eligibility;

        if (eligibility.FormsAny.Count > 0
            && !eligibility.FormsAny.Any(f =>
                string.Equals(f, genome.FormId, StringComparison.OrdinalIgnoreCase)
                || genome.Tags.Contains(f, StringComparer.OrdinalIgnoreCase)))
            return false;

        if (eligibility.Requires.Any(r => genome.PressureOf(r.Property) < r.Min))
            return false;

        if (eligibility.RequiresAnyEssence.Count > 0
            && !eligibility.RequiresAnyEssence.Any(e => genome.EssenceOf(e) > 0))
            return false;

        if (eligibility.ExcludesFamily.Any(f => presentFamilies.Contains(f, StringComparer.OrdinalIgnoreCase)))
            return false;

        return true;
    }

    public static double WeightOf(AffixDefinition affix, Genome genome)
    {
        ArgumentNullException.ThrowIfNull(affix);
        ArgumentNullException.ThrowIfNull(genome);

        var weight = affix.Weight.Base;
        foreach (var scale in affix.Weight.Scale)
        {
            if (scale.Property is { Length: > 0 } property)
                weight += genome.PressureOf(property) / 10.0 * scale.Per10;
            if (scale.Essence is { Length: > 0 } essence)
                weight += genome.EssenceOf(essence) / 10.0 * scale.Per10;
        }

        return Math.Max(0, weight);
    }

    /// <summary>The highest tier (lowest number) whose requirements the genome meets; null when
    /// even the lowest tier is out of reach.</summary>
    public static AffixTier? TierFor(AffixDefinition affix, Genome genome)
    {
        ArgumentNullException.ThrowIfNull(affix);
        ArgumentNullException.ThrowIfNull(genome);

        return affix.Tiers
            .Where(t => MeetsTier(t, genome))
            .OrderBy(t => t.Tier)
            .FirstOrDefault();
    }

    private static bool MeetsTier(AffixTier tier, Genome genome) =>
        tier.Requires.All(req =>
            req.Key.StartsWith("essence.", StringComparison.OrdinalIgnoreCase)
                ? genome.EssenceOf(req.Key["essence.".Length..]) >= req.Value
                : genome.PressureOf(req.Key) >= req.Value);

    private static RolledAffix? Materialise(AffixDefinition affix, Genome genome, double position)
    {
        var tier = TierFor(affix, genome);
        if (tier is null || tier.Range.Count < 2)
            return null;

        var t = Math.Clamp(position, 0.0, 1.0);
        var value = tier.Range[0] + (tier.Range[1] - tier.Range[0]) * t;
        return new RolledAffix(affix.Id, tier.Tier, Math.Round(value, 4));
    }

    /// <summary>§3.3's roll-quality lever: potency decides where in the tier the value lands.</summary>
    public static double PotencyPosition(int potency) => 0.35 + 0.65 * Math.Clamp(potency, 0, 100) / 100.0;

    private static int WeightedCount(IRandomSource random)
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
public static class AffixGrants
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

            ModifierScope? scope = null;
            if (grant.Scope is { Length: > 0 } text)
            {
                var cut = text.IndexOf(':');
                if (cut > 0)
                    scope = new ModifierScope(text[..cut], text[(cut + 1)..]);
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
