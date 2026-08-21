using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Crafting.Identity;
using Dungeons.Items;
using Dungeons.Persistence;
using Dungeons.Presentation;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Presentation;

/// <summary>
/// The item card's identity layer (migration Phase 6): a minted item's sentences, stakes and
/// dormancy read in player language through the same card every item uses — closing the hole
/// where an equipped identity sword showed nothing about its effects.
/// </summary>
public class IdentityItemReadingTests
{
    private const string Vital = "identity.vital";
    private const string Dense = "identity.dense";

    [Fact]
    public void AMintedItemsCardSpeaksItsSentences()
    {
        var (content, item, definition) = MintDenseVitalLongsword();

        var card = SemanticFormat.Item(ItemReadings.From(item, definition, content));

        Assert.Contains("Identities: ", card);
        Assert.Contains("Dense", card);
        Assert.Contains("Vital", card);
        // The two floors: Dense's Impact and Vital's Vitality, in modifier-key words.
        Assert.Contains("Guaranteed: While Worn: ", card);
        Assert.Contains("Flat Damage", card);
        Assert.Contains("Max Health", card);
        // No engine vocabulary anywhere on the card.
        Assert.DoesNotContain("→", card);
        Assert.False(System.Text.RegularExpressions.Regex.IsMatch(card, "[a-z]_[a-z]"),
            $"an engine id leaked into the card:\n{card}");
    }

    [Fact]
    public void TheCardKeepsTheFourCategoriesApart()
    {
        // D50: the promise, the roll, the Signature and the price must stay distinguishable.
        // Authored sentences rather than a seeded mint, so every category is present.
        var content = ShippedContent();
        var instance = InstanceCarrying(
            new ItemEffectSentence(ItemEffectCategory.Floor, "while_worn", "sustain", "vitality", 8, 1.0),
            new ItemEffectSentence(ItemEffectCategory.Generated, "on_hit", "afflict", "kindling", 2, 0.3),
            new ItemEffectSentence(ItemEffectCategory.Signature, "on_block", "store", "bulwark", 1.12, 1.0),
            new ItemEffectSentence(ItemEffectCategory.Drawback, "on_hit", "afflict", "kindling", 1, 0.2, AfflictsWearer: true));

        var card = SemanticFormat.Item(ItemReadings.From(
            instance, content.Equipment.GetById("equip.iron_sword"), content));

        Assert.Contains("Guaranteed: While Worn: +8 Max Health", card);
        Assert.Contains("On Hit: 30% chance to inflict Burn (2)", card);
        Assert.Contains("Signature: On Block: ", card);
        Assert.Contains("Drawback: On Hit: 20% chance to suffer Burn (1)", card);
    }

    [Fact]
    public void RanksReadAsWordsNeverNumerals()
    {
        var content = ShippedContent();

        Assert.Equal("Vital", IdentityPhrases.Stake(new IdentityStake(Vital, 1), content));
        Assert.Equal("Vital (improved)", IdentityPhrases.Stake(new IdentityStake(Vital, 2), content));
        Assert.Equal("Vital (advanced)", IdentityPhrases.Stake(new IdentityStake(Vital, 3), content));
        Assert.Equal("Vital (build-changing)", IdentityPhrases.Stake(new IdentityStake(Vital, 4), content));
    }

    [Fact]
    public void DormantIdentitiesJoinTheDormantLine()
    {
        var content = ShippedContent();
        var withDormant = new ItemInstance
        {
            InstanceId = 2,
            BaseDefinitionId = "equip.iron_sword",
            ItemType = ItemType.Weapon,
            DisplayName = "Test Blade",
            ExpressedIdentities = new[] { new IdentityStake(Dense, 1) },
            DormantIdentities = new[] { new IdentityStake(Vital, 2) },
        };

        var card = SemanticFormat.Item(ItemReadings.From(
            withDormant, content.Equipment.GetById("equip.iron_sword"), content));

        Assert.Contains("Dormant: Vital (improved) — waits for a different form", card);
    }

    [Fact]
    public void TheStripCountsEffectsAndFlagsTheSpecialLayers()
    {
        var content = ShippedContent();
        var instance = InstanceCarrying(
            new ItemEffectSentence(ItemEffectCategory.Floor, "while_worn", "sustain", "vitality", 8, 1.0),
            new ItemEffectSentence(ItemEffectCategory.Generated, "on_hit", "afflict", "kindling", 2, 0.3),
            new ItemEffectSentence(ItemEffectCategory.Signature, "on_block", "store", "bulwark", 1.12, 1.0));

        var strip = SemanticFormat.ItemStrip(ItemReadings.From(
            instance, content.Equipment.GetById("equip.iron_sword"), content));

        Assert.Contains("3 effects", strip);
        Assert.Contains("Signature", strip);
        Assert.DoesNotContain("Drawback", strip);
    }

    // --- Harness -------------------------------------------------------------

    private static ItemInstance InstanceCarrying(params ItemEffectSentence[] sentences) => new()
    {
        InstanceId = 1,
        BaseDefinitionId = "equip.iron_sword",
        ItemType = ItemType.Weapon,
        DisplayName = "Test Blade",
        ExpressedIdentities = new[] { new IdentityStake(Dense, 1), new IdentityStake(Vital, 1) },
        IdentitySentences = sentences,
    };

    private static (ContentBundle Content, ItemInstance Item, EquipmentDefinition Definition) MintDenseVitalLongsword()
    {
        var content = ShippedContent();
        var inventory = new Dungeons.Items.Inventory();
        var engine = new IdentityFabricationEngine(
            content, () => inventory, new InstanceIdSource(), new SeededRandom(7));

        var iron = content.Materials.GetById("material.iron_ingot");
        var denseVitalIron = new MaterialDefinition
        {
            Id = "material.test_dense_vital_iron",
            Name = "Dense Vital Iron Ingot",
            Tags = iron.Tags,
            Capacity = 2,
            IdentityState = IdentityStateResolver.StateOf(iron)! with
            {
                Identities = new[] { new IdentityStake(Dense, 1), new IdentityStake(Vital, 1) },
            },
        };
        content.Materials.Add(denseVitalIron);
        inventory.Add(denseVitalIron.Id, 2);
        inventory.Add("material.leather", 1);

        var result = engine.Fabricate(new IdentityFabricationInvocation(
            "form.longsword",
            new Dictionary<string, string>
            {
                ["edge"] = denseVitalIron.Id, ["core"] = denseVitalIron.Id, ["binding"] = "material.leather",
            }));

        Assert.NotNull(result.Item);
        Assert.True(content.Equipment.TryGetById(result.Item!.BaseDefinitionId, out var definition),
            "the mint must register its derived definition where the card can read it.");
        return (content, result.Item!, definition!);
    }

    private static ContentBundle ShippedContent() => new()
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
        MoveModifiers = TestPaths.LoadStore<MoveModifierDefinition>("move_modifiers"),
        Equipment = TestPaths.LoadStore<EquipmentDefinition>("equipment"),
    };
}
