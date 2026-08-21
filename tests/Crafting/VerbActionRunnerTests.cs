using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Crafting.Identity;
using Dungeons.Items;
using Dungeons.Professions;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Crafting;

/// <summary>
/// The identity bench end to end, over the REAL shipped starter content (migration Phase 2c):
/// gates, consumption, the authored-equivalence rule, emergent registration + naming, and the
/// full worked chain from prospecting a stone to a named, stacked, twice-infused ingot.
/// </summary>
public class VerbActionRunnerTests
{
    private const string Vital = "identity.vital";
    private const string Dense = "identity.dense";

    [Fact]
    public void SmeltingPlainOreDepositsTheAuthoredIngot()
    {
        // The authored-equivalence rule: a result indistinguishable from the authored
        // definition IS the authored definition — mundane chains keep their ids.
        var (runner, bag, _) = Harness();
        bag.Add("material.iron_ore", 1);

        var result = runner.Run(new VerbActionInvocation("craft.smelt_iron", "material.iron_ore", Array.Empty<string>()));

        var deposited = Assert.Single(result.Deposited);
        Assert.Equal("material.iron_ingot", deposited.ItemId);
        Assert.False(deposited.FirstDiscovery);
        Assert.Equal(0, bag.GetQuantity("material.iron_ore"));
        Assert.Equal(1, bag.GetQuantity("material.iron_ingot"));
    }

    [Fact]
    public void RevealedMaterialsRegisterOnceAndStack()
    {
        var (runner, bag, _) = Harness();
        bag.Add("material.oak", 2);

        var first = runner.Run(new VerbActionInvocation("craft.identify_virtues", "material.oak", Array.Empty<string>(), Vital));
        var second = runner.Run(new VerbActionInvocation("craft.identify_virtues", "material.oak", Array.Empty<string>(), Vital));

        Assert.True(first.AnyFirstDiscovery);
        Assert.False(second.AnyFirstDiscovery);
        var vitalOakId = first.Deposited[0].ItemId;
        Assert.Equal(vitalOakId, second.Deposited[0].ItemId); // same fingerprint, same stack
        Assert.Equal(2, bag.GetQuantity(vitalOakId));
        Assert.Equal("Vital Oak", first.Deposited[0].Name);
    }

    [Fact]
    public void TheStarterChainForgesTheTwiceInfusedIngot()
    {
        var (runner, bag, content) = Harness();
        bag.Add("material.oak", 3);
        bag.Add("material.granite", 1);
        bag.Add("material.iron_ore", 1);

        // Herblore: identify three oaks, draw one onto a carrier, keep two as distill feed.
        var vitalOak = RunOk(runner, new("craft.identify_virtues", "material.oak", Array.Empty<string>(), Vital)).Deposited[0].ItemId;
        RunOk(runner, new("craft.identify_virtues", "material.oak", Array.Empty<string>(), Vital));
        RunOk(runner, new("craft.identify_virtues", "material.oak", Array.Empty<string>(), Vital));
        var carrier = RunOk(runner, new("craft.draw_extract", vitalOak, Array.Empty<string>(), Vital)).Deposited[0];
        Assert.Equal("Oak Vital Extract", carrier.Name);

        // Alchemy: feed the carrier both remaining Vital Oaks — rank 2 (cost 2, r1 feeds ×2).
        var tincture = RunOk(runner, new("craft.distill", carrier.ItemId, new[] { vitalOak, vitalOak }, Vital)).Deposited[0];

        // Mining + Smithing: prospect the granite, smelt the ore, infuse twice.
        var denseGranite = RunOk(runner, new("craft.prospect_stone", "material.granite", Array.Empty<string>(), Dense)).Deposited[0].ItemId;
        RunOk(runner, new("craft.smelt_iron", "material.iron_ore", Array.Empty<string>()));
        var denseIngot = RunOk(runner, new("craft.infuse_metal", "material.iron_ingot", new[] { denseGranite })).Deposited[0];
        var finished = RunOk(runner, new("craft.infuse_metal", denseIngot.ItemId, new[] { tincture.ItemId })).Deposited[0];

        // Vital r2 (carrier fidelity) outranks Dense r1, oak earned its "-bound", and the
        // name generator held the four-word budget.
        Assert.Equal("Vital Oakbound Iron Ingot", finished.Name);
        Assert.True(finished.FirstDiscovery);

        var state = content.Materials.GetById(finished.ItemId).IdentityState!;
        Assert.Equal(2, state.StakeOf(Vital)!.Rank);
        Assert.Equal(1, state.StakeOf(Dense)!.Rank);
        Assert.Equal(Condition.Strained, state.Condition);
        Assert.Equal(Stability.Stable, state.Stability);
    }

    [Fact]
    public void TheLevelGateBlocksBeforeAnythingIsSpent()
    {
        var (runner, bag, _) = Harness(new() { ["profession.smithing"] = 1 });
        bag.Add("material.iron_ingot", 1);
        bag.Add("material.granite", 1);

        var result = runner.Run(new VerbActionInvocation("craft.infuse_metal", "material.iron_ingot", new[] { "material.granite" }));

        Assert.Equal(VerbActionGateFailure.ProfessionLevelTooLow, result.GateFailure);
        Assert.Equal(1, bag.GetQuantity("material.iron_ingot"));
        Assert.Equal(1, bag.GetQuantity("material.granite"));
    }

    [Fact]
    public void TheDomainGateRefusesSmeltingGranite()
    {
        var (runner, bag, _) = Harness();
        bag.Add("material.granite", 1);

        var result = runner.Run(new VerbActionInvocation("craft.smelt_iron", "material.granite", Array.Empty<string>()));

        Assert.Equal(VerbActionGateFailure.SubstrateOutsideDomain, result.GateFailure);
    }

    [Fact]
    public void AnUnmigratedSubstrateIsRefusedByName()
    {
        var (runner, bag, _) = Harness();
        bag.Add("material.limestone", 1);

        var result = runner.Run(new VerbActionInvocation("craft.prospect_stone", "material.limestone", Array.Empty<string>(), Dense));

        Assert.Equal(VerbActionGateFailure.SubstrateNotMigrated, result.GateFailure);
    }

    [Fact]
    public void MendPaysSlagAndEngineRefusalsSpendNothing()
    {
        var (runner, bag, content) = Harness();
        bag.Add("material.oak", 1);
        bag.Add("material.sageleaf", 1);
        bag.Add("material.granite", 1);
        bag.Add("material.iron_ore", 1);
        bag.Add("material.slag", 1);

        // Build a Strained metal: smelt, then infuse Dense and Vital.
        var denseGranite = RunOk(runner, new("craft.prospect_stone", "material.granite", Array.Empty<string>(), Dense)).Deposited[0].ItemId;
        var vitalSage = RunOk(runner, new("craft.identify_virtues", "material.sageleaf", Array.Empty<string>(), Vital)).Deposited[0].ItemId;
        RunOk(runner, new("craft.smelt_iron", "material.iron_ore", Array.Empty<string>()));
        var worked = RunOk(runner, new("craft.infuse_metal", "material.iron_ingot", new[] { denseGranite })).Deposited[0].ItemId;

        // Mend at Worked: the ENGINE refuses (Worked is Restore's ceiling) — and an engine
        // refusal, unlike a landed gamble, spends nothing: the slag stays.
        var refused = runner.Run(new VerbActionInvocation("craft.mend", worked, Array.Empty<string>()));
        Assert.Equal(VerbResultKind.Refused, refused.Outcome!.Kind);
        Assert.Equal(VerbFailureReason.ConditionAtCeiling, refused.Outcome.Failure);
        Assert.Equal(1, bag.GetQuantity("material.slag"));

        var strained = RunOk(runner, new("craft.infuse_metal", worked, new[] { vitalSage })).Deposited[0].ItemId;

        // Missing the extra cost is a gate, not a refusal.
        bag.TryRemove("material.slag", 1);
        var unpaid = runner.Run(new VerbActionInvocation("craft.mend", strained, Array.Empty<string>()));
        Assert.Equal(VerbActionGateFailure.MissingExtraCosts, unpaid.GateFailure);

        bag.Add("material.slag", 1);
        var mended = RunOk(runner, new("craft.mend", strained, Array.Empty<string>()));
        Assert.Equal(0, bag.GetQuantity("material.slag"));
        Assert.Equal(Condition.Worked, content.Materials.GetById(mended.Deposited[0].ItemId).IdentityState!.Condition);
    }

    // --- Harness -------------------------------------------------------------

    private static VerbActionResult RunOk(VerbActionRunner runner, VerbActionInvocation invocation)
    {
        var result = runner.Run(invocation);
        Assert.Null(result.GateFailure);
        Assert.Equal(VerbResultKind.Succeeded, result.Outcome!.Kind);
        return result;
    }

    private static (VerbActionRunner Runner, Inventory Bag, ContentBundle Content) Harness(
        Dictionary<string, int>? levels = null)
    {
        var content = new ContentBundle
        {
            Materials = TestPaths.LoadStore<MaterialDefinition>("materials"),
            Identities = TestPaths.LoadStore<IdentityDefinition>("identities"),
            VerbActions = TestPaths.LoadStore<VerbActionDefinition>("verb_actions"),
            Professions = TestPaths.LoadStore<ProfessionDefinition>("professions"),
            Byproducts = TestPaths.LoadStore<ByproductDefinition>("byproducts"),
        };
        var bag = new Inventory();
        var runner = new VerbActionRunner(
            content,
            new IdentityCraftingEngine(content, new SeededRandom(11)),
            new EmergentRegistry(content.Materials),
            () => bag,
            id => levels?.GetValueOrDefault(id, 99) ?? 99);
        return (runner, bag, content);
    }
}
