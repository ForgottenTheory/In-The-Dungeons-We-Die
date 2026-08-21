using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Crafting.Identity;
using Dungeons.Items;
using Dungeons.Persistence;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Crafting;

/// <summary>
/// The weapon name library (D34): ~120 nouns ship as <c>name_variants</c>, cosmetic by
/// construction and picked deterministically from the derived definition id — carried across
/// the Phase 7 engine swap so the library outlived its first engine.
/// </summary>
public class WeaponFormTests
{
    [Fact]
    public void NoNounIsClaimedByTwoForms()
    {
        var forms = TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms").GetAll();
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var form in forms)
        {
            foreach (var noun in form.NameVariants.Append(form.Name))
            {
                if (claims.TryGetValue(noun, out var owner) && owner != form.Id)
                    Assert.Fail($"'{noun}' is claimed by both {owner} and {form.Id}.");
                claims[noun] = form.Id;
            }
        }
    }

    [Fact]
    public void AFormNeverRepeatsANoun()
    {
        foreach (var form in TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms").GetAll())
        {
            var distinct = form.NameVariants.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            Assert.True(distinct == form.NameVariants.Count, $"{form.Id} repeats a name variant.");
        }
    }

    [Fact]
    public void TheNounIsDeterministicAndThePreviewPromisesIt()
    {
        var content = Content();
        var inventory = new Dungeons.Items.Inventory();
        var engine = new IdentityFabricationEngine(
            content, () => inventory, new InstanceIdSource(), new SeededRandom(3));
        inventory.Add("material.iron_ingot", 4);
        inventory.Add("material.leather", 2);

        var invocation = new IdentityFabricationInvocation("form.longsword",
            new Dictionary<string, string>
            {
                ["edge"] = "material.iron_ingot", ["core"] = "material.iron_ingot", ["binding"] = "material.leather",
            });

        var promised = engine.Preview(invocation).Composition!.Name;
        var first = engine.Fabricate(invocation).Item!.DisplayName;
        var second = engine.Fabricate(invocation).Item!.DisplayName;

        Assert.Equal(promised, first);
        Assert.Equal(first, second); // two identical blades are never a Falchion and a Scimitar
    }

    [Fact]
    public void DifferentItemKindsCanReachDifferentNouns()
    {
        var form = TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms").GetById("form.longsword");

        var nouns = new[] { "00000000", "00000001", "00000007", "0000002a", "000000ff", "00001234" }
            .Select(hex => IdentityFabricationEngine.FormNoun(form, $"equip.emergent.i{hex}"))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(nouns.Count > 1, "the noun pick must actually spread across the variant library.");
    }

    private static ContentBundle Content() => new()
    {
        Materials = TestPaths.LoadStore<MaterialDefinition>("materials"),
        Forms = TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms"),
        Identities = TestPaths.LoadStore<IdentityDefinition>("identities"),
        SignatureTriggers = TestPaths.LoadStore<SignatureTriggerDefinition>("signature_triggers"),
        SignatureBehaviors = TestPaths.LoadStore<SignatureBehaviorDefinition>("signature_behaviors"),
        SignatureThemes = TestPaths.LoadStore<SignatureThemeDefinition>("signature_themes"),
        SignaturePayloads = TestPaths.LoadStore<SignaturePayloadDefinition>("signature_payloads"),
        Statuses = TestPaths.LoadStore<StatusDefinition>("statuses"),
        ModifierKeys = TestPaths.LoadStore<Dungeons.Modifiers.ModifierKeyDefinition>("modifier_keys"),
        Moves = TestPaths.LoadStore<MoveDefinition>("moves"),
        Equipment = TestPaths.LoadStore<EquipmentDefinition>("equipment"),
    };
}
