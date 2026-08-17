using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Xunit;

namespace Dungeons.Tests.Crafting;

/// <summary>
/// The Reaction Log (docs/emergent-item-system.md §15.3) — "a hard requirement, not a
/// nice-to-have", because a system this deep is only playable if it explains itself.
///
/// <para>What is worth testing here is not the exact wording but the <b>explanatory
/// content</b>: that the log names the cause of every movement, quotes the property that
/// drove each coefficient, and shows the arithmetic behind the workability charge. A log that
/// prints numbers without their reasons would pass a naive test and fail the player.</para>
/// </summary>
public class ReactionLogTests
{
    private static DataStore<PropertyDefinition> Properties() =>
        TestPaths.LoadStore<PropertyDefinition>("properties");

    private static DataStore<MaterialDefinition> Materials() =>
        TestPaths.LoadStore<MaterialDefinition>("materials");

    private static DataStore<CraftingActionDefinition> CraftingActions() =>
        TestPaths.LoadStore<CraftingActionDefinition>("processes");

    /// <summary>Runs §19 attempt 2 and logs it — the craft §15.3's sample trace depicts.</summary>
    private static (ReactionLog Log, string Text) WorkedExample(double craftQuality = 1.0)
    {
        var properties = Properties();
        var materials = Materials();
        var craftingAction = CraftingActions().GetById("process.forge_infusion");
        var substrate = materials.GetById("material.iron_ingot");
        var reagent = materials.GetById("material.ember_core");

        var step = MaterialTransformationRules.ApplyReagent(
            substrate.BaseProperties, reagent.BaseProperties, craftingAction, properties,
            substrateWorkability: 90, qualityMultiplier: 1.05);

        var cost = WorkabilityCalculator.Cost(step.StateDelta, craftingAction.Severity, step.StressReleased, craftQuality);
        var after = WorkabilityCalculator.Apply(90, cost);

        var log = new ReactionLogBuilder(properties)
            .Step(new ReactionStepContext(
                craftingAction, substrate.Name, reagent.Name,
                substrate.BaseProperties, reagent.BaseProperties, step, 90, after, cost))
            .MaterialStrength(40, new[] { 70 }, 49)
            .Result("Emberlit Iron", 1, isFirstDiscovery: true)
            .Build();

        return (log, log.ToText());
    }

    [Fact]
    public void TheLogOpensByNamingTheCraft()
    {
        var (log, text) = WorkedExample();

        Assert.Equal(ReactionLogKind.Step, log.Entries[0].Kind);
        Assert.Contains("Forge Infusion — Iron Ingot ← Ember Core", text);
    }

    /// <summary>
    /// §15.3's example is "Acceptance 0.48 (iron resists bonding: affinity 30)". The
    /// parenthetical is the whole value of the line: it teaches the player to check affinity
    /// before choosing a crafting action, which a bare coefficient never would.
    /// </summary>
    [Fact]
    public void CoefficientsAreExplainedByThePropertyThatDroveThem()
    {
        var (_, text) = WorkedExample();

        Assert.Contains("Acceptance 0.48", text);
        Assert.Contains("Iron Ingot resists bonding (affinity 30)", text);

        Assert.Contains("Release 0.93", text);
        Assert.Contains("Ember Core gives freely under thermal (instability 90)", text);
    }

    /// <summary>The medium's governing property is named, so "why did a solvent do nothing to
    /// my metal" is answerable from the log alone (§7.3).</summary>
    [Fact]
    public void TheLogNamesTheMediumsGoverningProperty()
    {
        var properties = Properties();
        var materials = Materials();
        var craftingAction = CraftingActions().GetById("process.steep");
        var substrate = materials.GetById("material.iron_ingot");
        var reagent = materials.GetById("material.ember_sap");

        var step = MaterialTransformationRules.ApplyReagent(
            substrate.BaseProperties, reagent.BaseProperties, craftingAction, properties, 90);

        var text = new ReactionLogBuilder(properties)
            .Step(new ReactionStepContext(
                craftingAction, substrate.Name, reagent.Name,
                substrate.BaseProperties, reagent.BaseProperties, step, 90, 87, 3))
            .Build()
            .ToText();

        Assert.Contains("under solvent (solubility 55)", text);
    }

    /// <summary>Every property movement states its cause, which is what makes the off-channel
    /// rules (§8.3) learnable instead of mysterious.</summary>
    [Fact]
    public void EveryMovementStatesWhyItHappened()
    {
        var (_, text) = WorkedExample();

        Assert.Contains("heat", text);
        Assert.Contains("channel, rate 0.80", text);
        Assert.Contains("structural blend", text);
    }

    [Fact]
    public void DilutionPruningAndAnnihilationAreNamedPlainly()
    {
        var properties = Properties();
        var craftingAction = CraftingActions().GetById("process.forge_infusion");

        var step = MaterialTransformationRules.ApplyReagent(
            PropertySet.FromValues(new Dictionary<string, double>
            {
                ["cold"] = 60, ["toxicity"] = 40, ["corrosion"] = 3, ["affinity"] = 100, ["mass"] = 50,
            }),
            PropertySet.FromValues(new Dictionary<string, double> { ["heat"] = 100, ["instability"] = 100 }),
            craftingAction, properties, 100, qualityMultiplier: 1.12);

        var text = new ReactionLogBuilder(properties)
            .Step(new ReactionStepContext(
                craftingAction, "Test Substrate", "Test Reagent",
                PropertySet.Empty, PropertySet.Empty, step, 100, 80, 20))
            .Build()
            .ToText();

        Assert.Contains("diluted, off channel", text);
        Assert.Contains("annihilated against", text);
        Assert.Contains("pruned below floor 5", text);
    }

    /// <summary>§15.3 shows the workability arithmetic, not just the result — this is the line
    /// that teaches "gentle steps cost less", which is the main skill axis (§6.2a).</summary>
    [Fact]
    public void TheIntegrityLineShowsItsArithmetic()
    {
        var (_, text) = WorkedExample();

        Assert.Contains("Integrity 90 → ", text);
        Assert.Contains("Δstate", text);
        Assert.Contains("severity 0.55", text);
    }

    /// <summary>"Potency 40, 70 → 53": material strength being a mean is only learnable if the player can
    /// see what it averaged (§6.1).</summary>
    [Fact]
    public void ThePotencyLineShowsItsInputs()
    {
        var (_, text) = WorkedExample();
        Assert.Contains("Potency 40, 70 → 49", text);
    }

    [Fact]
    public void FirstDiscoveryIsCalledOut()
    {
        var (_, text) = WorkedExample();
        Assert.Contains("✦ First discovery: Emberlit Iron ×1", text);

        var repeat = new ReactionLogBuilder(Properties())
            .Result("Emberlit Iron", 3, isFirstDiscovery: false)
            .Build()
            .ToText();

        Assert.Equal("Produced: Emberlit Iron ×3", repeat);
    }

    /// <summary>Destruction must name the consolation prize in the same breath as the bad news
    /// (§6.2c) — otherwise a blown craft reads as a pure zero.</summary>
    [Fact]
    public void DestructionReportsWhatWasRecovered()
    {
        var text = new ReactionLogBuilder(Properties())
            .Destroyed("Emberlit Iron", "Slag", 1)
            .Build()
            .ToText();

        Assert.Contains("⚠ Emberlit Iron was destroyed — integrity reached 0.", text);
        Assert.Contains("Recovered: Slag ×1", text);
    }

    /// <summary>Off-channel drift touches many properties by fractions. Printing all of them
    /// buries the two lines that mattered, so they are summarised instead.</summary>
    [Fact]
    public void TinyDriftsAreSummarisedRatherThanListed()
    {
        var (log, text) = WorkedExample();

        Assert.Contains("minor drift", text);
        Assert.All(
            log.Entries.Where(e => e.Kind == ReactionLogKind.Property && e.Before is not null),
            e => Assert.True(Math.Abs(e.After!.Value - e.Before!.Value) >= 0.5));
    }

    /// <summary>Entries carry their numbers as data, so the Crafting UI and the future codex
    /// can render rows rather than re-parsing the text.</summary>
    [Fact]
    public void EntriesAreStructuredNotJustText()
    {
        var (log, _) = WorkedExample();

        var heat = log.Entries.Single(e => e.Property == "heat");
        Assert.Equal(ReactionLogKind.Property, heat.Kind);
        Assert.Equal(0.0, heat.Before);
        Assert.Equal(35.0, heat.After!.Value, 0);

        Assert.Contains(log.Entries, e => e.Kind == ReactionLogKind.Workability);
        Assert.Contains(log.Entries, e => e.Kind == ReactionLogKind.MaterialStrength);
        Assert.Contains(log.Entries, e => e.Kind == ReactionLogKind.Result);
    }

    /// <summary>The log is a pure rendering of the step, so the same craft always explains
    /// itself the same way (§12.5).</summary>
    [Fact]
    public void TheLogIsDeterministic()
    {
        Assert.Equal(WorkedExample().Text, WorkedExample().Text);
    }

    [Fact]
    public void AnEmptyLogRendersToNothing()
    {
        Assert.Equal(string.Empty, ReactionLog.Empty.ToText());
    }
}
