using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Professions;
using Xunit;

namespace Dungeons.Tests.Integration;

/// <summary>
/// The C2c checkpoint's machine-verifiable half: the ValidateForms failing-content gap
/// (HANDOFF debt — shipped content exercises the rules; broken stores didn't), the D28/D29
/// first-session sufficiency audit, and the D29.3 essence source audit. The playtest half —
/// balance feel — is the user's by standing decision.
/// </summary>
public class C2cAuditTests
{
    // ---- ValidateForms — per-rule failing content -------------------------------------------

    private static IReadOnlyList<ContentProblem> FormProblems(EquipmentBlueprintDefinition broken)
    {
        var forms = new DataStore<EquipmentBlueprintDefinition>();
        forms.Add(broken);
        var content = new ContentBundle
        {
            Forms = forms,
            Properties = TestPaths.LoadStore<PropertyDefinition>("properties"),
            Moves = TestPaths.LoadStore<Dungeons.Combat.MoveDefinition>("moves"),
        };

        return ContentValidator.Validate(content).Where(p => p.Category == "forms").ToList();
    }

    [Fact]
    public void AFormWithNoSlotsFailsLoudly() =>
        Assert.Contains(FormProblems(new EquipmentBlueprintDefinition { Id = "form.broken" }),
            p => p.Message.Contains("no slots"));

    [Fact]
    public void AZeroTraitCapFailsLoudly() =>
        Assert.Contains(FormProblems(new EquipmentBlueprintDefinition
        {
            Id = "form.broken",
            TraitCap = 0,
            Slots = { ["face"] = new BlueprintSlot() },
        }), p => p.Message.Contains("trait_cap"));

    [Fact]
    public void AnUnknownApertureCategoryFailsLoudly() =>
        Assert.Contains(FormProblems(new EquipmentBlueprintDefinition
        {
            Id = "form.broken",
            Slots = { ["face"] = new BlueprintSlot { TraitExpression = { ["sonic"] = 1.0 } } },
        }), p => p.Message.Contains("unknown category 'sonic'"));

    [Fact]
    public void AStatMapReadingAnUnknownPropertyFailsLoudly() =>
        Assert.Contains(FormProblems(new EquipmentBlueprintDefinition
        {
            Id = "form.broken",
            Slots = { ["face"] = new BlueprintSlot() },
            StatMap = { ["mass"] = new[] { new StatContribution { Slot = "face", Property = "wobble" } } },
        }), p => p.Message.Contains("unknown property 'wobble'"));

    [Fact]
    public void AStatMapReadingAnUnknownSlotFailsLoudly() =>
        Assert.Contains(FormProblems(new EquipmentBlueprintDefinition
        {
            Id = "form.broken",
            Slots = { ["face"] = new BlueprintSlot() },
            StatMap = { ["mass"] = new[] { new StatContribution { Slot = "edge", Property = "mass" } } },
        }), p => p.Message.Contains("unknown slot 'edge'"));

    [Fact]
    public void AFormGrantingAnUnknownMoveFailsLoudly() =>
        Assert.Contains(FormProblems(new EquipmentBlueprintDefinition
        {
            Id = "form.broken",
            Slots = { ["face"] = new BlueprintSlot() },
            Moves = new[] { new Dungeons.Combat.MoveGrantSpec { Id = "move.imaginary" } },
        }), p => p.Message.Contains("unknown move"));

    // ---- The Phase 4 rules: forms that load cleanly and are still broken ----------------------

    /// <summary>A form whose slots do not sum to 1 under-reads every <c>"*"</c> stat and
    /// under-weights every material influence, silently.</summary>
    [Fact]
    public void MassSharesThatDoNotSumToOneFailLoudly() =>
        Assert.Contains(FormProblems(new EquipmentBlueprintDefinition
        {
            Id = "form.broken",
            Type = Dungeons.Items.EquipmentSlot.Body,
            Tags = new[] { "armor" },
            Slots =
            {
                ["shell"] = new BlueprintSlot { MassShare = 0.5 },
                ["trim"] = new BlueprintSlot { MassShare = 0.2 },
            },
        }), p => p.Message.Contains("mass shares sum to"));

    /// <summary>A gate no shipped material can satisfy is a form nobody can ever assemble.</summary>
    [Fact]
    public void AnUnsatisfiableSlotGateFailsLoudly() =>
        Assert.Contains(FormProblemsAgainstShippedMaterials(new EquipmentBlueprintDefinition
        {
            Id = "form.broken",
            Type = Dungeons.Items.EquipmentSlot.Body,
            Tags = new[] { "armor" },
            Slots = { ["shell"] = new BlueprintSlot { RequiresTags = new[] { "form:unobtainium" } } },
        }), p => p.Message.Contains("could never be assembled"));

    /// <summary>Since E4 a weapon IS its moves; one granting none equips fine and leaves the
    /// player swinging nothing.</summary>
    [Fact]
    public void AWeaponFormGrantingNoMovesFailsLoudly() =>
        Assert.Contains(FormProblems(new EquipmentBlueprintDefinition
        {
            Id = "form.broken",
            Type = Dungeons.Items.EquipmentSlot.Weapon,
            Tags = new[] { "weapon" },
            Slots = { ["edge"] = new BlueprintSlot() },
        }), p => p.Message.Contains("grants no moves"));

    /// <summary>Most modifiers gate on <c>weapon</c> / <c>armor</c> / <c>shield</c>. A form
    /// missing its tag rolls nothing and looks merely unlucky.</summary>
    [Fact]
    public void AFormMissingTheTagItsModifierPoolGatesOnFailsLoudly()
    {
        Assert.Contains(FormProblems(new EquipmentBlueprintDefinition
        {
            Id = "form.broken",
            Type = Dungeons.Items.EquipmentSlot.Weapon,
            Moves = new[] { new Dungeons.Combat.MoveGrantSpec { Id = "move.iron_slash" } },
            Slots = { ["edge"] = new BlueprintSlot() },
        }), p => p.Message.Contains("does not carry the 'weapon' tag"));

        Assert.Contains(FormProblems(new EquipmentBlueprintDefinition
        {
            Id = "form.broken",
            Type = Dungeons.Items.EquipmentSlot.Feet,
            Slots = { ["sole"] = new BlueprintSlot() },
        }), p => p.Message.Contains("neither the 'armor' nor the 'shield' tag"));
    }

    /// <summary>The unsatisfiable-gate rule only fires when there are materials to check
    /// against — a bundle with an empty material store is a partial fixture, not content that
    /// forgot its library.</summary>
    private static IReadOnlyList<ContentProblem> FormProblemsAgainstShippedMaterials(EquipmentBlueprintDefinition broken)
    {
        var forms = new DataStore<EquipmentBlueprintDefinition>();
        forms.Add(broken);
        var content = new ContentBundle
        {
            Forms = forms,
            Materials = TestPaths.LoadStore<MaterialDefinition>("materials"),
            Properties = TestPaths.LoadStore<PropertyDefinition>("properties"),
            Moves = TestPaths.LoadStore<Dungeons.Combat.MoveDefinition>("moves"),
        };

        return ContentValidator.Validate(content).Where(p => p.Category == "forms").ToList();
    }

    // ---- D28/D29 — the first-session sufficiency audit ---------------------------------------

    /// <summary>A fresh character must reach a fabricated Longsword with professions only:
    /// ore from Mining, Smelt at Smithing 1 turning form:ore into form:metal, and a
    /// binding-legal hide from an early Beast Lore ladder rung. Content, not debug grants.</summary>
    [Fact]
    public void AFreshCharacterCanReachALongswordThroughProfessionsAlone()
    {
        var actions = TestPaths.LoadStore<ProfessionActionDefinition>("profession_actions").GetAll();
        var materials = TestPaths.LoadStore<MaterialDefinition>("materials");
        var processes = TestPaths.LoadStore<CraftingActionDefinition>("processes");
        var forms = TestPaths.LoadStore<EquipmentBlueprintDefinition>("forms");

        // Ore exists as a profession faucet (the P1 pin, restated here for the chain).
        Assert.Contains(actions, a => a.Outputs.Any(o => o.ItemId == "material.iron_ore"));

        // Smelt is reachable in the first session and turns ore into metal.
        var smelt = processes.GetById("process.smelt");
        Assert.True(smelt.Requires.ProfessionLevel <= 1, "Smelt must be a first-session craftingAction");
        Assert.Contains("form:ore", smelt.Requires.SubstrateTags);
        Assert.Contains("form:metal", smelt.TagEffects.Set);

        // A binding-legal material (form:hide / form:fiber) flows from an early action —
        // bonus outputs count: Track Boar's 30% hide at a 130-tick interval is a genuine
        // first-session faucet, a few tracks in.
        bool BindingLegal(string id) =>
            materials.TryGetById(id, out var m)
            && (m.Tags.Contains("form:hide", StringComparer.OrdinalIgnoreCase)
                || m.Tags.Contains("form:fiber", StringComparer.OrdinalIgnoreCase));

        var bindingActions = actions.Where(a =>
            a.Outputs.Any(o => BindingLegal(o.ItemId))
            || a.BonusOutputs.Any(o => BindingLegal(o.ItemId))).ToList();
        Assert.NotEmpty(bindingActions);
        Assert.True(bindingActions.Min(a => a.RequiredLevel) <= 10,
            $"a binding-legal hide/fiber must be reachable early (best: level {bindingActions.Min(a => a.RequiredLevel)})");

        // And the Longsword's slots ask for exactly what that chain supplies.
        var longsword = forms.GetById("form.longsword");
        Assert.Contains("form:metal", longsword.Slots["edge"].RequiresTags);
        Assert.Contains(longsword.Slots["binding"].RequiresTags,
            t => t is "form:hide" or "form:fiber");
    }

    // ---- D29.3 — the essence source audit ------------------------------------------------------

    /// <summary>
    /// <b>D29.3, settled 2026-08-18: profession essence is active-only.</b> "Trace profession
    /// essence must never compete economically with Realm extraction" (the user's phrasing).
    ///
    /// <para>This used to be an <em>allowlist</em> of eleven grandfathered material ids, carried
    /// unresolved for three contexts with the argument "a level-45+ rung is not competing with
    /// extraction for the same player at the same time". Phase 10 broke that argument: two of the
    /// eleven were <b>guaranteed outputs</b> on passive-runnable rungs, so a 12-hour absence banked
    /// ~3,750 essence-bearing logs with no Realm exposure at all — and auto-repeat meant it no
    /// longer even stopped when nobody was looking.</para>
    ///
    /// <para><b>The rule that replaced it is structural, not a list.</b> An essence-bearing
    /// material may reach the player through a profession <em>only</em> as an opportunity payload,
    /// and <see cref="ActionResolver"/> rolls opportunities on the active path alone. So "you
    /// cannot bank essence while idle" is a fact about the code rather than eleven exceptions
    /// somebody has to keep arguing for — and a new essence faucet cannot be slipped in, because
    /// there is no list to add it to.</para>
    ///
    /// <para>The M6 loot path is held to the same standard, from the other side, by
    /// <c>LootEcosystemTests.NoProfessionDropTableReachesEssence</c> — a drop table may not reach
    /// essence at all, since a table is rolled by both paths.</para>
    /// </summary>
    [Fact]
    public void ProfessionEssenceIsReachableOnlyThroughTheActivePath()
    {
        var actions = TestPaths.LoadStore<ProfessionActionDefinition>("profession_actions").GetAll();
        var materials = TestPaths.LoadStore<MaterialDefinition>("materials");
        var lootTables = TestPaths.LoadStore<Dungeons.Loot.LootTableDefinition>("loot_tables");

        bool BearsEssence(string id) =>
            materials.TryGetById(id, out var material) && material.Essence.Count > 0;

        foreach (var action in actions)
        {
            // The passive surface: what a completion hands over whether anyone is watching or
            // not — including the drop table, which reaches further than the action's own JSON
            // because tables nest.
            var passiveYield = action.Outputs.Select(output => output.ItemId)
                .Concat(action.BonusOutputs.Select(bonus => bonus.ItemId))
                .Concat(action.LootTableId is { Length: > 0 } table
                    ? Dungeons.Loot.LootReachability.ItemsReachableFrom(lootTables, table)
                    : Enumerable.Empty<string>());

            foreach (var itemId in passiveYield)
                Assert.False(BearsEssence(itemId),
                    $"{action.Id} yields essence-bearing '{itemId}' without being asked to. Essence may only " +
                    "reach a profession through an opportunity payload, which passive and offline cannot roll (D29.3).");
        }

        // Stated positively, so the rule cannot be satisfied by removing essence from professions
        // altogether — the active path is supposed to be able to find some.
        var activeEssence = actions
            .SelectMany(action => action.Opportunities)
            .SelectMany(opportunity => opportunity.Outputs.Select(output => output.ItemId)
                .Concat(opportunity.BonusOutputs.Select(bonus => bonus.ItemId)))
            .Where(BearsEssence)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.NotEmpty(activeEssence);
    }

    /// <summary>
    /// The other half of the same fence: an essence faucet may be deep, but it must not be
    /// <em>early</em>. Everything past the two originally audited eel rungs sits at level 30 or
    /// better, so no first-session player can farm trace essence instead of extracting for it.
    /// </summary>
    [Fact]
    public void NewEssenceFaucetsSitBehindDeepLevelGates()
    {
        var actions = TestPaths.LoadStore<ProfessionActionDefinition>("profession_actions").GetAll();
        var materials = TestPaths.LoadStore<MaterialDefinition>("materials");

        const int deepGate = 30;
        var originallyAudited = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "material.eel_skin",
            "material.shock_eel_gland",
        };

        bool CarriesEssence(string id) =>
            materials.TryGetById(id, out var material)
            && material.Essence.Count > 0
            && !originallyAudited.Contains(id);

        var early = actions
            .Where(action => action.RequiredLevel < deepGate)
            .Where(action => action.Outputs.Select(o => o.ItemId)
                .Concat(action.BonusOutputs.Select(o => o.ItemId))
                .Concat(action.Opportunities.SelectMany(op => op.Outputs.Select(o => o.ItemId)))
                .Concat(action.Opportunities.SelectMany(op => op.BonusOutputs.Select(o => o.ItemId)))
                .Any(CarriesEssence))
            .Select(action => $"{action.Id} (level {action.RequiredLevel})")
            .ToList();

        Assert.True(early.Count == 0,
            "essence faucets must sit at level " + deepGate + " or deeper: " + string.Join(", ", early));
    }
}
