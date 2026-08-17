using Dungeons.Content;
using Xunit;

namespace Dungeons.Tests.Content;

/// <summary>
/// The shipped starter crafting action set (docs/emergent-item-system.md §7.2). CraftingActions are the
/// <i>only</i> authored content the reaction engine needs — the algebra is universal — so
/// these tests guard the properties that make the set worth having: it covers the media, it
/// spreads severity, and it never names an item.
/// </summary>
public class CraftingActionContentTests
{
    private static DataStore<CraftingActionDefinition> CraftingActions() =>
        TestPaths.LoadStore<CraftingActionDefinition>("processes");

    /// <summary>§7.2 lists eight; <c>Attune</c> is the resonance-raising one and is P3.</summary>
    [Fact]
    public void TheSevenMundaneStarterProcessesShip()
    {
        var processes = CraftingActions();

        foreach (var id in new[]
        {
            "process.grind", "process.steep", "process.distill",
            "process.smelt", "process.quench", "process.alloy", "process.forge_infusion",
            "process.attune", // the resonance-raising action, live since C1b (P3)
        })
        {
            Assert.True(processes.Contains(id), $"missing starter craftingAction '{id}'.");
        }

        Assert.Equal(8, processes.Count);
    }

    /// <summary>
    /// §19's worked example computes real numbers from Forge Infusion and Steep. If these
    /// drift, the algebra tests that use the example as a fixture stop describing the game.
    /// </summary>
    [Fact]
    public void ForgeInfusion_MatchesTheWorkedExample()
    {
        var forge = CraftingActions().GetById("process.forge_infusion");

        Assert.Equal(TransferMedium.Thermal, forge.Medium);
        Assert.Equal(0.55, forge.Severity, 3);
        Assert.Equal(0.80, forge.TransferRateFor("heat"), 3);
        Assert.Equal(0.25, forge.TransferRateFor("hardness"), 3);
        Assert.Equal(0.15, forge.TransferRateFor("affinity"), 3);
        Assert.Equal(0.45, forge.EssenceRate, 3);
        Assert.Equal(0.65, forge.RoleWeights.Substrate, 3);
        Assert.Equal(0.30, forge.RoleWeights.Reagent, 3);
        Assert.Equal(0.05, forge.RoleWeights.Catalyst, 3);
        Assert.Contains("form:metal", forge.Requires.SubstrateTags);
    }

    [Fact]
    public void Steep_MatchesTheWorkedExample()
    {
        var steep = CraftingActions().GetById("process.steep");

        Assert.Equal(TransferMedium.Solvent, steep.Medium);
        Assert.Equal(0.20, steep.Severity, 3);
        Assert.Equal(0.55, steep.TransferRateFor("heat"), 3);
    }

    /// <summary>
    /// §7.3's point: the medium is what makes "which crafting action suits this ingredient" a readable
    /// property of the ingredient. That only works if the mundane set spans more than one.
    /// </summary>
    [Fact]
    public void TheStarterSetCoversTheMundaneMedia()
    {
        var media = CraftingActions().GetAll().Select(p => p.Medium).ToHashSet();

        Assert.Contains(TransferMedium.Thermal, media);
        Assert.Contains(TransferMedium.Solvent, media);
        Assert.Contains(TransferMedium.Mechanical, media);
        Assert.Contains(TransferMedium.Arcane, media); // Attune, live since C1b (P3)
    }

    /// <summary>
    /// Every reactive property must be workable by <i>something</i>. A property no crafting action
    /// opens can only ever dilute away (§8.3), which would quietly make every material
    /// carrying it — Stormglass, Storm Core, the thunderhorn parts — impossible to craft with
    /// on their defining trait. This is a content-completeness rule, and it caught exactly
    /// that gap for <c>charge</c>.
    /// </summary>
    [Fact]
    public void EveryReactivePropertyIsReachableBySomeProcess()
    {
        var reactive = TestPaths.LoadStore<PropertyDefinition>("properties").GetAll()
            .Where(p => p.Role == PropertyRole.Reactive)
            .Select(p => p.Id);

        var opened = CraftingActions().GetAll()
            .SelectMany(action => action.AffectedQualities)
            .Select(c => c.Property)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var property in reactive)
            Assert.True(opened.Contains(property), $"no craftingAction can work '{property}'.");
    }

    /// <summary>
    /// §6.2a rewards elegant paths by charging workability in proportion to severity. That is
    /// only a decision if the player has gentle and violent options to choose between.
    /// </summary>
    [Fact]
    public void SeveritySpansGentleToViolent()
    {
        var severities = CraftingActions().GetAll().Select(p => p.Severity).ToList();

        Assert.True(severities.Min() <= 0.25, "no genuinely gentle craftingAction to choose.");
        Assert.True(severities.Max() >= 0.55, "no genuinely violent craftingAction to choose.");
        Assert.True(severities.Distinct().Count() >= 5, "severities are too clustered to matter.");
    }

    /// <summary>
    /// The load-bearing rule of the whole design (§0 Decision 1, §9.1): the moment a crafting action
    /// names an item, the recipe table is back.
    /// </summary>
    [Fact]
    public void NoProcessReferencesAnItemId()
    {
        foreach (var craftingAction in CraftingActions().GetAll())
        {
            foreach (var tag in craftingAction.Requires.SubstrateTags
                         .Concat(craftingAction.TagEffects.Set)
                         .Concat(craftingAction.TagEffects.Clear))
            {
                Assert.False(tag.StartsWith("material.", StringComparison.Ordinal),
                    $"{craftingAction.Id} references item id '{tag}'.");
            }
        }
    }

    /// <summary>
    /// A form change has to replace the old form, not add to it — otherwise ground iron is
    /// simultaneously a powder and an ingot (§4.2, §7.2).
    /// </summary>
    [Fact]
    public void ProcessesThatChangeFormClearTheOldForm()
    {
        foreach (var craftingAction in CraftingActions().GetAll())
        {
            var setsForm = craftingAction.TagEffects.Set.Any(t => t.StartsWith("form:", StringComparison.Ordinal));
            if (!setsForm)
                continue;

            Assert.Contains("form:*", craftingAction.TagEffects.Clear);
        }
    }

    [Fact]
    public void OnlyGrindIsUngated()
    {
        foreach (var craftingAction in CraftingActions().GetAll())
        {
            if (craftingAction.Id == "process.grind")
                Assert.True(craftingAction.IsUngated, "Grind is the universal prep step and stays ungated.");
            else
                Assert.False(craftingAction.IsUngated, $"{craftingAction.Id} should be gated by a profession.");
        }
    }

    [Fact]
    public void ChannelLookupIsCaseInsensitiveAndReportsOffChannel()
    {
        var forge = CraftingActions().GetById("process.forge_infusion");

        Assert.True(forge.Affects("HEAT"));
        Assert.False(forge.Affects("toxicity"));
        Assert.Equal(0.0, forge.TransferRateFor("toxicity"));
    }
}
