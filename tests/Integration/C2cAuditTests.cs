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

    /// <summary>"Trace profession essence must never compete economically with Realm
    /// extraction" (D29.3, user's phrasing). This pins the overlap between essence-authored
    /// materials and profession outputs to the audited allowlist — today exactly one, the
    /// shock-eel skin. Whether that faucet needs a rarity gate is a C2c playtest question;
    /// a NEW overlap appearing silently is a content bug this test catches.</summary>
    [Fact]
    public void ProfessionFaucetsYieldEssenceOnlyFromTheAuditedAllowlist()
    {
        var actions = TestPaths.LoadStore<ProfessionActionDefinition>("profession_actions").GetAll();
        var materials = TestPaths.LoadStore<MaterialDefinition>("materials");

        // The complete audited list. Anything new appearing here must be argued through D29.3,
        // not slipped in — which is exactly what this test caught when the 20-profession pass
        // landed, and why the entries below carry their level gate.
        //
        // 2026-08-16, the original two: the shock-eel fishing rung, skin + gland — both
        // storm-trace, both flagged for C2c's economic-noncompete check.
        //
        // The 20-profession pass added nine more, every one of them behind a deep gate. The
        // argument is that a level-45-plus rung is not competing with Realm extraction for the
        // same player at the same time: a miner who can work an emberite seam has already been
        // extracting for a long while. All nine are still provisional and belong to the same
        // C2c noncompete check as the eel rung.
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "material.eel_skin",            // Fishing 20 — the original audited rung
            "material.shock_eel_gland",     // Fishing 20 — ditto, as a bonus output
            "material.static_charge",       // Fishing 34, 12% bonus off storm kelp
            "material.cinder_shard",        // Mining 45, 18% bonus off emberite
            "material.rime_shard",          // Mining 45, 18% bonus off frostiron
            "material.ember_core",          // Mining 45, 20% inside a 45%-risk opportunity
            "material.emberwood_log",       // Forestry 50
            "material.emberbark",           // Forestry 50, 25% bonus
            "material.livingbark_log",      // Forestry 62
            "material.spiritwood_log",      // Forestry 62, inside a 25%-risk opportunity
            "material.soul_gem",            // Thieving 58, 10% bonus and an opportunity payout
        };

        // Opportunity payloads are profession faucets too — the whole point of one is that it
        // pays better than the action that surfaced it, so leaving them out of this audit would
        // have been a hole big enough to drive the entire active path through.
        //
        // M6 added a third faucet: an action's `loot_table`, which reaches further than the
        // action's own JSON because tables nest. Walking it keeps this audit honest about the
        // whole surface. (The loot path is additionally held to a ZERO-tolerance rule by
        // LootEcosystemTests.NoProfessionDropTableReachesEssence — the allowlist above is a
        // legacy of content that predates it, and the new path starts clean rather than
        // inheriting eleven exceptions.)
        var lootTables = TestPaths.LoadStore<Dungeons.Loot.LootTableDefinition>("loot_tables");

        var essenceOutputs = actions
            .SelectMany(a => a.Outputs.Select(o => o.ItemId)
                .Concat(a.BonusOutputs.Select(o => o.ItemId))
                .Concat(a.Opportunities.SelectMany(op => op.Outputs.Select(o => o.ItemId)))
                .Concat(a.Opportunities.SelectMany(op => op.BonusOutputs.Select(o => o.ItemId)))
                .Concat(a.LootTableId is { Length: > 0 } table
                    ? Dungeons.Loot.LootReachability.ItemsReachableFrom(lootTables, table)
                    : Enumerable.Empty<string>()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(id => materials.TryGetById(id, out var m) && m.Essence.Count > 0)
            .ToList();

        foreach (var id in essenceOutputs)
            Assert.Contains(id, allowed);
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
