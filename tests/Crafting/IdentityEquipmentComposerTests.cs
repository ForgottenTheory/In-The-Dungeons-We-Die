using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Crafting.Identity;
using Dungeons.Items;
using Xunit;

namespace Dungeons.Tests.Crafting;

/// <summary>
/// The item side of identity fabrication (migration Phase 3 — D46 base reads, D51
/// expression): the parity pin that calibrates base units to combat units, the
/// union/cap/dormancy rules stated by D51, the readable selection order, and the naming
/// rule that keeps "-bound" meaning what was worked into a material rather than what the
/// item was assembled from.
/// </summary>
public class IdentityEquipmentComposerTests
{
    private const string Vital = "identity.vital";
    private const string Dense = "identity.dense";
    private const string Keen = "identity.keen";

    // --- The §11.5 parity pin ------------------------------------------------

    [Fact]
    public void APlainIronLongswordMatchesTheAuthoredIronSword()
    {
        // The calibration promise (D46): one scale constant, pinned so base reads land where
        // the authored reference item already sits. The authored Iron Sword carries mass 3 —
        // +3 damage and (at 2 ticks per mass) +6 windup through the equipment resolver.
        var content = ShippedContent();
        var composition = IdentityEquipmentComposer.Compose(
            content.Forms.GetById("form.longsword"),
            PlainIronLongswordComponents(content),
            content);

        Assert.Equal(IdentityCompositionFailure.None, composition.Failure);
        var authoredIronSword = TestPaths.LoadStore<EquipmentDefinition>("equipment").GetById("equip.iron_sword");
        Assert.Equal(authoredIronSword.BaseProperties.Get(ItemProperties.Mass), composition.BaseDelivery.DamageBonus, 1);
        Assert.Equal(6, composition.BaseDelivery.WindupTicks);
        Assert.Equal(0.0, composition.BaseDelivery.Armor);
    }

    [Fact]
    public void PlainGearStaysPlain()
    {
        // Reality test 1: no identities anywhere → no expression, no dormancy, a plain name.
        var content = ShippedContent();
        var composition = IdentityEquipmentComposer.Compose(
            content.Forms.GetById("form.longsword"),
            PlainIronLongswordComponents(content),
            content);

        Assert.Empty(composition.Expressed);
        Assert.Empty(composition.Dormant);
        Assert.Equal("Iron Longsword", composition.Name);
    }

    [Fact]
    public void ABucklerReadsToughnessIntoArmor()
    {
        var content = ShippedContent();
        var iron = content.Materials.GetById("material.iron_ingot");
        var composition = IdentityEquipmentComposer.Compose(
            content.Forms.GetById("form.buckler"),
            new Dictionary<string, (MaterialDefinition, IdentityMaterialState)>
            {
                ["face"] = (iron, IdentityStateResolver.StateOf(iron)!),
            },
            content);

        // Iron toughness 6 × the shared scale (0.5) — armor in combat units, damage nothing.
        Assert.Equal(3.0, composition.BaseDelivery.Armor, 1);
        Assert.Equal(0.0, composition.BaseDelivery.DamageBonus);
    }

    // --- D51: union, cap, dormancy, readable selection ------------------------

    [Fact]
    public void ExpressionIsTheUnionUpToTheFormCapAndTheRestGoDormant()
    {
        var content = InMemoryContent();
        var edgeMetal = MetalState() with
        {
            Identities = new[] { new IdentityStake(Dense, 1), new IdentityStake(Keen, 1) },
        };
        var gripWood = WoodState() with
        {
            Identities = new[] { new IdentityStake(Vital, 1) },
        };

        var composition = Compose(content, TwoSlotForm(identityCap: 2), edgeMetal, gripWood);

        // Edge priority beats grip priority; within the edge, rank ties break by id.
        Assert.Equal(new[] { Dense, Keen }, composition.Expressed.Select(stake => stake.Id));
        var dormant = Assert.Single(composition.Dormant);
        Assert.Equal(Vital, dormant.Id);
    }

    [Fact]
    public void TheSameIdentityFromTwoSlotsMergesAtItsHighestRank()
    {
        var content = InMemoryContent();
        var edgeMetal = MetalState() with { Identities = new[] { new IdentityStake(Vital, 1) } };
        var gripWood = WoodState() with { Identities = new[] { new IdentityStake(Vital, 3) } };

        var composition = Compose(content, TwoSlotForm(identityCap: 2), edgeMetal, gripWood);

        var expressed = Assert.Single(composition.Expressed);
        Assert.Equal(Vital, expressed.Id);
        Assert.Equal(3, expressed.Rank);
        Assert.Empty(composition.Dormant);
    }

    [Fact]
    public void SlotPriorityDecidesExpressionBeforeRank()
    {
        // The edge speaks before the grip even when the grip's identity is deeper — the
        // selection order is priority → rank → contribution → id, and it is readable on
        // purpose (D51 forbids percentage apertures here).
        var content = InMemoryContent();
        var edgeMetal = MetalState() with { Identities = new[] { new IdentityStake(Dense, 1) } };
        var gripWood = WoodState() with { Identities = new[] { new IdentityStake(Vital, 4) } };

        var composition = Compose(content, TwoSlotForm(identityCap: 1), edgeMetal, gripWood);

        Assert.Equal(Dense, Assert.Single(composition.Expressed).Id);
        Assert.Equal(Vital, Assert.Single(composition.Dormant).Id);
    }

    [Fact]
    public void TheSameMaterialExpressesTheSameIdentitiesInEveryForm()
    {
        // Reality test 5's identical-floor rule: form bias lives in generation weights,
        // never in floor eligibility. The same Dense+Vital iron expresses both identities
        // on a longsword and on a buckler alike.
        var content = ShippedContent();
        var iron = content.Materials.GetById("material.iron_ingot");
        var leather = content.Materials.GetById("material.leather");
        var denseVitalIron = IdentityStateResolver.StateOf(iron)! with
        {
            Identities = new[] { new IdentityStake(Dense, 1), new IdentityStake(Vital, 1) },
        };

        var onLongsword = IdentityEquipmentComposer.Compose(
            content.Forms.GetById("form.longsword"),
            new Dictionary<string, (MaterialDefinition, IdentityMaterialState)>
            {
                ["edge"] = (iron, denseVitalIron),
                ["core"] = (iron, denseVitalIron),
                ["binding"] = (leather, IdentityStateResolver.StateOf(leather)!),
            },
            content);
        var onBuckler = IdentityEquipmentComposer.Compose(
            content.Forms.GetById("form.buckler"),
            new Dictionary<string, (MaterialDefinition, IdentityMaterialState)>
            {
                ["face"] = (iron, denseVitalIron),
            },
            content);

        Assert.Equal(
            onLongsword.Expressed.Select(stake => stake.Id).OrderBy(id => id),
            onBuckler.Expressed.Select(stake => stake.Id).OrderBy(id => id));
        Assert.Empty(onLongsword.Dormant);
        Assert.Empty(onBuckler.Dormant);
    }

    // --- Naming --------------------------------------------------------------

    [Fact]
    public void AnAssembledComponentNeverEarnsItsBoundAdjective()
    {
        // The leather binding is 15% of the sword — enough weight for "-bound" — but it was
        // assembled in, not worked in, so the sword stays "Iron Longsword". "-bound" names
        // crafting provenance (the oak a tincture infused), or it names nothing.
        var content = ShippedContent();
        var composition = IdentityEquipmentComposer.Compose(
            content.Forms.GetById("form.longsword"),
            PlainIronLongswordComponents(content),
            content);

        Assert.DoesNotContain("Leatherbound", composition.Name);
    }

    [Fact]
    public void CraftedProvenanceKeepsItsBoundAdjectiveOnTheItem()
    {
        // An oak-infused ingot (oak in its roots, not among the slotted components) carries
        // "Oakbound" onto the finished item: "Vital Oakbound Iron Longsword".
        var content = ShippedContent();
        var iron = content.Materials.GetById("material.iron_ingot");
        var leather = content.Materials.GetById("material.leather");
        var oakboundIron = IdentityStateResolver.StateOf(iron)! with
        {
            Identities = new[] { new IdentityStake(Vital, 1) },
            Roots = new[]
            {
                new ProvenanceRoot("material.iron_ingot", 0.85),
                new ProvenanceRoot("material.oak", 0.15),
            },
        };

        var composition = IdentityEquipmentComposer.Compose(
            content.Forms.GetById("form.longsword"),
            new Dictionary<string, (MaterialDefinition, IdentityMaterialState)>
            {
                ["edge"] = (iron, oakboundIron),
                ["core"] = (iron, oakboundIron),
                ["binding"] = (leather, IdentityStateResolver.StateOf(leather)!),
            },
            content);

        Assert.Equal("Vital Oakbound Iron Longsword", composition.Name);
    }

    // --- Refusals ------------------------------------------------------------

    [Fact]
    public void AnUnmigratedFormIsRefused()
    {
        var content = InMemoryContent();
        var form = TwoSlotForm(identityCap: 2);
        var unmigrated = new EquipmentBlueprintDefinition
        {
            Id = form.Id, Name = form.Name, Type = form.Type, Slots = form.Slots,
        };

        var composition = Compose(content, unmigrated, MetalState(), WoodState());

        Assert.Equal(IdentityCompositionFailure.FormNotMigrated, composition.Failure);
    }

    [Fact]
    public void AMissingComponentIsRefused()
    {
        var content = InMemoryContent();
        var composition = IdentityEquipmentComposer.Compose(
            TwoSlotForm(identityCap: 2),
            new Dictionary<string, (MaterialDefinition, IdentityMaterialState)>
            {
                ["edge"] = (content.Materials.GetById("material.test_metal"), MetalState()),
            },
            content);

        Assert.Equal(IdentityCompositionFailure.MissingComponent, composition.Failure);
    }

    [Fact]
    public void ASlotTagGateRefusesTheWrongMaterial()
    {
        var content = InMemoryContent();
        var wood = content.Materials.GetById("material.test_wood");
        var composition = IdentityEquipmentComposer.Compose(
            TwoSlotForm(identityCap: 2),
            new Dictionary<string, (MaterialDefinition, IdentityMaterialState)>
            {
                ["edge"] = (wood, WoodState()), // the edge requires form:metal
                ["grip"] = (wood, WoodState()),
            },
            content);

        Assert.Equal(IdentityCompositionFailure.SlotTagMismatch, composition.Failure);
    }

    // --- Validator rules, each proven to fire --------------------------------

    [Fact]
    public void AnIdentityCapOutsideTheRangeFails()
    {
        AssertFormProblem(TwoSlotForm(identityCap: 9), "identity_cap 9");
    }

    [Fact]
    public void BaseReadsWithoutAnIdentityCapFail()
    {
        var form = TwoSlotForm(identityCap: 2);
        AssertFormProblem(new EquipmentBlueprintDefinition
        {
            Id = form.Id, Name = form.Name, Type = form.Type, Slots = form.Slots,
            BaseReads = form.BaseReads,
        }, "without identity_cap");
    }

    [Fact]
    public void ABaseReadFeedingAnUnknownItemStatFails()
    {
        var form = TwoSlotForm(identityCap: 2);
        form.BaseReads["handling"] = new[] { new BaseReadContribution { Slot = "grip", Stat = "give" } };

        AssertFormProblem(form, "unknown item stat 'handling'");
    }

    [Fact]
    public void ABaseReadOfAnUnknownBaseStatFails()
    {
        var form = TwoSlotForm(identityCap: 2);
        form.BaseReads["damage"] = new[] { new BaseReadContribution { Slot = "edge", Stat = "sharpness" } };

        AssertFormProblem(form, "unknown base stat 'sharpness'");
    }

    [Fact]
    public void ABaseReadOfAnUnknownSlotFails()
    {
        var form = TwoSlotForm(identityCap: 2);
        form.BaseReads["damage"] = new[] { new BaseReadContribution { Slot = "pommel", Stat = "bite" } };

        AssertFormProblem(form, "unknown slot 'pommel'");
    }

    [Fact]
    public void ANegativeIdentityPriorityFails()
    {
        var negative = new EquipmentBlueprintDefinition
        {
            Id = "form.test_negative", Name = "Test", Type = EquipmentSlot.Offhand,
            IdentityCap = 1,
            Slots = new Dictionary<string, BlueprintSlot>
            {
                ["face"] = new() { MassShare = 1.0, IdentityPriority = -1 },
            },
        };

        AssertFormProblem(negative, "identity_priority is negative");
    }

    // --- Harness -------------------------------------------------------------

    private static ContentBundle ShippedContent() => new()
    {
        Materials = TestPaths.LoadStore<MaterialDefinition>("materials"),
        Forms = TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms"),
        Identities = TestPaths.LoadStore<IdentityDefinition>("identities"),
    };

    private static Dictionary<string, (MaterialDefinition, IdentityMaterialState)> PlainIronLongswordComponents(
        ContentBundle content)
    {
        var iron = content.Materials.GetById("material.iron_ingot");
        var leather = content.Materials.GetById("material.leather");
        return new Dictionary<string, (MaterialDefinition, IdentityMaterialState)>
        {
            ["edge"] = (iron, IdentityStateResolver.StateOf(iron)!),
            ["core"] = (iron, IdentityStateResolver.StateOf(iron)!),
            ["binding"] = (leather, IdentityStateResolver.StateOf(leather)!),
        };
    }

    private static IdentityComposition Compose(
        ContentBundle content,
        EquipmentBlueprintDefinition form,
        IdentityMaterialState edgeState,
        IdentityMaterialState gripState) => IdentityEquipmentComposer.Compose(
            form,
            new Dictionary<string, (MaterialDefinition, IdentityMaterialState)>
            {
                ["edge"] = (content.Materials.GetById("material.test_metal"), edgeState),
                ["grip"] = (content.Materials.GetById("material.test_wood"), gripState),
            },
            content);

    /// <summary>Edge (priority 2, metal) + grip (priority 1, wood): the smallest form that
    /// exercises priority, tag gates and whole-item reads at once.</summary>
    private static EquipmentBlueprintDefinition TwoSlotForm(int identityCap) => new()
    {
        Id = "form.test_blade",
        Name = "Test Blade",
        Type = EquipmentSlot.Offhand, // no weapon-must-grant-moves rule in the way
        IdentityCap = identityCap,
        Slots = new Dictionary<string, BlueprintSlot>
        {
            ["edge"] = new()
            {
                RequiresTags = new[] { "form:metal" }, MassShare = 0.7, IdentityPriority = 2,
            },
            ["grip"] = new()
            {
                RequiresTags = new[] { "form:wood" }, MassShare = 0.3, IdentityPriority = 1,
            },
        },
        BaseReads = new Dictionary<string, IReadOnlyList<BaseReadContribution>>
        {
            ["damage"] = new[] { new BaseReadContribution { Slot = "edge", Stat = "bite" } },
            ["speed"] = new[] { new BaseReadContribution { Slot = BlueprintSlots.AllSlots, Stat = "heft" } },
        },
    };

    private static IdentityMaterialState MetalState() => new()
    {
        Capacity = 3,
        Roots = new[] { new ProvenanceRoot("material.test_metal", 1.0) },
    };

    private static IdentityMaterialState WoodState() => new()
    {
        Capacity = 3,
        Roots = new[] { new ProvenanceRoot("material.test_wood", 1.0) },
    };

    private static ContentBundle InMemoryContent()
    {
        var bundle = new ContentBundle();
        bundle.Materials.Add(new MaterialDefinition
        {
            Id = "material.test_metal", Name = "Test Metal",
            Tags = new[] { "form:metal" },
            Capacity = 3,
            Base = new MaterialBaseStats { Heft = 5, Bite = 6, Toughness = 6 },
        });
        bundle.Materials.Add(new MaterialDefinition
        {
            Id = "material.test_wood", Name = "Test Wood",
            Tags = new[] { "form:wood" },
            Capacity = 3,
            Base = new MaterialBaseStats { Heft = 4, Toughness = 4, Give = 6 },
        });
        return bundle;
    }

    private static void AssertFormProblem(EquipmentBlueprintDefinition form, string messageFragment)
    {
        var bundle = new ContentBundle();
        bundle.Forms.Add(form);
        var problems = ContentValidator.Validate(bundle);
        Assert.Contains(problems, p => p.Category == "forms" && p.Message.Contains(messageFragment));
    }
}
