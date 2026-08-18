using System.Text.RegularExpressions;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Presentation;
using Xunit;

namespace Dungeons.Tests.Presentation;

/// <summary>
/// R3 — the §6 reveal hierarchy: identity → combat stats → traits → material influence.
/// The card speaks gameplay language (damage, timing, armour — the numbers combat uses) and
/// never the material-property wall D30 retired.
/// </summary>
public class ItemReadingTests
{
    private static (EquipmentAssemblyEngine Engine, ContentBundle Content, Inventory Inventory) Harness()
    {
        var content = new ContentBundle
        {
            Materials = TestPaths.LoadStore<MaterialDefinition>("materials"),
            Properties = TestPaths.LoadStore<PropertyDefinition>("properties"),
            Traits = TestPaths.LoadStore<TraitDefinition>("traits"),
            Forms = TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms"),
            Equipment = TestPaths.LoadStore<EquipmentDefinition>("equipment"),
            Moves = TestPaths.LoadStore<Dungeons.Combat.MoveDefinition>("moves"),
        };

        var inventory = new Inventory();
        var engine = new EquipmentAssemblyEngine(
            content, () => inventory, new MaterialStateResolver(content.Properties), new InstanceIdSource());

        inventory.Add("material.iron_ingot", 6);
        inventory.Add("material.leather", 3);

        return (engine, content, inventory);
    }

    private static EquipmentAssemblyRequest Longsword => new("form.longsword", new Dictionary<string, string>
    {
        ["edge"] = "material.iron_ingot",
        ["core"] = "material.iron_ingot",
        ["binding"] = "material.leather",
    });

    [Fact]
    public void TheRevealLeadsWithIdentityThenCombatStatsNeverProperties()
    {
        var (engine, content, _) = Harness();
        var outcome = engine.Assemble(Longsword);
        Assert.True(outcome.Success, outcome.Failure.ToString());

        var definition = content.Equipment.GetById(outcome.Item!.BaseDefinitionId);
        var card = SemanticFormat.Item(ItemReadings.From(outcome.Item, definition, content));

        var lines = card.Split('\n');
        Assert.StartsWith(outcome.Name, lines[0]);                    // 1. identity first
        Assert.Contains("Grants: ", card);                            // 2. real moves
        Assert.Contains("Slashing", card);                            //    with lane damage
        Assert.Contains("impact", card);                              //    and timing
        Assert.Contains("Made of: ", card);                           // 7. material influence
        Assert.Contains("Iron Ingot", card);

        // The §6 ban: no material-property wall on the card.
        Assert.DoesNotContain("hardness", card, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#", card);                             // no instance-id debris
    }

    [Fact]
    public void TheItemStripReplacesThePropertyWall()
    {
        var (engine, content, _) = Harness();
        var outcome = engine.Assemble(Longsword);

        var definition = content.Equipment.GetById(outcome.Item!.BaseDefinitionId);
        var strip = SemanticFormat.ItemStrip(ItemReadings.From(outcome.Item, definition, content));

        Assert.StartsWith(outcome.Name, strip);
        Assert.Contains("dmg", strip);
        Assert.Contains("impact", strip);
        Assert.DoesNotContain("hardness", strip, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mass", strip, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The preview is the same reading the minted item produces — one seam, no drift.</summary>
    [Fact]
    public void TheFabricationPreviewMatchesTheMintedItemsCard()
    {
        var (engine, content, _) = Harness();

        var projection = engine.Preview(Longsword);
        var form = content.Forms.GetById("form.longsword");
        var previewReading = ItemReadings.From(projection, form, content);
        var preview = SemanticFormat.Fabrication(projection, previewReading);

        Assert.StartsWith($"Would make: {projection.Name}", preview);
        Assert.Contains("✦ first of its kind", preview);
        Assert.Contains("Grants: ", preview);
        Assert.Contains("Made of: ", preview);
        Assert.Contains("edge Iron Ingot", preview);

        var outcome = engine.Assemble(Longsword);
        var definition = content.Equipment.GetById(outcome.Item!.BaseDefinitionId);
        var mintedCard = SemanticFormat.Item(ItemReadings.From(outcome.Item, definition, content));

        // Every combat line the preview promised appears verbatim on the minted card.
        foreach (var line in preview.Split('\n').Where(l => l.StartsWith("Grants: ", StringComparison.Ordinal)))
            Assert.Contains(line, mintedCard);
    }

    [Fact]
    public void ArmourFormsReadArmourAndResistances()
    {
        var (engine, content, _) = Harness();
        var outcome = engine.Assemble(new EquipmentAssemblyRequest("form.vest", new Dictionary<string, string>
        {
            ["shell"] = "material.leather",
        }));
        Assert.True(outcome.Success, outcome.Failure.ToString());

        var definition = content.Equipment.GetById(outcome.Item!.BaseDefinitionId);
        var reading = ItemReadings.From(outcome.Item, definition, content);

        // The slot reads as the body location it is worn on, not as the category "armor" —
        // once head, hands and feet exist, every one of them is armour too (D32).
        Assert.Equal("body", reading.Slot);
        Assert.NotNull(definition.Armor);
        Assert.Empty(reading.Moves);
    }

    [Fact]
    public void SlotFitSpeaksReasonsInWords()
    {
        var (_, content, _) = Harness();
        var glossary = new PropertyGlossary(content.Properties);
        var materialStates = new MaterialStateResolver(content.Properties);
        var form = content.Forms.GetById("form.longsword");

        var iron = content.Materials.GetById("material.iron_ingot");
        var edge = SemanticFormat.SlotFit(
            SlotReadings.For(form, "edge", iron, materialStates.StateOf(iron), content.Traits), glossary);

        Assert.Contains("bears most of the item", edge);
        Assert.Contains("◆ Hardness", edge);
        Assert.Contains("heavily", edge);
        Assert.DoesNotMatch(new Regex(@"\d"), edge);

        var binding = SemanticFormat.SlotFit(
            SlotReadings.For(form, "binding", iron, materialStates.StateOf(iron), content.Traits), glossary);
        Assert.Contains("won't take this", binding);
        Assert.Contains("hide", binding);
    }
}
