using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Crafting;

/// <summary>
/// The universal reaction algebra (docs/emergent-item-system.md §8) — a <b>total function</b>:
/// every combination of substrate, reagent and crafting action produces a result, always. There is no
/// lookup, no recipe, and no per-combination rule anywhere in this file. Order-dependence
/// comes for free, because applying reagents in sequence means each step acts on a different
/// intermediate state.
///
/// <para>Pure and deterministic. The two seeded rolls the design permits (execution quality
/// and variance perturbation, §12.5) are supplied by the caller as
/// <c>qualityMultiplier</c> and applied outside this file.</para>
///
/// <para>P1 scope: convergence, off-channel handling and opposition. Essence transfer (§8.4),
/// signature reactions (§9) and traits (§10) are P3/P4/P2 and are deliberately absent.</para>
/// </summary>
public static class MaterialTransformationRules
{
    /// <summary>
    /// Applies one reagent to a substrate under a crafting action — steps 2–6 of the §8.7 pipeline:
    /// coefficients, channel convergence, off-channel drift, floor pruning, then opposition.
    /// </summary>
    /// <param name="substrate">The state being transformed.</param>
    /// <param name="reagent">The reagent being applied; consumed entirely by the caller.</param>
    /// <param name="crafting action">Decides which properties react, and how violently.</param>
    /// <param name="properties">The property registry — roles, opposition, floors.</param>
    /// <param name="substrateWorkability">Remaining transformation budget (§6.2).</param>
    /// <param name="qualityMultiplier">Execution quality, 0.85–1.12 (§7.4).</param>
    /// <param name="catalyst">Catalyst factor; 1.0 when no catalyst is slotted.</param>
    public static TransformationStepResult ApplyReagent(
        PropertySet substrate,
        PropertySet reagent,
        CraftingActionDefinition craftingAction,
        DataStore<PropertyDefinition> properties,
        int substrateWorkability,
        double qualityMultiplier = 1.0,
        double catalyst = MaterialTransformationTuning.NoCatalyst)
    {
        ArgumentNullException.ThrowIfNull(substrate);
        ArgumentNullException.ThrowIfNull(reagent);
        ArgumentNullException.ThrowIfNull(craftingAction);
        ArgumentNullException.ThrowIfNull(properties);

        var coefficients = TransferCoefficients.For(
            substrate, reagent, craftingAction, substrateWorkability, qualityMultiplier, catalyst);

        var changes = new List<PropertyChange>();
        var state = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        // Every property either side carries is a candidate. Convergence reads the substrate's
        // pre-step values throughout, so the properties move simultaneously rather than each
        // one seeing its predecessors' results.
        var mixture = MassWeights(substrate, reagent);

        foreach (var key in Union(substrate, reagent))
        {
            var role = properties.TryGetById(key, out var definition) ? definition.Role : PropertyRole.Structural;
            var before = substrate.Get(key);

            switch (role)
            {
                // §2.2/§2.3: resistances are derived from what the material now is, never
                // carried. Dropping them here keeps ResistanceCalculator the single read path
                // — storing a derived value would make it an authored override next generation.
                case PropertyRole.Response:
                    if (before != 0.0)
                        changes.Add(new(key, before, 0.0, PropertyChangeKind.DerivedResistance));
                    continue;

                // §2.3: inert in crafting — read only by the harvest systems.
                case PropertyRole.Sourcing:
                    Set(state, key, before);
                    continue;
            }

            var rate = craftingAction.TransferRateFor(key);
            if (rate > 0.0)
            {
                var after = Converge(before, reagent.Get(key), rate, coefficients.Product);
                Record(state, changes, key, before, after, PropertyChangeKind.OnChannelTransfer);
                continue;
            }

            // Off-channel. The property definition decides which way it drifts, so this stays
            // data-driven: `dilutes` is exactly §8.3's split between the two behaviours.
            if (definition?.Dilutes ?? false)
            {
                var after = before * (1.0 - MaterialTransformationTuning.ReactiveDilutionRate);
                Record(state, changes, key, before, after, PropertyChangeKind.Dilution);
            }
            else
            {
                var target = mixture.Substrate * before + mixture.Reagent * reagent.Get(key);
                var after = before + (target - before) * MaterialTransformationTuning.StructuralBlendRate;
                Record(state, changes, key, before, after, PropertyChangeKind.StructuralBlend);
            }
        }

        // §8.7 lists pruning at step 5 and opposition at step 6. We prune last instead:
        // annihilation is precisely the operation that leaves sub-floor residue behind, and
        // §8.3's stated purpose — stopping deep-generation materials from carrying a muddy
        // vector of trace values — is only served if the floor is applied after it.
        var strain = ResolveOpposition(state, properties, changes);
        Prune(state, properties, changes);

        return new TransformationStepResult(new PropertySet(state), coefficients, strain, changes);
    }

    /// <summary>
    /// §8.2 — move a fraction of the remaining gap toward the reagent's value. The result can
    /// never exceed the stronger of the two inputs, which is the single rule that kills
    /// unbounded stat escalation everywhere in the system, permanently.
    /// </summary>
    public static double Converge(double before, double target, double rate, double coefficientProduct)
    {
        var k = Math.Clamp(rate * coefficientProduct, 0.0, MaterialTransformationTuning.MaxConvergence);
        return before + (target - before) * k;
    }

    /// <summary>
    /// §8.5 — opposed pairs mutually annihilate, leaving only the asymmetry, so opposites can
    /// never be stockpiled. The energy released is returned as strain and charged against
    /// workability by the caller (§6.2a).
    /// </summary>
    private static double ResolveOpposition(
        Dictionary<string, double> state,
        DataStore<PropertyDefinition> properties,
        List<PropertyChange> changes)
    {
        var strain = 0.0;
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in state.Keys.ToList())
        {
            if (!properties.TryGetById(key, out var definition) || definition.Opposes is not { } opposite)
                continue;

            // Each unordered pair once — heat opposes cold and cold opposes heat.
            if (!resolved.Add(key) || !resolved.Add(opposite))
                continue;

            var a = state.GetValueOrDefault(key);
            var b = state.GetValueOrDefault(opposite);
            var annihilated = Math.Min(a, b) * MaterialTransformationTuning.ConflictAnnihilationRate;
            if (annihilated <= 0.0)
                continue;

            changes.Add(new(key, a, a - annihilated, PropertyChangeKind.Annihilation));
            changes.Add(new(opposite, b, b - annihilated, PropertyChangeKind.Annihilation));
            Set(state, key, a - annihilated);
            Set(state, opposite, b - annihilated);

            strain += annihilated;
        }

        return strain;
    }

    /// <summary>§8.3 — anything left below its floor is pruned to zero, so trace amounts don't
    /// accumulate into a muddy vector over many generations.</summary>
    private static void Prune(
        Dictionary<string, double> state,
        DataStore<PropertyDefinition> properties,
        List<PropertyChange> changes)
    {
        foreach (var key in state.Keys.ToList())
        {
            var value = state[key];
            if (value <= 0.0)
                continue;

            if (properties.TryGetById(key, out var definition) && definition.Role == PropertyRole.Sourcing)
                continue;

            var floor = definition?.Floor ?? MaterialTransformationTuning.DefaultFloor;
            if (value >= floor)
                continue;

            changes.Add(new(key, value, 0.0, PropertyChangeKind.Pruned));
            state.Remove(key);
        }
    }

    /// <summary>
    /// The mass shares that define the mixture an off-channel structural property blends
    /// toward (§8.3). Massless inputs fall back to an even split rather than dividing by zero.
    /// </summary>
    private static (double Substrate, double Reagent) MassWeights(PropertySet substrate, PropertySet reagent)
    {
        var substrateMass = substrate.Get(ItemProperties.Mass);
        var reagentMass = reagent.Get(ItemProperties.Mass);
        var total = substrateMass + reagentMass;

        return total <= 0.0
            ? (0.5, 0.5)
            : (substrateMass / total, reagentMass / total);
    }

    private static IEnumerable<string> Union(PropertySet a, PropertySet b) =>
        a.Keys.Union(b.Keys, StringComparer.OrdinalIgnoreCase);

    private static void Record(
        Dictionary<string, double> state,
        List<PropertyChange> changes,
        string key,
        double before,
        double after,
        PropertyChangeKind kind)
    {
        Set(state, key, after);
        if (before != after)
            changes.Add(new(key, before, after, kind));
    }

    private static void Set(Dictionary<string, double> state, string key, double value) =>
        state[key] = Math.Clamp(value, MaterialTransformationTuning.MinPropertyValue, MaterialTransformationTuning.MaxPropertyValue);
}
