using Dungeons.Affixes;
using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Crafting;

/// <summary>
/// The weapon archetypes, and the naming mechanism that lets ~180 weapon names live on ten of
/// them.
///
/// <para>The rule the whole approach rests on: a name variant is <b>cosmetic by construction</b>.
/// A Falchion and a Scimitar are one blueprint with two names — if a variant could ever change a
/// number, this would be 180 forms pretending to be ten.</para>
/// </summary>
public class WeaponFormTests
{
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

    private static (EquipmentAssemblyEngine Engine, ContentBundle Content) Harness(int seed = 99)
    {
        var content = Content();
        var bag = new Inventory();
        var engine = new EquipmentAssemblyEngine(
            content, () => bag, new MaterialStateResolver(content.Properties),
            new InstanceIdSource(), new SeededRandom(seed));

        foreach (var material in content.Materials.GetAll())
            bag.Add(material.Id, 8);

        return (engine, content);
    }

    private static IReadOnlyList<EquipmentBlueprintDefinition> Weapons() =>
        TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms").GetAll()
            .Where(form => form.Type == EquipmentSlot.Weapon)
            .ToList();

    // ---- Names ------------------------------------------------------------------------------

    /// <summary>
    /// No noun may belong to two forms. If "Falchion" were both a sword and an axe, the name
    /// would stop telling the player what they are holding — which is the one job it has.
    /// </summary>
    [Fact]
    public void NoNounIsClaimedByTwoForms()
    {
        var owners = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var form in TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms").GetAll())
        foreach (var noun in form.NameVariants.Append(form.Name))
        {
            if (!owners.TryGetValue(noun, out var list))
                owners[noun] = list = new List<string>();
            list.Add(form.Id);
        }

        var shared = owners.Where(pair => pair.Value.Count > 1)
            .Select(pair => $"{pair.Key} → {string.Join(", ", pair.Value)}")
            .ToList();

        Assert.True(shared.Count == 0, "nouns claimed by more than one form: " + string.Join(" | ", shared));
    }

    /// <summary>A form must not list the same noun twice, including its own canonical name.</summary>
    [Fact]
    public void AFormNeverRepeatsANoun()
    {
        foreach (var form in TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms").GetAll())
        {
            var nouns = form.NameVariants.Append(form.Name).ToList();
            Assert.Equal(nouns.Count, nouns.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    /// <summary>
    /// The fairness rule (§6.2c) reaching the name: components are consumed forever, so the
    /// noun the preview promised must be the noun the bench mints — which is why the variant is
    /// picked from the signature and never from the RNG.
    /// </summary>
    [Fact]
    public void TheVariantNameIsDeterministicAndSurvivesTheProjection()
    {
        var request = new EquipmentAssemblyRequest("form.longsword", new Dictionary<string, string>
        {
            ["edge"] = "material.iron_ingot",
            ["core"] = "material.oak_plank",
            ["binding"] = "material.leather",
        });

        // Different engines, different seeds: the name must not move.
        var first = Harness(seed: 1).Engine.Preview(request).Name;
        var second = Harness(seed: 2).Engine.Preview(request).Name;
        var minted = Harness(seed: 3).Engine.Assemble(request).Name;

        Assert.Equal(first, second);
        Assert.Equal(first, minted);
    }

    /// <summary>
    /// Different materials must be able to reach different nouns, or the variant list is
    /// decoration — every longsword would be a Kilij forever.
    /// </summary>
    [Fact]
    public void DifferentMaterialsReachDifferentNouns()
    {
        var (engine, _) = Harness();

        var names = new[] { "material.iron_ingot", "material.copper_ingot", "material.tin_ingot", "material.silver_ingot", "material.cobalt_ingot", "material.nickel_ingot" }
            .Select(metal => engine.Preview(new EquipmentAssemblyRequest("form.longsword", new Dictionary<string, string>
            {
                ["edge"] = metal, ["core"] = "material.oak_plank", ["binding"] = "material.leather",
            })).Name)
            .ToHashSet();

        Assert.True(names.Count > 1, "every material produced the same noun — the variant pick is not spreading.");
    }

    // ---- The archetypes ----------------------------------------------------------------------

    /// <summary>Every weapon must grant a move that can actually fire. The validator enforces the
    /// tag gate; this pins that the shipped set has not regressed to zero-move weapons.</summary>
    [Fact]
    public void EveryWeaponGrantsAMoveItCanActuallyFire()
    {
        var moves = TestPaths.LoadStore<MoveDefinition>("moves");

        foreach (var weapon in Weapons())
        {
            Assert.NotEmpty(weapon.Moves);

            var tags = new HashSet<string>(weapon.Tags, StringComparer.OrdinalIgnoreCase);
            var usable = weapon.Moves.Count(grant =>
                moves.GetById(grant.Id).Requires.All(condition =>
                    !string.Equals(condition.Kind, "equippedTag", StringComparison.OrdinalIgnoreCase)
                    || tags.Contains(condition.Text)));

            Assert.True(usable > 0, $"{weapon.Id} grants only moves it cannot satisfy.");
        }
    }

    /// <summary>
    /// The Longbow is the pass's sharpest counter-example and the reason it was worth adding: it
    /// is the only weapon that reads <b>flexibility</b> harder than hardness, so a bow limbed in
    /// iron is a bad bow. Every instinct the Longsword teaches is wrong here.
    /// </summary>
    [Fact]
    public void ABowLimbedInWoodBeatsTheSameBowLimbedInMetal()
    {
        var (engine, _) = Harness();

        double Flex(string limb) => engine.Preview(new EquipmentAssemblyRequest("form.longbow", new Dictionary<string, string>
        {
            ["limb"] = limb, ["string"] = "material.hemp_fiber", ["grip"] = "material.leather",
        })).Stats.GetValueOrDefault("flexibility");

        Assert.True(Flex("material.yew_log") > Flex("material.iron_ingot"),
            "an iron-limbed bow should be worse than a yew one — the limb is read for flex, not hardness.");
    }

    /// <summary>
    /// The Maul is the one form that wants to be heavy: mass is read off its head at the highest
    /// weight in the file. A dense, soft metal is a bad sword and a fine maul, which is the
    /// placement argument applied to a property other than hardness.
    /// </summary>
    [Fact]
    public void AMaulRewardsTheDenseMetalASwordWouldWaste()
    {
        var (engine, _) = Harness();

        double MaulMass(string head) => engine.Preview(new EquipmentAssemblyRequest("form.maul", new Dictionary<string, string>
        {
            ["head"] = head, ["haft"] = "material.oak_plank",
        })).Stats.GetValueOrDefault("mass");

        double SwordHardness(string edge) => engine.Preview(new EquipmentAssemblyRequest("form.longsword", new Dictionary<string, string>
        {
            ["edge"] = edge, ["core"] = "material.oak_plank", ["binding"] = "material.leather",
        })).Stats.GetValueOrDefault("hardness");

        // Lead: heavy and soft. Better in the maul, worse in the sword — both at once.
        Assert.True(MaulMass("material.lead_ingot") > MaulMass("material.iron_ingot"));
        Assert.True(SwordHardness("material.lead_ingot") < SwordHardness("material.iron_ingot"));
    }

    /// <summary>
    /// The second archetype wave's identity claims, which are all about what a form <em>refuses</em>
    /// to read.
    ///
    /// <para>A Sling reads no hardness at all — it is cord and a pouch, and the stone is not part
    /// of the weapon. A Whip reads flexibility harder than anything else in the file, including
    /// the bow's limb. Knuckles read the least mass of any weapon. Each of those is the reason
    /// the form exists rather than being a variant name on something else, so each is asserted.
    /// </para>
    /// </summary>
    [Fact]
    public void TheLightWeaponsEarnTheirPlaceByWhatTheyRefuseToRead()
    {
        var forms = TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms");

        double WeightOf(string formId, string stat) =>
            forms.GetById(formId).StatMap.TryGetValue(stat, out var reads)
                ? reads.Sum(read => read.Weight)
                : 0;

        // A sling is cord and a pouch. Hardness would be a claim about the ammunition.
        Assert.Equal(0, WeightOf("form.sling", "hardness"));
        Assert.True(WeightOf("form.sling", "flexibility") > 0);

        // The whip leans on ONE component harder than any form leans on anything: the lash is
        // the whole weapon. The Longbow still reads more flexibility in TOTAL (limb plus string),
        // and should — being the flexibility weapon is the bow's identity, and the whip's is
        // being a single flexible thing on a handle.
        double HeaviestSingleRead(string formId, string stat) =>
            forms.GetById(formId).StatMap.TryGetValue(stat, out var reads)
                ? reads.Max(read => read.Weight)
                : 0;

        var whipLash = HeaviestSingleRead("form.whip", "flexibility");
        foreach (var other in forms.GetAll().Where(form => form.Id != "form.whip"))
            Assert.True(whipLash > HeaviestSingleRead(other.Id, "flexibility"),
                $"{other.Id} reads flexibility off one slot at least as hard as the whip reads its lash.");

        Assert.True(WeightOf("form.longbow", "flexibility") > WeightOf("form.whip", "flexibility"),
            "the bow must still be the flexibility weapon overall.");

        // Knuckles are the smallest weapon there is.
        var knuckleMass = WeightOf("form.knuckles", "mass");
        foreach (var other in Weapons().Where(form => form.Id != "form.knuckles"))
            Assert.True(knuckleMass < WeightOf(other.Id, "mass"),
                $"{other.Id} reads no more mass than a pair of knuckles.");
    }

    /// <summary>
    /// The Halberd exists as the Warspear's opposite: a spear flexes on the thrust and reads its
    /// haft for flex, while a halberd is a weight on a lever and wants the haft stiff. If the
    /// halberd ever reads flex off its haft, the pair has collapsed into one weapon.
    /// </summary>
    [Fact]
    public void TheHalberdIsTheSpearsOppositeAndNotACopyOfIt()
    {
        var forms = TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms");

        var spearHaftFlex = forms.GetById("form.warspear").StatMap["flexibility"]
            .Where(read => read.Slot == "haft").Sum(read => read.Weight);
        var halberdHaftFlex = forms.GetById("form.halberd").StatMap["flexibility"]
            .Where(read => read.Slot == "haft").Sum(read => read.Weight);

        Assert.True(spearHaftFlex > 0, "the spear must read flex off its haft — that is its whole identity.");
        Assert.Equal(0, halberdHaftFlex);
    }

    /// <summary>Weapons are the bulk of the form file now, so the "no two forms are the same
    /// form" rule is re-checked over just them — a copy-pasted axe is the easy mistake.</summary>
    [Fact]
    public void NoTwoWeaponsAreTheSameWeapon()
    {
        string Shape(EquipmentBlueprintDefinition form) =>
            string.Join("+", form.Slots.Keys.OrderBy(name => name, StringComparer.Ordinal))
            + " reads " + string.Join(",", form.StatMap.Values
                .SelectMany(reads => reads.Select(read => read.Property))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(property => property, StringComparer.Ordinal));

        var duplicates = Weapons().GroupBy(Shape).Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} → {string.Join(", ", group.Select(f => f.Id))}")
            .ToList();

        Assert.True(duplicates.Count == 0, "weapons that are the same weapon: " + string.Join(" | ", duplicates));
    }
}
