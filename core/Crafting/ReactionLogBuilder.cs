using System.Globalization;
using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Crafting;

/// <summary>Everything one reagent step needs to explain itself.</summary>
public sealed record ReactionStepContext(
    ProcessDefinition Process,
    string SubstrateName,
    string ReagentName,
    PropertySet Substrate,
    PropertySet Reagent,
    ReactionStepResult Result,
    int IntegrityBefore,
    int IntegrityAfter,
    double IntegrityCost);

/// <summary>
/// Turns a craft into the trace of docs/emergent-item-system.md §15.3.
///
/// <para>Every line answers "why", not just "what" — that is the difference between a log the
/// player learns the system from and a wall of numbers they stop reading.</para>
/// </summary>
public sealed class ReactionLogBuilder
{
    /// <summary>Movements smaller than this are summarised rather than listed. Off-channel
    /// drift touches many properties by fractions; printing all of them buries the two lines
    /// that actually mattered.</summary>
    private const double NoteworthyChange = 0.5;

    private readonly DataStore<PropertyDefinition> _properties;
    private readonly List<ReactionLogEntry> _entries = new();

    public ReactionLogBuilder(DataStore<PropertyDefinition> properties)
    {
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
    }

    public ReactionLog Build() => new(_entries.ToList());

    /// <summary>Appends one reagent step: header, coefficients, property movements, integrity.</summary>
    public ReactionLogBuilder Step(ReactionStepContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Add(ReactionLogKind.Step, $"{context.Process.Name} — {context.SubstrateName} ← {context.ReagentName}");
        Coefficients(context);
        Changes(context);
        Integrity(context);

        return this;
    }

    private void Coefficients(ReactionStepContext context)
    {
        var coefficients = context.Result.Coefficients;
        var affinity = context.Substrate.Get(ItemProperties.Affinity);

        Add(ReactionLogKind.Coefficient,
            $"Acceptance {Coefficient(coefficients.Acceptance)} — {context.SubstrateName} "
            + $"{AcceptanceVerb(affinity)} bonding (affinity {Value(affinity)})",
            indent: 1,
            property: ItemProperties.Affinity,
            after: coefficients.Acceptance);

        var mediumProperty = MediumPropertyName(context.Process.Medium);
        var mediumValue = ReactionCoefficients.MediumProperty(context.Process.Medium, context.Reagent);

        Add(ReactionLogKind.Coefficient,
            $"Release {Coefficient(coefficients.Release)} — {context.ReagentName} "
            + $"{ReleaseVerb(mediumValue)} under {context.Process.Medium.ToString().ToLowerInvariant()} "
            + $"({mediumProperty} {Value(mediumValue)})",
            indent: 1,
            property: mediumProperty,
            after: coefficients.Release);

        if (coefficients.IntegrityFactor < 1.0)
        {
            Add(ReactionLogKind.Coefficient,
                $"Integrity factor {Coefficient(coefficients.IntegrityFactor)} — "
                + $"{context.IntegrityBefore} integrity remaining",
                indent: 1);
        }
    }

    private void Changes(ReactionStepContext context)
    {
        // A property can move more than once in a step — converge then annihilate, or blend
        // then get pruned. Reporting each hop separately produces lines like
        // "instability 0 → 1" followed by "instability 1 → 0", which is noise. Collapse to the
        // net movement and explain it with the last thing that happened to it, which is always
        // the most informative (annihilation beats the dilution that preceded it).
        var net = context.Result.Changes
            .GroupBy(c => c.Property, StringComparer.OrdinalIgnoreCase)
            .Select(g => new PropertyChange(g.Key, g.First().Before, g.Last().After, g.Last().Kind))
            .ToList();

        // Resistances are recomputed from what the material became rather than carried (§2.2).
        // That is bookkeeping, not something the player did, so it gets one note rather than a
        // line per resistance shouting that a 60 became a 0.
        var recomputed = net.Count(c => c.Kind == PropertyChangeKind.DerivedResistance);

        // Ordered by cause so the channel work — the part the player chose — reads first.
        var ordered = net
            .Where(c => c.Kind != PropertyChangeKind.DerivedResistance)
            .OrderBy(c => KindOrder(c.Kind))
            .ThenBy(c => c.Property, StringComparer.OrdinalIgnoreCase);

        var minor = 0;

        foreach (var change in ordered)
        {
            if (Math.Abs(change.Delta) < NoteworthyChange)
            {
                minor++;
                continue;
            }

            Add(ReactionLogKind.Property,
                $"{change.Property,-14} {Value(change.Before),3} → {Value(change.After),-3} ({Reason(change, context.Process)})",
                indent: 1,
                property: change.Property,
                before: change.Before,
                after: change.After);
        }

        if (minor > 0)
            Add(ReactionLogKind.Property, $"({minor} minor drift{(minor == 1 ? "" : "s")})", indent: 1);

        if (recomputed > 0)
            Add(ReactionLogKind.Property, "(resistances recomputed from the new state)", indent: 1);
    }

    private void Integrity(ReactionStepContext context)
    {
        var strain = context.Result.StrainReleased;
        var detail = $"Δstate {context.Result.StateDelta.ToString("0.00", CultureInfo.InvariantCulture)} "
            + $"× severity {context.Process.Severity.ToString("0.00", CultureInfo.InvariantCulture)}";

        if (strain > 0)
            detail += $", strain {Value(strain)}";

        Add(ReactionLogKind.Integrity,
            $"Integrity {context.IntegrityBefore} → {context.IntegrityAfter}  "
            + $"(cost {context.IntegrityCost.ToString("0.#", CultureInfo.InvariantCulture)}: {detail})",
            indent: 1,
            before: context.IntegrityBefore,
            after: context.IntegrityAfter);
    }

    /// <summary>"Potency 40, 70 → 53" — the inputs alongside the result, because potency being
    /// a mean is only learnable if the player can see what it averaged.</summary>
    public ReactionLogBuilder Potency(int substrate, IReadOnlyList<int> reagents, int result)
    {
        ArgumentNullException.ThrowIfNull(reagents);

        var inputs = string.Join(", ", new[] { substrate }.Concat(reagents));
        Add(ReactionLogKind.Potency, $"Potency {inputs} → {result}", indent: 1, after: result);

        return this;
    }

    /// <summary>The §10.4 trait report: births, mergers and displacements are always explicit —
    /// "which three?" is only a decision if the player can see what each craft did to the set.</summary>
    public ReactionLogBuilder Traits(TraitResolution resolution, Func<string, string> traitName)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(traitName);

        foreach (var born in resolution.Born)
            Add(ReactionLogKind.Trait, $"✦ Trait gained: {traitName(born.Id)} ({born.Magnitude:0})", after: born.Magnitude);

        foreach (var (a, b, into) in resolution.Superseded)
            Add(ReactionLogKind.Trait,
                $"⚡ Traits superseded: {traitName(a.Id)} + {traitName(b.Id)} → {traitName(into.Id)} ({into.Magnitude:0})",
                after: into.Magnitude);

        foreach (var displaced in resolution.Displaced)
            Add(ReactionLogKind.Trait,
                $"⚠ Trait lost: {traitName(displaced.Id)} ({displaced.Magnitude:0}) — displaced, trait cap {TraitResolver.MaterialCap}/{TraitResolver.MaterialCap}",
                after: displaced.Magnitude);

        return this;
    }

    /// <summary>The §8.4 essence report: what moved, what annihilated, and whether the vessel
    /// is straining under it (§5.3) — "attune first, then infuse" is only learnable technique
    /// if the log teaches it.</summary>
    public ReactionLogBuilder Essence(EssenceStepResult step)
    {
        ArgumentNullException.ThrowIfNull(step);

        foreach (var (key, before, after) in step.Changes)
        {
            var reason = after > before ? "essence transfer" : "opposition annihilated the overlap";
            Add(ReactionLogKind.Essence, $"essence.{key,-10} {before,3:0} → {after,3:0}  ({reason})",
                indent: 1, property: key, before: before, after: after);
        }

        if (step.StrainReleased > 0)
            Add(ReactionLogKind.Essence,
                $"Opposition released {step.StrainReleased:0.#} strain — only the asymmetry survives",
                indent: 1);

        return this;
    }

    /// <summary>§5.3's warning line: essence past capacity is strain, and strain makes every
    /// further craft wilder. The fix is a worthier vessel — Attune raises resonance.</summary>
    public ReactionLogBuilder EssenceStrain(double totalEssence, double capacity, double resonance)
    {
        Add(ReactionLogKind.Essence,
            $"⚠ Strained vessel: essence {totalEssence:0.#} exceeds capacity {capacity:0.#} " +
            $"(resonance {resonance:0.#}) — instability rises until attuned",
            indent: 1);
        return this;
    }

    /// <summary>Destruction is never total loss (§6.2c), so the log has to name the consolation
    /// prize in the same breath as the bad news.</summary>
    public ReactionLogBuilder Destroyed(string materialName, string? byproductName, int byproductQuantity)
    {
        Add(ReactionLogKind.Destruction, $"⚠ {materialName} was destroyed — integrity reached 0.");

        if (byproductName is not null)
            Add(ReactionLogKind.Destruction, $"Recovered: {byproductName} ×{byproductQuantity}", indent: 1);

        return this;
    }

    public ReactionLogBuilder Result(string materialName, int quantity, bool isFirstDiscovery)
    {
        Add(ReactionLogKind.Result,
            isFirstDiscovery
                ? $"✦ First discovery: {materialName} ×{quantity}"
                : $"Produced: {materialName} ×{quantity}");

        return this;
    }

    public ReactionLogBuilder Note(string text)
    {
        Add(ReactionLogKind.Result, text, indent: 1);
        return this;
    }

    // ---- Phrasing --------------------------------------------------------------------------

    private string Reason(PropertyChange change, ProcessDefinition process) => change.Kind switch
    {
        PropertyChangeKind.Channel =>
            $"channel, rate {process.ChannelRate(change.Property).ToString("0.00", CultureInfo.InvariantCulture)}",
        PropertyChangeKind.StructuralBlend => "structural blend",
        PropertyChangeKind.Dilution => "diluted, off channel",
        PropertyChangeKind.Pruned => $"pruned below floor {Floor(change.Property)}",
        PropertyChangeKind.Annihilation => $"annihilated against {Opposite(change.Property)}",
        PropertyChangeKind.DerivedResistance => "resistance is now derived",
        _ => change.Kind.ToString().ToLowerInvariant(),
    };

    private int Floor(string property) =>
        _properties.TryGetById(property, out var definition) ? definition.Floor : ReactionTuning.DefaultFloor;

    private string Opposite(string property) =>
        _properties.TryGetById(property, out var definition) ? definition.Opposes ?? "its opposite" : "its opposite";

    private static string MediumPropertyName(TransferMedium medium) => medium switch
    {
        TransferMedium.Solvent => "solubility",
        TransferMedium.Thermal => "instability",
        TransferMedium.Mechanical => "softness",
        TransferMedium.Arcane => "resonance",
        _ => "nothing",
    };

    // Phrased from the underlying property rather than the coefficient, because that is the
    // number in the parenthetical and the one the player can actually act on. §15.3's own
    // example calls affinity 30 "resists bonding", which a coefficient of 0.48 would not
    // suggest on its own.

    private static string AcceptanceVerb(double affinity) => affinity switch
    {
        < 35 => "resists",
        < 65 => "accepts",
        _ => "welcomes",
    };

    private static string ReleaseVerb(double mediumValue) => mediumValue switch
    {
        < 30 => "holds tight",
        < 65 => "gives up what it carries",
        _ => "gives freely",
    };

    private static int KindOrder(PropertyChangeKind kind) => kind switch
    {
        PropertyChangeKind.Channel => 0,
        PropertyChangeKind.Annihilation => 1,
        PropertyChangeKind.StructuralBlend => 2,
        PropertyChangeKind.Dilution => 3,
        PropertyChangeKind.Pruned => 4,
        PropertyChangeKind.DerivedResistance => 5,
        _ => 6,
    };

    private static string Coefficient(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Value(double value) =>
        Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

    private void Add(
        ReactionLogKind kind,
        string text,
        int indent = 0,
        string? property = null,
        double? before = null,
        double? after = null) =>
        _entries.Add(new ReactionLogEntry(kind, text, indent, property, before, after));
}
