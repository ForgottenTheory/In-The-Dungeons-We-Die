using Dungeons.Content;

namespace Dungeons.Crafting;

/// <summary>One reagent step's essence movement, fully described for the log.</summary>
public sealed record EssenceStepResult(
    IReadOnlyDictionary<string, double> Essence,
    IReadOnlyList<(string Key, double Before, double After)> Changes,
    double StressReleased);

/// <summary>
/// The §8.4/§8.5 essence algebra, run beside the property algebra each reagent step.
///
/// <para>Transfer is <b>additive</b>: <c>gain = reagent.essence × essence_rate × A × S ×
/// quality</c>, with a bonus when the crafting action channel moves the essence's anchor. Essence
/// never converges toward zero on its own — it must be diluted deliberately or annihilated by
/// opposition, which cancels the overlap at §8.5's rate and releases it as strain the
/// workability cost feels. Only the asymmetry survives: you cannot stockpile opposites.</para>
/// </summary>
public static class EssenceAlgebra
{
    public static EssenceStepResult Apply(
        IReadOnlyDictionary<string, double> substrate,
        IReadOnlyDictionary<string, double> reagent,
        CraftingActionDefinition craftingAction,
        TransferCoefficients coefficients,
        DataStore<EssenceDefinition> essences)
    {
        ArgumentNullException.ThrowIfNull(substrate);
        ArgumentNullException.ThrowIfNull(reagent);
        ArgumentNullException.ThrowIfNull(craftingAction);
        ArgumentNullException.ThrowIfNull(coefficients);
        ArgumentNullException.ThrowIfNull(essences);

        var result = new Dictionary<string, double>(substrate, StringComparer.OrdinalIgnoreCase);
        var changes = new List<(string, double, double)>();

        // ---- §8.4 transfer -----------------------------------------------------------------
        foreach (var definition in essences.GetAll().OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            var incoming = reagent.GetValueOrDefault(definition.Key);
            if (incoming <= 0)
                continue;

            var anchorInChannel = craftingAction.AffectedQualities.Any(c =>
                string.Equals(c.Property, definition.Anchor, StringComparison.OrdinalIgnoreCase));

            var gain = incoming
                * craftingAction.EssenceRate
                * coefficients.Compatibility
                * coefficients.TransferStrength
                * coefficients.QualityMultiplier
                * (anchorInChannel ? 1.0 + EssenceTuning.AnchorChannelBonus : 1.0);

            if (gain <= 0)
                continue;

            var before = result.GetValueOrDefault(definition.Key);
            var after = Math.Min(100, before + gain);
            result[definition.Key] = after;
            changes.Add((definition.Key, before, after));
        }

        // ---- §8.5 opposition — only the asymmetry survives ----------------------------------
        // Pairs are collected unordered first, so an opposition authored on either side (or
        // both) annihilates exactly once.
        var pairs = new HashSet<(string A, string B)>();
        foreach (var definition in essences.GetAll())
        {
            foreach (var opposed in definition.Opposes)
                pairs.Add(string.CompareOrdinal(definition.Key, opposed) < 0
                    ? (definition.Key, opposed)
                    : (opposed, definition.Key));
        }

        var released = 0.0;
        foreach (var (aKey, bKey) in pairs.OrderBy(p => p.A, StringComparer.Ordinal).ThenBy(p => p.B, StringComparer.Ordinal))
        {
            var a = result.GetValueOrDefault(aKey);
            var b = result.GetValueOrDefault(bKey);
            var overlap = Math.Min(a, b);
            if (overlap <= 0)
                continue;

            var cancelled = overlap * MaterialTransformationTuning.ConflictAnnihilationRate;
            Record(result, changes, aKey, a, a - cancelled);
            Record(result, changes, bKey, b, b - cancelled);
            released += cancelled;
        }

        // ---- Floor — a whisper of essence is no essence at all ------------------------------
        foreach (var key in result.Keys.ToList())
        {
            if (result[key] < EssenceTuning.Floor)
                result.Remove(key);
        }

        return new EssenceStepResult(result, changes, released);
    }

    private static void Record(
        Dictionary<string, double> essence, List<(string, double, double)> changes,
        string key, double before, double after)
    {
        essence[key] = after;
        changes.Add((key, before, after));
    }
}
