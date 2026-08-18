using System.Text;
using Dungeons.Affixes;
using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Randomness;
using Xunit;
using Xunit.Abstractions;

namespace Dungeons.Tests.Crafting;

/// <summary>
/// The Phase 4 breadth pass, checked as a claim rather than a count: <b>nine forms across nine
/// slots, each existing to exercise a different part of the material system</b>.
///
/// <para>The load-bearing test is <see cref="TheSameMetalIsExcellentInOneFormAndWastedInAnother"/>.
/// If a single material is simply best everywhere, forms are cosmetic and the whole
/// material-genetics idea collapses into "find the highest number". Everything else here exists
/// to keep that one honest.</para>
///
/// <para><see cref="RenderAFullLoadout"/> prints a fabricated head-to-foot set so a form can be
/// tuned by reading what it actually produced.</para>
/// </summary>
public class FormBreadthTests
{
    private readonly ITestOutputHelper _output;

    public FormBreadthTests(ITestOutputHelper output) => _output = output;

    private static ContentBundle Content() => new()
    {
        Materials = TestPaths.LoadStore<MaterialDefinition>("materials"),
        Properties = TestPaths.LoadStore<PropertyDefinition>("properties"),
        Traits = TestPaths.LoadStore<TraitDefinition>("traits"),
        Forms = TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms"),
        Equipment = TestPaths.LoadStore<EquipmentDefinition>("equipment"),
        Affixes = TestPaths.LoadStore<AffixDefinition>("affixes"),
        Moves = TestPaths.LoadStore<MoveDefinition>("moves"),
    };

    /// <summary>An engine with a generous bag, so a test never fails for want of components.</summary>
    private static (EquipmentAssemblyEngine Engine, ContentBundle Content, Inventory Bag) Harness(int seed = 4242)
    {
        var content = Content();
        var bag = new Inventory();
        var engine = new EquipmentAssemblyEngine(
            content, () => bag, new MaterialStateResolver(content.Properties),
            new InstanceIdSource(), new SeededRandom(seed));

        foreach (var material in content.Materials.GetAll())
            bag.Add(material.Id, 8);

        return (engine, content, bag);
    }

    // ---- The loadout the definition of done asks for ------------------------------------------

    /// <summary>
    /// One material per slot, chosen the way a player would: metal where hardness is read, hide
    /// and cloth where flexibility and insulation are, a resonant crystal in the focus.
    /// </summary>
    private static readonly (string Form, Dictionary<string, string> Slots)[] FullLoadout =
    {
        ("form.longsword", new() { ["edge"] = "material.iron_ingot", ["core"] = "material.oak_plank", ["binding"] = "material.leather" }),
        ("form.buckler", new() { ["face"] = "material.iron_ingot" }),
        ("form.helm", new() { ["crown"] = "material.iron_ingot", ["lining"] = "material.wolf_fur" }),
        ("form.vest", new() { ["shell"] = "material.boiled_leather" }),
        ("form.gauntlets", new() { ["glove"] = "material.supple_deer_leather", ["plating"] = "material.iron_ingot" }),
        ("form.treads", new() { ["sole"] = "material.cured_boar_leather", ["upper"] = "material.wolf_fur" }),
        ("form.focus", new() { ["stone"] = "material.ley_crystal", ["setting"] = "material.silver_ingot" }),

        // Two rings from the SAME form. Nothing about the pair says which hand it goes on — that
        // is decided at equip time, which is the whole point of the ring positions.
        ("form.ring", new() { ["band"] = "material.silver_ingot", ["inset"] = "material.amethyst" }),
        ("form.ring", new() { ["band"] = "material.copper_ingot", ["inset"] = "material.onyx" }),
    };

    /// <summary>
    /// Phase 4's definition of done, as one test: <b>fabricate a head-to-foot loadout, equip it,
    /// and have combat read the result.</b> Fabricating alone would not prove it — an item that
    /// mints fine and then cannot be worn, or that is worn and mitigates nothing, fails the
    /// milestone just as completely.
    /// </summary>
    [Fact]
    public void AFullHeadToFootLoadoutFabricatesEquipsAndArmsTheCharacter()
    {
        var (engine, content, _) = Harness();
        var equipment = new Dungeons.Items.Equipment();

        foreach (var (formId, slots) in FullLoadout)
        {
            var outcome = engine.Assemble(new EquipmentAssemblyRequest(formId, slots));
            Assert.True(outcome.Success, $"{formId} failed to assemble: {outcome.Failure}");

            var definition = content.Equipment.GetById(outcome.Item!.BaseDefinitionId);

            // Core picks the position — which is how the second ring reaches the second hand.
            // Nothing in a representative loadout should have to evict one of its own pieces.
            var displaced = equipment.EquipInFirstFreePosition(definition.Slot, outcome.Item);
            Assert.Null(displaced);
        }

        // Every slot the game knows about is filled by something a player fabricated.
        foreach (var slot in EquipmentSlots.DisplayOrder)
            Assert.True(equipment.InSlot(slot) is not null, $"nothing in the loadout fills the {slot} slot.");

        // What combat actually consumes. Armour is the sum of the five worn pieces…
        var worn = equipment.Slots
            .Where(pair => EquipmentSlots.GrantsArmor(pair.Key))
            .Select(pair => (content.Equipment.GetById(pair.Value.BaseDefinitionId), (ItemInstance?)pair.Value));

        var profile = EquipmentResolver.ResolveWornArmor(worn);
        Assert.True(profile.Armor > 0, "a full set of armour mitigates nothing.");
        Assert.NotEmpty(profile.Resistances);

        // …and the weapon still IS its moves (E4), which the trinket and the armour never touch.
        var weapon = equipment.InSlot(EquipmentSlot.Weapon)!;
        var moves = EquipmentResolver.ResolveWeaponMoves(
            content.Equipment.GetById(weapon.BaseDefinitionId), weapon, content.Moves);
        Assert.NotEmpty(moves);
    }

    [Fact]
    public void EveryShippedFormCoversASlotAndTheSlotsAreAllCovered()
    {
        var forms = TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms").GetAll();

        Assert.InRange(forms.Count, 6, 11); // the GDD's 6–8 target, with room either side

        // Expanded through the interchangeable positions: no form declares Ring2 and none should,
        // because a Ring1-declaring form fills either hand. Comparing declared types alone would
        // demand a duplicate ring form to satisfy a slot that is already reachable.
        var covered = forms
            .SelectMany(form => EquipmentSlots.InterchangeablePositions(form.Type))
            .ToHashSet();

        foreach (var slot in EquipmentSlots.DisplayOrder)
            Assert.Contains(slot, covered);
    }

    // ---- The claim the whole system rests on --------------------------------------------------

    /// <summary>
    /// There must be no best material — only a best <em>placement</em>. Iron is the right answer
    /// for a longsword's edge and the wrong answer for a spear's haft, because the spear reads
    /// flexibility from the part that is 60% of it while the sword reads hardness from the part
    /// that is 60% of it.
    ///
    /// <para>If this ever fails, forms have become cosmetic and fabrication has quietly become
    /// "use the highest numbers you own".</para>
    /// </summary>
    [Fact]
    public void TheSameMetalIsExcellentInOneFormAndWastedInAnother()
    {
        var (engine, _, _) = Harness();

        double Stat(string form, Dictionary<string, string> slots, string stat) =>
            engine.Preview(new EquipmentAssemblyRequest(form, slots)).Stats.GetValueOrDefault(stat);

        // A spear hafted in wood against the same spear hafted in iron.
        var woodHafted = new Dictionary<string, string>
            { ["point"] = "material.iron_ingot", ["haft"] = "material.yew_log", ["grip"] = "material.leather" };
        var ironHafted = new Dictionary<string, string>
            { ["point"] = "material.iron_ingot", ["haft"] = "material.iron_ingot", ["grip"] = "material.leather" };

        Assert.True(
            Stat("form.warspear", woodHafted, "flexibility") > Stat("form.warspear", ironHafted, "flexibility"),
            "an iron haft should cost the spear its flex — the stat_map reads the haft, not the point.");

        // And the reverse: the longsword reads hardness off the edge, so the edge metal is what
        // decides the blade. Tin passes the same `form:metal` gate iron does and is far softer —
        // the gate says what fits, never what it is worth.
        var ironEdged = new Dictionary<string, string>
            { ["edge"] = "material.iron_ingot", ["core"] = "material.oak_plank", ["binding"] = "material.leather" };
        var tinEdged = new Dictionary<string, string>
            { ["edge"] = "material.tin_ingot", ["core"] = "material.oak_plank", ["binding"] = "material.leather" };

        Assert.True(
            Stat("form.longsword", ironEdged, "hardness") > Stat("form.longsword", tinEdged, "hardness"),
            "hardness is read off the edge, so an iron edge must out-cut a tin one.");

        // The claim in one line: iron is the right answer in the slot the sword reads and the
        // wrong answer in the slot the spear reads. Same material, opposite verdict — possible
        // only because the form decides what gets read.
        Assert.True(
            Stat("form.warspear", ironHafted, "mass") > Stat("form.warspear", woodHafted, "mass"),
            "an iron haft should at least be heavier, or the material is doing nothing at all.");
    }

    /// <summary>
    /// No two forms may be the same form. A form is distinguished by <em>what it reads</em> or
    /// by <em>how it is built</em> — the Buckler and the Longsword both read hardness and mass,
    /// and that is fine, because one is a single component and the other is three. What would
    /// not be fine is a copy-pasted vest with a new name.
    ///
    /// <para>Plus the sharp case: the Focus is the only form that reads resonance at all, which
    /// is what gives a ley crystal anywhere to be excellent. Delete that read and every resonant
    /// material in the game becomes decoration.</para>
    /// </summary>
    [Fact]
    public void NoTwoFormsAreTheSameForm()
    {
        var forms = TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms").GetAll();

        var readsByForm = forms.ToDictionary(
            form => form.Id,
            form => form.StatMap.Values.SelectMany(reads => reads.Select(read => read.Property))
                .ToHashSet(StringComparer.OrdinalIgnoreCase));

        string Shape(EquipmentBlueprintDefinition form) =>
            string.Join("+", form.Slots.Keys.OrderBy(name => name, StringComparer.Ordinal))
            + " reads " + string.Join(",", readsByForm[form.Id].OrderBy(p => p, StringComparer.Ordinal));

        var byShape = forms.GroupBy(Shape).Where(group => group.Count() > 1).ToList();
        Assert.True(byShape.Count == 0,
            "forms that are indistinguishable: " + string.Join(" | ",
                byShape.Select(group => $"{group.Key} → {string.Join(", ", group.Select(f => f.Id))}")));

        foreach (var (formId, reads) in readsByForm)
            Assert.True(reads.Count > 0, $"{formId} reads no properties at all.");

        // The Focus is the only home for resonance.
        var resonanceReaders = readsByForm.Where(f => f.Value.Contains(ItemProperties.Resonance)).Select(f => f.Key);
        Assert.Equal(new[] { "form.focus" }, resonanceReaders);

        // …and the Ring the only home for conductivity and affinity, which nothing read before it
        // existed — that is why a ring is not simply a smaller focus. If a later form starts
        // reading either, decide deliberately whether the ring is still distinct.
        foreach (var property in new[] { ItemProperties.Conductivity, ItemProperties.Affinity })
            Assert.Equal(
                new[] { "form.ring" },
                readsByForm.Where(f => f.Value.Contains(property)).Select(f => f.Key));
    }

    // ---- The pipeline the breadth pass must not have disturbed --------------------------------

    /// <summary>Every new form must still produce a genome, and the genome must reflect where
    /// the form reads — that is the seam modifier eligibility hangs off.</summary>
    [Fact]
    public void EveryFormProducesAnItemPotentialWeightedByWhereItReads()
    {
        var (engine, _, _) = Harness();

        foreach (var (formId, slots) in FullLoadout)
        {
            var preview = engine.Preview(new EquipmentAssemblyRequest(formId, slots));
            Assert.True(preview.CanFabricate, $"{formId}: {preview.Failure}");
            Assert.NotEmpty(preview.Potential.MaterialInfluence);
            Assert.True(preview.Potential.MaterialStrength > 0, $"{formId} produced a zero-strength item.");
        }

        // The gauntlets read flexibility off the glove at nearly triple the weight they read
        // hardness off the plating, so a supple glove must out-influence the metal.
        var gauntlets = engine.Preview(new EquipmentAssemblyRequest("form.gauntlets", new Dictionary<string, string>
        {
            ["glove"] = "material.supple_deer_leather",
            ["plating"] = "material.iron_ingot",
        }));
        Assert.True(
            gauntlets.Potential.MaterialInfluence.GetValueOrDefault(ItemProperties.Flexibility)
            > gauntlets.Potential.MaterialInfluence.GetValueOrDefault(ItemProperties.Hardness),
            "gauntlets should be a flexibility item; if hardness leads, the stat_map weights are wrong.");
    }

    /// <summary>Pre-commit projection is the fairness rule (§6.2c): components are consumed
    /// forever, so what the preview promised has to be what the item is — for every new form,
    /// not just the one it was written against.</summary>
    [Fact]
    public void ThePreviewMatchesTheMintedItemForEveryForm()
    {
        foreach (var (formId, slots) in FullLoadout)
        {
            var (engine, content, _) = Harness();
            var request = new EquipmentAssemblyRequest(formId, slots);

            var preview = engine.Preview(request);
            var outcome = engine.Assemble(request);

            Assert.True(outcome.Success, $"{formId}: {outcome.Failure}");
            Assert.Equal(preview.Name, outcome.Name);

            var definition = content.Equipment.GetById(outcome.Item!.BaseDefinitionId);
            foreach (var (stat, value) in preview.Stats)
                Assert.Equal(value, definition.Properties[stat], precision: 6);

            Assert.Equal(preview.Expressed.Count, outcome.Expressed.Count);
            Assert.Equal(preview.Armor is null, definition.Armor is null);
        }
    }

    /// <summary>Only the slots that mitigate get an armour block. A focus is worn and a sword is
    /// held; neither becomes armour by being "not the other one".</summary>
    [Fact]
    public void OnlyArmourBearingSlotsDeriveAnArmourBlock()
    {
        var (engine, content, _) = Harness();

        foreach (var (formId, slots) in FullLoadout)
        {
            var outcome = engine.Assemble(new EquipmentAssemblyRequest(formId, slots));
            var definition = content.Equipment.GetById(outcome.Item!.BaseDefinitionId);

            Assert.Equal(EquipmentSlots.GrantsArmor(definition.Slot), definition.Armor is not null);
        }
    }

    /// <summary>A form whose modifier pool is empty is a form that always rolls nothing and
    /// looks merely unlucky. Every shipped form must have <em>something</em> it can roll.</summary>
    [Fact]
    public void EveryFormHasModifiersAvailableToIt()
    {
        var (engine, content, _) = Harness();
        var affixes = content.Affixes.GetAll();

        foreach (var (formId, slots) in FullLoadout)
        {
            var potential = engine.Preview(new EquipmentAssemblyRequest(formId, slots)).Potential;

            var eligible = affixes.Count(affix =>
                affix.Availability.FormsAny.Count == 0
                || affix.Availability.FormsAny.Any(tag =>
                    string.Equals(tag, potential.BlueprintId, StringComparison.OrdinalIgnoreCase)
                    || potential.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));

            Assert.True(eligible > 0, $"{formId} has no modifier in the whole catalog available to it.");
        }
    }

    // ---- Traits: the new forms carry real apertures, not copy-pasted numbers -------------------

    /// <summary>
    /// A trait-bearing component, of the kind the bench produces. Authored materials always
    /// start with no traits (that is what earns them at the bench), so a form's trait_expression
    /// can only be exercised against a crafted one.
    /// </summary>
    private static MaterialDefinition TraitedFur(string id) => new()
    {
        Id = id, Name = "Warded Fur",
        Tags = new[] { "form:fur", "form:hide", "state:raw", "rarity:common", "comp:organic", "origin:fauna" },
        Properties = new() { ["insulation"] = 70, ["mass"] = 20, ["flexibility"] = 60 },
        State = new MaterialState(
            new PropertySet(new Dictionary<string, double> { ["insulation"] = 70, ["mass"] = 20, ["flexibility"] = 60 }),
            50, 60, Lineage.ForBase(id), id)
        {
            Traits = new[]
            {
                new TraitInstance("trait.verdant", 80),    // vital
                new TraitInstance("trait.resilient", 70),  // structural
                new TraitInstance("trait.cycled", 60),     // vital
                new TraitInstance("trait.blighted", 50),   // vital
            },
        },
    };

    /// <summary>
    /// The Helm's lining passes vital traits at 0.8 and its crown at 0.3 — so the same fur is a
    /// different helm depending on where it goes. This is the §16.2 placement rule holding on a
    /// form written this phase rather than only on the one it was designed against.
    /// </summary>
    [Fact]
    public void TheNewFormsApertureGatesChangeWhatATraitIsWorth()
    {
        var (engine, content, bag) = Harness();
        var fur = TraitedFur("material.test_warded_fur");
        content.Materials.Add(fur);
        bag.Add(fur.Id, 4);

        var asLining = engine.Preview(new EquipmentAssemblyRequest("form.helm", new Dictionary<string, string>
        {
            ["crown"] = "material.iron_ingot", ["lining"] = fur.Id,
        }));
        var asCrown = engine.Preview(new EquipmentAssemblyRequest("form.helm", new Dictionary<string, string>
        {
            ["crown"] = fur.Id, ["lining"] = "material.wolf_fur",
        }));

        double Verdant(EquipmentAssemblyPreview preview) =>
            preview.Expressed.SingleOrDefault(t => t.Id == "trait.verdant")?.Magnitude ?? 0;

        Assert.True(Verdant(asLining) > Verdant(asCrown),
            $"the lining gates vital at 0.8 and the crown at 0.3, but expressed {Verdant(asLining)} vs {Verdant(asCrown)}.");
    }

    /// <summary>Smaller pieces hold less. A vest carries four traits where a helm carries three,
    /// and the overflow goes dormant rather than vanishing (§16.2's dormancy rule).</summary>
    [Fact]
    public void ASmallerFormHoldsFewerTraitsAndTheRestGoDormant()
    {
        var (engine, content, bag) = Harness();
        var fur = TraitedFur("material.test_warded_fur");
        content.Materials.Add(fur);
        bag.Add(fur.Id, 4);

        var vest = engine.Preview(new EquipmentAssemblyRequest("form.vest",
            new Dictionary<string, string> { ["shell"] = fur.Id }));
        var helm = engine.Preview(new EquipmentAssemblyRequest("form.helm",
            new Dictionary<string, string> { ["crown"] = "material.iron_ingot", ["lining"] = fur.Id }));

        Assert.Equal(4, vest.Expressed.Count);   // trait_cap 4
        Assert.Equal(3, helm.Expressed.Count);   // trait_cap 3
        Assert.NotEmpty(helm.Dormant);           // nothing is lost, only held back
        Assert.Equal(4, helm.Expressed.Count + helm.Dormant.Count);
    }

    // ---- The worked example ------------------------------------------------------------------

    [Fact]
    public void RenderAFullLoadout()
    {
        var (engine, content, _) = Harness();
        var page = new StringBuilder();
        page.AppendLine("───── A fabricated head-to-foot loadout ─────");

        // Equipped rather than merely fabricated, so the printed position is the one the item
        // actually ends up in — otherwise both rings report the Ring1 their definition declares.
        var equipment = new Dungeons.Items.Equipment();
        var worn = new List<(EquipmentDefinition Definition, ItemInstance? Instance)>();

        foreach (var (formId, slots) in FullLoadout)
        {
            var outcome = engine.Assemble(new EquipmentAssemblyRequest(formId, slots));
            var definition = content.Equipment.GetById(outcome.Item!.BaseDefinitionId);
            equipment.EquipInFirstFreePosition(definition.Slot, outcome.Item);
            var position = equipment.Slots.First(pair => ReferenceEquals(pair.Value, outcome.Item)).Key;

            if (EquipmentSlots.GrantsArmor(position))
                worn.Add((definition, outcome.Item));

            var stats = string.Join(", ", definition.Properties.OrderBy(p => p.Key).Select(p => $"{p.Key} {p.Value:0.##}"));
            var modifiers = outcome.Item.Affixes.Count == 0
                ? "—"
                : string.Join(", ", outcome.Item.Affixes.Select(a => $"{a.AffixId.Replace("affix.", "")} T{a.Tier}"));
            var traits = outcome.Expressed.Count == 0 ? "—" : string.Join(", ", outcome.Expressed.Select(t => t.Id.Replace("trait.", "")));

            page.AppendLine($"  [{position,-8}] {outcome.Name}");
            page.AppendLine($"             stats    {stats}");
            page.AppendLine($"             traits   {traits}");
            page.AppendLine($"             mods     {modifiers}");
        }

        var profile = EquipmentResolver.ResolveWornArmor(worn);
        page.AppendLine($"  → worn total: {profile.Armor:0.#} armor" +
                        (profile.Resistances.Count == 0
                            ? string.Empty
                            : ", resist " + string.Join(", ", profile.Resistances.OrderBy(r => r.Key).Select(r => $"{r.Key} {r.Value:P0}"))));

        _output.WriteLine(page.ToString());
    }
}
