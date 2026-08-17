using System.Text.RegularExpressions;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Presentation;
using Xunit;

namespace Dungeons.Tests.Presentation;

/// <summary>
/// The player-facing voice (docs/presentation-architecture.md §2–3). Two rule sets are pinned
/// here: the §6.2c fairness guarantees (destruction stated outright, risk as a percentage),
/// which moved with the voice from the old <c>CraftFormat</c> tests; and D30's core promise —
/// raw simulation values never appear in the normal-play text.
/// </summary>
public class SemanticFormatTests
{
    private static PropertyGlossary Glossary { get; } =
        new(TestPaths.LoadStore<PropertyDefinition>("properties"));

    private static CraftReading Reading(
        RiskBand risk = RiskBand.Safe,
        double cost = 3,
        double spread = 0,
        int projected = 88,
        double chance = 0,
        bool firstDiscovery = false,
        IReadOnlyList<PropertyMovement>? strengthening = null,
        IReadOnlyList<PropertyMovement>? weakening = null,
        IReadOnlyList<OppositionReading>? opposition = null,
        IReadOnlyList<TraitBirthReading>? births = null) =>
        new(
            CanCraft: true,
            Failure: CraftFailure.None,
            SubstrateName: "Iron Ingot",
            ProjectedName: "Emberlit Iron",
            FirstDiscovery: firstDiscovery,
            Expression: PropertyTier.Moderate,
            ExpressionShift: 1,
            Strengthening: strengthening ?? Array.Empty<PropertyMovement>(),
            Weakening: weakening ?? Array.Empty<PropertyMovement>(),
            WashingOut: Array.Empty<PropertyMovement>(),
            Opposition: opposition ?? Array.Empty<OppositionReading>(),
            TraitBirths: births ?? Array.Empty<TraitBirthReading>(),
            NearbyTraits: Array.Empty<NearbyTrait>(),
            Essence: Array.Empty<EssenceReading>(),
            VesselStrained: false,
            Risk: risk,
            Integrity: new IntegrityProjection(cost, spread, projected, chance));

    // ---- The §6.2c guarantees, in the new voice ----------------------------------------------

    [Fact]
    public void CertainDestructionIsStatedOutrightNeverAsAPercentage()
    {
        var text = SemanticFormat.Projection(
            Reading(risk: RiskBand.Destroys, cost: 40, projected: 0, chance: 1.0), Glossary);

        Assert.Contains("DESTROY", text);
        Assert.Contains("Iron Ingot", text);
        Assert.Contains("byproducts", text);
        Assert.DoesNotContain("%", text);
    }

    [Fact]
    public void RealRiskShowsThePercentage()
    {
        var text = SemanticFormat.Projection(
            Reading(risk: RiskBand.Perilous, cost: 18, spread: 10, projected: 2, chance: 0.35), Glossary);

        Assert.Contains("PERILOUS", text);
        Assert.Contains("35%", text);
        Assert.DoesNotContain("DESTROY the", text);
    }

    [Fact]
    public void AnImpossibleCraftExplainsItselfInThePlayersLanguage()
    {
        var reading = Reading() with { CanCraft = false, Failure = CraftFailure.SubstrateRejected };

        Assert.Equal("This process cannot work that material.", SemanticFormat.Projection(reading, Glossary));
    }

    [Fact]
    public void AnUnmadeMaterialIsFlaggedBeforeCommitting() =>
        Assert.Contains("never made before", SemanticFormat.Projection(Reading(firstDiscovery: true), Glossary));

    // ---- D30's core promise -------------------------------------------------------------------

    /// <summary>The normal-play projection carries no raw values at all: tiers, arrows, wear
    /// words and names only. (PERILOUS's percentage is the §6.2c exception, tested above.)</summary>
    [Fact]
    public void ASafeProjectionContainsTierWordsAndNoNumbers()
    {
        var text = SemanticFormat.Projection(
            Reading(strengthening: new[] { new PropertyMovement("heat", 10, 50, Trend.Rising) }),
            Glossary);

        Assert.Contains("Aiming at: Emberlit Iron", text);
        Assert.Contains("Strengthening:", text);
        Assert.Contains("▲ Heat", text);
        Assert.Contains("↑↑", text);              // crosses Trace → Moderate
        Assert.Contains("Trace → Moderate", text);
        Assert.Contains("Risk: SAFE", text);
        Assert.Contains("Sturdy", text);           // 88 reads as wear, not as 88
        Assert.DoesNotContain("⚠", text);
        Assert.DoesNotMatch(new Regex(@"\d"), text);
    }

    [Fact]
    public void OppositionAndBirthsNarrateAsMeaningNotArithmetic()
    {
        var text = SemanticFormat.Projection(
            Reading(
                opposition: new[]
                {
                    new OppositionReading(new PropertyMovement("heat", 30, 22, Trend.Opposed), "cold"),
                },
                births: new[] { new TraitBirthReading("trait.emberveined", "Emberveined", "brittle when cold") }),
            Glossary);

        Assert.Contains("Opposition: ▲ Heat ⇄ Cold — strain released", text);
        Assert.Contains("Trait born: Emberveined (drawback: brittle when cold)", text);
    }

    // ---- Typed lines (the R2 panel contract) ---------------------------------------------------

    /// <summary>The client styles by kind and never parses text — the ReactionLogKind pattern.
    /// The joined lines are byte-identical to the plain-text projection: one wording, two shapes.</summary>
    [Fact]
    public void ProjectionLinesCarryKindsAndJoinToTheSameText()
    {
        var reading = Reading(
            risk: RiskBand.Destroys, cost: 40, projected: 0, chance: 1.0,
            strengthening: new[] { new PropertyMovement("heat", 10, 50, Trend.Rising) },
            births: new[] { new TraitBirthReading("trait.emberveined", "Emberveined", "") });

        var lines = SemanticFormat.ProjectionLines(reading, Glossary);

        Assert.Equal(ProjectionLineKind.Aim, lines[0].Kind);
        Assert.Equal(ProjectionLineKind.Expression, lines[1].Kind);
        Assert.Contains(lines, l => l.Kind == ProjectionLineKind.Strengthening);
        Assert.Contains(lines, l => l.Kind == ProjectionLineKind.TraitBirth);

        // DESTROYS is two Risk lines: the band, then the §6.2c statement.
        Assert.Equal(2, lines.Count(l => l.Kind == ProjectionLineKind.Risk));

        Assert.Equal(
            SemanticFormat.Projection(reading, Glossary),
            string.Join("\n", lines.Select(l => l.Text)));
    }

    [Fact]
    public void AFailedReadingIsASingleFailureLine()
    {
        var reading = Reading() with { CanCraft = false, Failure = CraftFailure.MissingInputs };
        var line = Assert.Single(SemanticFormat.ProjectionLines(reading, Glossary));

        Assert.Equal(ProjectionLineKind.Failure, line.Kind);
        Assert.Equal("You do not have the materials.", line.Text);
    }

    [Fact]
    public void TheMaterialStripCompressesToGlyphsAndPipsOnly()
    {
        var properties = TestPaths.LoadStore<PropertyDefinition>("properties");
        var materials = TestPaths.LoadStore<MaterialDefinition>("materials");
        var iron = materials.GetById("material.iron_ingot");

        var reading = MaterialReadings.From(
            iron, new MaterialProfileResolver(properties).Resolve(iron), properties,
            TestPaths.LoadStore<TraitDefinition>("traits"),
            TestPaths.LoadStore<EssenceDefinition>("essences"));

        var strip = SemanticFormat.MaterialStrip(reading, Glossary);

        Assert.Contains("●", strip);
        Assert.True(strip.Split("  ").Length <= 3, strip);
        Assert.DoesNotMatch(new Regex(@"\d"), strip);
    }

    // ---- Material readings ----------------------------------------------------------------------

    [Fact]
    public void AMaterialReadsAsMeaningWithoutNumbers()
    {
        var properties = TestPaths.LoadStore<PropertyDefinition>("properties");
        var materials = TestPaths.LoadStore<MaterialDefinition>("materials");
        var resolver = new MaterialProfileResolver(properties);
        var iron = materials.GetById("material.iron_ingot");

        var reading = MaterialReadings.From(
            iron, resolver.Resolve(iron), properties,
            TestPaths.LoadStore<TraitDefinition>("traits"),
            TestPaths.LoadStore<EssenceDefinition>("essences"));
        var text = SemanticFormat.Material(reading, Glossary);

        Assert.Contains("Iron Ingot", text);
        Assert.Contains("◆ Hardness", text);
        Assert.Contains("●", text);                 // pips
        Assert.Contains("Bonds", text);             // the affinity phrase
        Assert.DoesNotMatch(new Regex(@"\d"), text);
    }

    // ---- Process labels --------------------------------------------------------------------------

    [Fact]
    public void ProcessLabelsSpeakSeverityAndSubstrateInWords()
    {
        var processes = TestPaths.LoadStore<ProcessDefinition>("processes");

        var forge = SemanticFormat.Process(processes.GetById("process.forge_infusion"), "Smithing");
        Assert.Contains("Forge Infusion", forge);
        Assert.Contains("forceful", forge);         // severity 0.55, as a word
        Assert.Contains("works metal", forge);
        Assert.Contains("Smithing L15", forge);     // the gate is a requirement, not a value
        Assert.DoesNotContain("severity", forge);
        Assert.DoesNotContain("0.55", forge);

        Assert.Contains("any skill", SemanticFormat.Process(processes.GetById("process.grind"), string.Empty));
    }

    [Fact]
    public void TheChannelSpeaksInGlyphsAndRateWordsNotRates()
    {
        var channel = SemanticFormat.Channel(
            TestPaths.LoadStore<ProcessDefinition>("processes").GetById("process.forge_infusion"),
            Glossary);

        Assert.Contains("▲ Heat hard", channel);
        Assert.DoesNotContain("0.8", channel);
        Assert.DoesNotMatch(new Regex(@"\d"), channel);
    }
}
