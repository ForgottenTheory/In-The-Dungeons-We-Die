using Dungeons.Characters;
using Dungeons.Characters.Composition;
using Dungeons.Combat;
using Dungeons.Crafting;
using Dungeons.Events;
using Dungeons.Items;
using Dungeons.Professions;
using Dungeons.Realms;

namespace Dungeons.Content;

/// <summary>
/// Validates cross-references and well-formedness across the loaded <see cref="ContentBundle"/>,
/// at load time rather than only in tests. Every "this id points at that store"
/// relationship in the shipped JSON is checked once, up front, so a mistyped or
/// missing reference fails loudly at startup instead of throwing a
/// <see cref="KeyNotFoundException"/> mid-play (see DECISIONS.md D5, ROADMAP Phase 4).
///
/// Character-component <c>abilityIds</c> are validated against a known-unimplemented
/// allowlist (<see cref="KnownUnimplementedAbilities"/>) so real typos fail while designed
/// placeholders pass; component <c>ruleIds</c> are validated by the
/// <see cref="CharacterComposer"/> path (they resolve against code-supplied handlers).
/// </summary>
public static class ContentValidator
{
    /// <summary>Valid property range for material/equipment profiles (docs/itemization.md §2).</summary>
    public const double MinPropertyValue = 0.0;
    public const double MaxPropertyValue = 100.0;

    /// <summary>Valid range for the optional authored material strength/workability overrides
    /// (docs/emergent-item-system.md §6). Workability 0 means destroyed, so it is not authorable.</summary>
    public const int MinMaterialStrength = 1;
    public const int MaxMaterialStrength = 100;
    public const int MinWorkability = 1;
    public const int MaxWorkability = 100;

    /// <summary>Valid ranges for a <see cref="CraftingActionDefinition"/> (docs/emergent-item-system.md §7).
    /// A channel rate above 1 would mean a single step overshoots the reagent it is converging
    /// toward, which §8.2 exists to make impossible.</summary>
    public const double MinSeverity = 0.0;
    public const double MaxSeverity = 1.0;
    public const double MaxChannelRate = 1.0;
    public const double RoleWeightTolerance = 0.001;

    /// <summary>How far a form's slot mass shares may drift from 1.0 before it is a bug rather
    /// than floating-point noise (docs/crafting-overview.md, forms).</summary>
    public const double MassShareTolerance = 0.001;

    /// <summary>Identity-system ranges (docs/identity-foundation.md §10–§11, D45/D46 — all
    /// provisional until play). Capacity is the stable slot count; rank maps to the four
    /// effect-family access rungs; base stats are the four visible physical stats on 0–10.</summary>
    public const int MinCapacity = 1;
    public const int MaxCapacity = 4;
    public const int MinIdentityRank = 1;
    public const int MaxIdentityRank = 4;
    public const int MinBaseStat = 0;
    public const int MaxBaseStat = 10;

    /// <summary>The roster's cluster vocabulary (docs/identity-foundation.md §3, D44) — a
    /// closed set per D16: clusters group the roster for tooling and docs, and a typo'd
    /// cluster should fail loudly rather than create an eighth group.</summary>
    public static readonly IReadOnlySet<string> IdentityClusters =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "physical", "precision", "sustain", "elemental", "magical", "occult", "fortune", "meta",
        };

    /// <summary>Generic tier words the name grammar forbids (docs/emergent-item-system.md §13.1) —
    /// intensity is expressed through vocabulary, not adjectives-of-adjectives.</summary>
    public static readonly IReadOnlySet<string> ForbiddenNameWords =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Greater", "Lesser", "Superior", "Inferior", "Grand", "Minor", "Major",
            "Advanced", "Basic", "Improved", "Enhanced", "Supreme", "Ultimate",
        };

    // E4 deleted both stale allowlists this class used to carry: `KnownUnimplementedAbilities`
    // (its two ids existed nowhere once the Bases stopped declaring abilityIds) and
    // `KnownUnimplementedStatuses` (`status.recalled_move` is authorable now that moves exist).
    // A reference that does not resolve is a real typo again, everywhere.

    /// <summary>
    /// Returns every problem found in the bundle (empty when the content is well-formed).
    /// Never throws for content problems — the caller decides whether to log, warn, or throw
    /// <see cref="ContentValidationException"/>.
    /// </summary>
    public static IReadOnlyList<ContentProblem> Validate(ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(content);

        // The property registry is the single source of truth for valid property names.
        var knownProperties = new HashSet<string>(
            content.Properties.GetAll().Select(p => p.Id), StringComparer.OrdinalIgnoreCase);

        var problems = new List<ContentProblem>();

        ValidateMaterials(content.Materials, knownProperties, problems);
        ValidateMaterialTags(content.Materials, problems);
        ValidateIdentityRegistry(content.Identities, problems);
        ValidateSignatureVocabulary(content.SignatureTriggers, content.SignatureBehaviors,
            content.SignatureThemes, problems);
        ValidateMaterialIdentityFields(content, problems);
        ValidateVerbActions(content, problems);
        ValidateProcesses(content.CraftingActions, content.Properties, content.Professions, problems);
        ValidateByproducts(content.Byproducts, content.Materials, problems);
        ValidateTraits(content.Traits, knownProperties, problems);
        ValidateEssences(content.Essences, content.Materials, knownProperties, problems);
        ValidateForms(content.Forms, content.Moves, content.Materials, knownProperties, problems);
        ValidateNameGrammar(content.NameGrammar, content.Properties, problems);
        ValidateModifierKeys(content.ModifierKeys, problems);
        ValidateBases(content.Classes, content.ModifierKeys, problems);
        ValidatePrefixes(content.Prefixes, content.Classes, content.ModifierKeys, problems);
        ValidateSuffixes(content.Suffixes, content.ModifierKeys, problems);
        ValidateActors(content.Actors, content.Moves, content.LootTables,
            content.EnemyFamilies, content.EnemyRoles, content.AiProfiles, problems);
        ValidateMoves(content, problems);
        ValidateProfessionActions(content.Actions, content.Professions, content.Materials, content.Realms, content.LootTables, problems);
        ValidateTrainingObstacles(content.TrainingObstacles, problems);
        ValidateMasteryLadder(content.MasteryBenefits, content.Professions, problems);
        ValidateProfessionSynergies(content.Synergies, content.Professions, problems);
        ValidateAutoCombatProfiles(content.AutoCombatProfiles, content.Moves, problems);
        ValidateStations(content, problems);
        ValidateInteractions(content.Interactions, content.Materials, content.Consumables, content.Professions, problems);
        ValidateRealms(content.Realms, content.Actors, content.Actions, content.LootTables, problems);
        ValidateLootTables(content, problems);
        ValidateEquipment(content.Equipment, content.Moves, content.MoveModifiers, knownProperties, problems);
        ValidateStatuses(content, problems);
        ValidateComponentMoves(content, problems);
        ValidateAffixes(content, knownProperties, problems);

        return problems;
    }

    /// <summary>The identity roster (docs/identity-foundation.md §3, D44): ids carry the
    /// <c>identity.</c> prefix, every entry is named and described, and clusters come from
    /// the closed set. The roster's exact membership is pinned by test, not here — the
    /// validator checks shape, the test checks intent.</summary>
    private static void ValidateIdentityRegistry(DataStore<IdentityDefinition> identities, List<ContentProblem> problems)
    {
        foreach (var identity in identities.GetAll())
        {
            void Problem(string message) => problems.Add(new ContentProblem("identity", $"{identity.Id}: {message}"));

            if (!identity.Id.StartsWith("identity.", StringComparison.Ordinal))
                Problem("id must start with 'identity.'.");
            if (string.IsNullOrWhiteSpace(identity.Name))
                Problem("needs a name — identities are player-facing by design (D42).");
            if (string.IsNullOrWhiteSpace(identity.Description))
                Problem("needs its one-sentence boundary description.");
            if (!IdentityClusters.Contains(identity.Cluster))
                Problem($"cluster '{identity.Cluster}' is not one of the roster clusters ({string.Join(", ", IdentityClusters)}).");
        }
    }

    /// <summary>The Signature grammar's D30 fence (docs/identity-foundation.md §7): a trigger
    /// binds to a real <see cref="GameEvents"/> event or is the one standing shape — never
    /// both, never neither — and vocabulary ids are bare keys like property names, so
    /// authored profiles read as designed (<c>"favored_triggers": ["on_block"]</c>).</summary>
    private static void ValidateSignatureVocabulary(
        DataStore<SignatureTriggerDefinition> triggers,
        DataStore<SignatureBehaviorDefinition> behaviors,
        DataStore<SignatureThemeDefinition> themes,
        List<ContentProblem> problems)
    {
        static bool IsBareKey(string id) => !string.IsNullOrWhiteSpace(id) && !id.Contains('.');

        foreach (var trigger in triggers.GetAll())
        {
            void Problem(string message) => problems.Add(new ContentProblem("signature_trigger", $"{trigger.Id}: {message}"));

            if (!IsBareKey(trigger.Id))
                Problem("trigger ids are bare keys (on_block), never dotted entity ids.");
            var hasEvent = !string.IsNullOrWhiteSpace(trigger.Event);
            if (hasEvent == trigger.Standing)
                Problem("must declare exactly one of 'event' or 'standing: true'.");
            if (hasEvent && !GameEvents.All.Contains(trigger.Event!))
                Problem($"event '{trigger.Event}' is not a published game event — a trigger may only bind to machinery that resolves (D30).");
            if (string.IsNullOrWhiteSpace(trigger.Name))
                Problem("needs a name.");
        }

        foreach (var behavior in behaviors.GetAll())
        {
            void Problem(string message) => problems.Add(new ContentProblem("signature_behavior", $"{behavior.Id}: {message}"));

            if (!IsBareKey(behavior.Id))
                Problem("behavior ids are bare keys (store), never dotted entity ids.");
            if (string.IsNullOrWhiteSpace(behavior.Name) || string.IsNullOrWhiteSpace(behavior.Description))
                Problem("needs a name and a one-sentence description of what its assembler composes.");
        }

        foreach (var theme in themes.GetAll())
        {
            void Problem(string message) => problems.Add(new ContentProblem("signature_theme", $"{theme.Id}: {message}"));

            if (!IsBareKey(theme.Id))
                Problem("theme ids are bare keys (renewal), never dotted entity ids.");
            if (string.IsNullOrWhiteSpace(theme.Name))
                Problem("needs a name (authoring tools only — themes are never player-facing, §6.1).");
        }
    }

    /// <summary>The identity fields a material may author (docs/identity-foundation.md
    /// §10–§11): capacity and ranks in range, every identity/latent/profile reference
    /// resolving, no duplicates, and no identity both active and latent — each rule the
    /// exact typo class that would otherwise fail silently at generation time.</summary>
    private static void ValidateMaterialIdentityFields(ContentBundle content, List<ContentProblem> problems)
    {
        foreach (var material in content.Materials.GetAll())
        {
            void Problem(string message) => problems.Add(new ContentProblem("material_identity", $"{material.Id}: {message}"));

            if (material.Capacity is int capacity && (capacity < MinCapacity || capacity > MaxCapacity))
                Problem($"capacity {capacity} is outside {MinCapacity}–{MaxCapacity}.");

            var activeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var grant in material.Identities)
            {
                if (!content.Identities.Contains(grant.Id))
                    Problem($"identity '{grant.Id}' is not in the identity registry.");
                if (grant.Rank < MinIdentityRank || grant.Rank > MaxIdentityRank)
                    Problem($"identity '{grant.Id}' rank {grant.Rank} is outside {MinIdentityRank}–{MaxIdentityRank}.");
                if (!activeIds.Add(grant.Id))
                    Problem($"identity '{grant.Id}' is granted twice.");
            }

            if (material.Capacity is int slots && activeIds.Count > slots)
                Problem($"authors {activeIds.Count} active identities over a capacity of {slots} — authored materials start Stable; overfill is play, not authoring (§10.3).");

            var latentIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var latent in material.Latent)
            {
                if (!content.Identities.Contains(latent))
                    Problem($"latent identity '{latent}' is not in the identity registry.");
                if (!latentIds.Add(latent))
                    Problem($"latent identity '{latent}' is listed twice.");
                if (activeIds.Contains(latent))
                    Problem($"identity '{latent}' is both active and latent — latent means present but inactive (§10.2).");
            }

            if (material.Base is { } baseStats)
            {
                void CheckStat(string name, int value)
                {
                    if (value < MinBaseStat || value > MaxBaseStat)
                        Problem($"base stat {name} = {value} is outside {MinBaseStat}–{MaxBaseStat}.");
                }
                CheckStat("heft", baseStats.Heft);
                CheckStat("bite", baseStats.Bite);
                CheckStat("toughness", baseStats.Toughness);
                CheckStat("give", baseStats.Give);
            }

            if (material.SignatureProfile is { } profile)
            {
                foreach (var theme in profile.Themes)
                    if (!content.SignatureThemes.Contains(theme))
                        Problem($"profile theme '{theme}' is not in the theme registry.");
                foreach (var trigger in profile.FavoredTriggers)
                    if (!content.SignatureTriggers.Contains(trigger))
                        Problem($"profile favored trigger '{trigger}' is not in the trigger registry.");
                foreach (var behavior in profile.FavoredBehaviors)
                    if (!content.SignatureBehaviors.Contains(behavior))
                        Problem($"profile favored behavior '{behavior}' is not in the behavior registry.");
            }
        }
    }

    /// <summary>The identity-targeting verbs — the only ones an <c>identity_scope</c> means
    /// anything on (Runecrafting's lever, D48). Scoping Refine would be a silent no-op, so it
    /// is a load error instead.</summary>
    private static readonly IReadOnlySet<Dungeons.Crafting.Identity.CraftVerb> IdentityTargetingVerbs =
        new HashSet<Dungeons.Crafting.Identity.CraftVerb>
        {
            Dungeons.Crafting.Identity.CraftVerb.Reveal,
            Dungeons.Crafting.Identity.CraftVerb.Transfer,
            Dungeons.Crafting.Identity.CraftVerb.Develop,
            Dungeons.Crafting.Identity.CraftVerb.Extract,
            Dungeons.Crafting.Identity.CraftVerb.Displace,
        };

    /// <summary>Verb actions (docs/transformation-verbs.md §1, D47/D48): gates resolve,
    /// scopes only appear where they mean something, Process names a migrated output, and
    /// every cost exists — each rule the typo class that would otherwise fail at the bench.</summary>
    private static void ValidateVerbActions(ContentBundle content, List<ContentProblem> problems)
    {
        foreach (var action in content.VerbActions.GetAll())
        {
            void Problem(string message) => problems.Add(new ContentProblem("verb_actions", $"{action.Id}: {message}"));

            if (!action.Id.StartsWith("craft.", StringComparison.Ordinal))
                Problem("id must start with 'craft.'.");
            if (string.IsNullOrWhiteSpace(action.Name))
                Problem("needs a fiction name — the verb is the executor, the name is what the player clicks.");
            if (action.RequiredLevel < 0)
                Problem($"required_level {action.RequiredLevel} is negative.");
            if (!string.IsNullOrEmpty(action.Profession) && !content.Professions.Contains(action.Profession))
                Problem($"gates on unknown profession '{action.Profession}'.");

            foreach (var tag in action.SubstrateTags)
                if (!TagFamilies.TryParse(tag, out _, out _))
                    Problem($"substrate tag '{tag}' is not a family:value tag.");
            if (action.SubstrateTags.Count > 0
                && content.Materials.Count > 0
                && !content.Materials.GetAll().Any(material =>
                    action.SubstrateTags.Any(tag => material.Tags.Contains(tag, StringComparer.Ordinal))))
            {
                Problem("no material satisfies its substrate gate — an action nobody can ever run.");
            }

            foreach (var identityId in action.IdentityScope)
                if (!content.Identities.Contains(identityId))
                    Problem($"identity scope names unknown identity '{identityId}'.");
            if (action.IdentityScope.Count > 0 && !IdentityTargetingVerbs.Contains(action.Verb))
                Problem($"declares an identity scope on {action.Verb}, which targets no identity — the scope would be a silent no-op.");

            if (action.Verb == Dungeons.Crafting.Identity.CraftVerb.Process)
            {
                if (string.IsNullOrWhiteSpace(action.Output))
                    Problem("Process actions must name their output definition.");
                else if (!content.Materials.TryGetById(action.Output, out var output))
                    Problem($"output '{action.Output}' is not a known material.");
                else if (output.Capacity is null)
                    Problem($"output '{action.Output}' has not been migrated to the identity model — processing into it would strand the carried state.");
            }
            else if (!string.IsNullOrWhiteSpace(action.Output))
            {
                Problem($"declares an output but its verb is {action.Verb} — only Process converts substance.");
            }

            foreach (var cost in action.ExtraCosts)
                if (!content.Materials.Contains(cost.ItemId))
                    Problem($"extra cost '{cost.ItemId}' is not a known material.");
        }
    }

    /// <summary>The docs/affixes.md §8 rules R4b can enforce at load: every grant resolves,
    /// tiers are monotonic with sane ranges, $roll parity holds, ids avoid the D-17 collision,
    /// slots/classes are known, and availability names real properties. (Reachability and the
    /// distribution guarantees live in the seeded test suite; family-≥2 waits for catalog
    /// breadth.)</summary>
    private static void ValidateAffixes(ContentBundle content, HashSet<string> knownProperties, List<ContentProblem> problems)
    {
        var slots = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "prefix", "suffix", "innate" };
        var classes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "standard", "trigger", "innate", "exotic", "signature", "anomalous" };

        foreach (var affix in content.Affixes.GetAll())
        {
            void Problem(string message) => problems.Add(new ContentProblem("affix", $"{affix.Id}: {message}"));

            if (affix.Id.StartsWith("prefix.", StringComparison.Ordinal)
                || affix.Id.StartsWith("suffix.", StringComparison.Ordinal))
                Problem("affix ids must not start 'prefix.'/'suffix.' (D-17 naming collision)");

            if (!slots.Contains(affix.Slot))
                Problem($"unknown slot '{affix.Slot}'");
            if (!classes.Contains(affix.Class))
                Problem($"unknown class '{affix.Class}'");
            if (string.IsNullOrWhiteSpace(affix.Family))
                Problem("family is required — it is the anti-stacking unit (§3.5)");

            foreach (var requirement in affix.Availability.Requires)
            {
                if (!knownProperties.Contains(requirement.Property))
                    Problem($"availability names unknown property '{requirement.Property}'");
            }

            foreach (var scale in affix.ChanceWeight.Scale)
            {
                if (scale.Property is { Length: > 0 } p && !knownProperties.Contains(p))
                    Problem($"weight scales on unknown property '{p}'");
            }

            if (affix.Tiers.Count == 0)
            {
                Problem("no tiers — nothing can ever roll");
            }
            else
            {
                var ordered = affix.Tiers.OrderBy(t => t.Tier).ToList();
                for (var i = 0; i < ordered.Count; i++)
                {
                    if (ordered[i].Range.Count != 2)
                        Problem($"tier {ordered[i].Tier} range must be [lo, hi]");

                    foreach (var key in ordered[i].Requires.Keys)
                    {
                        var bare = key.StartsWith("essence.", StringComparison.OrdinalIgnoreCase)
                            ? null
                            : key;
                        if (bare is not null && !knownProperties.Contains(bare))
                            Problem($"tier {ordered[i].Tier} requires unknown property '{key}'");
                    }
                }
            }

            var hasRollGrant = false;
            foreach (var grant in affix.Grants)
            {
                switch (grant.Type.ToLowerInvariant())
                {
                    case "stat":
                        if (!content.ModifierKeys.Contains(grant.Key))
                            Problem($"stat grant targets unknown modifier key '{grant.Key}'");
                        if (string.Equals(grant.Value, "$roll", StringComparison.OrdinalIgnoreCase))
                            hasRollGrant = true;
                        else if (!double.TryParse(grant.Value, System.Globalization.NumberStyles.Float,
                                     System.Globalization.CultureInfo.InvariantCulture, out _))
                            Problem($"stat grant value '{grant.Value}' is neither $roll nor a number");
                        break;

                    case "rule":
                        if (grant.Rule is null)
                            Problem("rule grant carries no rule");
                        else if (string.IsNullOrWhiteSpace(grant.Rule.Event))
                            Problem($"rule grant '{grant.Rule.Id}' names no event");
                        if (grant.RollInto is "chance" or "amount")
                            hasRollGrant = true;
                        break;

                    case "movemodifier":
                        if (!content.MoveModifiers.Contains(grant.Key))
                            Problem($"moveModifier grant targets unknown modifier '{grant.Key}'");
                        break;

                    default:
                        Problem($"unknown grant type '{grant.Type}'");
                        break;
                }
            }

            if (affix.Grants.Count == 0)
                Problem("no grants — the modifier would do nothing when equipped (D30)");

            // §8's parity rule: the tooltip and the mechanics may never drift.
            var describesRoll = affix.Description.Contains("$roll", StringComparison.Ordinal);
            if (hasRollGrant && !describesRoll)
                Problem("$roll is granted but never described — silent tooltip drift");
            if (!hasRollGrant && describesRoll)
                Problem("$roll is described but never granted — the tooltip lies");
        }
    }

    private static void ValidateMaterials(
        DataStore<MaterialDefinition> materials,
        IReadOnlySet<string> knownProperties,
        List<ContentProblem> problems)
    {
        foreach (var material in materials.GetAll())
        {
            foreach (var (property, value) in material.Properties)
            {
                if (!knownProperties.Contains(property))
                    problems.Add(new("materials", $"{material.Id} has unknown property '{property}' (typo, or add it to game/data/properties/)."));

                if (value < MinPropertyValue || value > MaxPropertyValue)
                    problems.Add(new("materials", $"{material.Id} property '{property}' = {value:0.##} is outside the {MinPropertyValue:0}–{MaxPropertyValue:0} range."));
            }

            // Optional emergent-system overrides (docs/emergent-item-system.md §6); normally
            // unset, so a value out of range is a typo rather than a deliberate choice.
            if (material.MaterialStrength is { } materialStrength && (materialStrength < MinMaterialStrength || materialStrength > MaxMaterialStrength))
                problems.Add(new("materials", $"{material.Id} potency override {materialStrength} is outside the {MinMaterialStrength}–{MaxMaterialStrength} range."));

            if (material.Workability is { } workability && (workability < MinWorkability || workability > MaxWorkability))
                problems.Add(new("materials", $"{material.Id} integrity override {workability} is outside the {MinWorkability}–{MaxWorkability} range."));
        }
    }

    /// <summary>
    /// Validates crafting processes (docs/emergent-item-system.md §7). CraftingActions are the only
    /// authored content the reaction engine needs, so a typo here silently changes the physics
    /// of the whole game rather than breaking one recipe — hence the thorough checks.
    /// </summary>
    private static void ValidateProcesses(
        DataStore<CraftingActionDefinition> processes,
        DataStore<PropertyDefinition> properties,
        DataStore<ProfessionDefinition> professions,
        List<ContentProblem> problems)
    {
        foreach (var craftingAction in processes.GetAll())
        {
            if (!craftingAction.IsUngated && !professions.Contains(craftingAction.Profession))
                problems.Add(new("processes", $"{craftingAction.Id} requires unknown profession '{craftingAction.Profession}'."));

            if (craftingAction.Severity is < MinSeverity or > MaxSeverity)
                problems.Add(new("processes", $"{craftingAction.Id} severity {craftingAction.Severity:0.##} is outside the {MinSeverity:0}–{MaxSeverity:0} range."));

            if (craftingAction.EssenceRate is < 0.0 or > 1.0)
                problems.Add(new("processes", $"{craftingAction.Id} essence_rate {craftingAction.EssenceRate:0.##} is outside the 0–1 range."));

            ValidateRoleWeights(craftingAction, problems);
            ValidateChannel(craftingAction, properties, problems);
            ValidateProcessTagEffects(craftingAction, problems);

            foreach (var tag in craftingAction.Requires.SubstrateTags)
                ValidateProcessTag(craftingAction, tag, "requires.substrate_tags", allowWildcard: false, problems);

            if (craftingAction.Requires.ProfessionLevel < 0)
                problems.Add(new("processes", $"{craftingAction.Id} requires a negative profession level."));

            if (craftingAction.IsUngated && craftingAction.Requires.ProfessionLevel > 0)
                problems.Add(new("processes", $"{craftingAction.Id} is ungated but requires profession level {craftingAction.Requires.ProfessionLevel}, which can never be met."));
        }
    }

    /// <summary>
    /// Validates the destruction byproduct table (docs/emergent-item-system.md §6.2c). The
    /// table must be <b>total</b> — every destroyed material has to leave something — and
    /// unambiguous, since a form covered twice would make the outcome depend on load order.
    /// </summary>
    private static void ValidateByproducts(
        DataStore<ByproductDefinition> byproducts,
        DataStore<MaterialDefinition> materials,
        List<ContentProblem> problems)
    {
        if (byproducts.Count == 0)
            return;

        var coveredForms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var byproduct in byproducts.GetAll())
        {
            if (!materials.Contains(byproduct.Material))
                problems.Add(new("byproducts", $"{byproduct.Id} produces unknown material '{byproduct.Material}'."));

            if (byproduct.Forms.Count == 0 && !byproduct.Fallback)
                problems.Add(new("byproducts", $"{byproduct.Id} covers no forms and is not the fallback, so it can never be produced."));

            foreach (var form in byproduct.Forms)
            {
                if (coveredForms.TryGetValue(form, out var owner))
                    problems.Add(new("byproducts", $"{byproduct.Id} and {owner} both cover form '{form}'."));
                else
                    coveredForms[form] = byproduct.Id;
            }
        }

        var fallbacks = byproducts.GetAll().Count(b => b.Fallback);
        if (fallbacks != 1)
            problems.Add(new("byproducts", $"expected exactly one fallback byproduct, found {fallbacks} — destruction must always yield something."));

        // Every form the material library actually uses should map somewhere explicit; the
        // fallback exists for emergent forms, not as cover for an unfinished table.
        var authoredForms = materials.GetAll()
            .SelectMany(m => m.Tags)
            .Where(t => TagFamilies.TryParse(t, out var family, out _) && family == TagFamilies.Form.Name)
            .Select(t => t[(t.IndexOf(':') + 1)..])
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var form in authoredForms.Where(f => !coveredForms.ContainsKey(f)))
            problems.Add(new("byproducts", $"form '{form}' is used by the material library but no byproduct covers it."));
    }

    /// <summary>
    /// Validates the name grammar (docs/emergent-item-system.md §13). The grammar's hard
    /// constraints are what keep generated names from reading like loot-generator spam, so
    /// they are enforced on the vocabulary rather than trusted to whoever authors it.
    /// </summary>
    private static void ValidateNameGrammar(
        DataStore<NameWordDefinition> grammar,
        DataStore<PropertyDefinition> properties,
        List<ContentProblem> problems)
    {
        foreach (var entry in grammar.GetAll())
        {
            if (!entry.HasKnownPrefix)
            {
                problems.Add(new("name_grammar", $"{entry.Id} must start with '{NameWordDefinition.IntensityPrefix}' or '{NameWordDefinition.FormPrefix}'."));
                continue;
            }

            if (entry.Words.Count == 0)
            {
                problems.Add(new("name_grammar", $"{entry.Id} supplies no words."));
                continue;
            }

            if (entry.Kind == NameWordKind.Intensity && !properties.Contains(entry.Key))
                problems.Add(new("name_grammar", $"{entry.Id} is a ladder for unknown property '{entry.Key}'."));

            foreach (var word in entry.Words)
            {
                if (string.IsNullOrWhiteSpace(word))
                    problems.Add(new("name_grammar", $"{entry.Id} contains a blank word."));

                // §13.1: numbers never appear in a name, and intensity is expressed through
                // vocabulary rather than adjectives-of-adjectives.
                if (word.Any(char.IsDigit))
                    problems.Add(new("name_grammar", $"{entry.Id} word '{word}' contains a number."));

                if (ForbiddenNameWords.Contains(word))
                    problems.Add(new("name_grammar", $"{entry.Id} word '{word}' is a tier word; use a stronger vocabulary word instead."));
            }
        }
    }

    /// <summary>
    /// Validates the modifier registry itself (docs/effect-foundation.md §4.1–4.2, D-12).
    ///
    /// <para>Two rules, both closing a failure that only shows up long after load. A
    /// <c>scoped_by</c> naming a dimension nothing supplies is a key nothing can ever resolve;
    /// a <c>danger</c> family without a ceiling is unbounded avoidance, which the design cannot
    /// recover from once it is in players' hands.</para>
    /// </summary>
    private static void ValidateModifierKeys(
        DataStore<Modifiers.ModifierKeyDefinition> modifierKeys,
        List<ContentProblem> problems)
    {
        foreach (var key in modifierKeys.GetAll())
        {
            if (key.IsScoped && !Modifiers.ScopeDimensions.IsKnown(key.ScopedBy))
            {
                problems.Add(new("modifier_keys",
                    $"{key.Id} is scoped by unknown dimension '{key.ScopedBy}'. Valid: {string.Join(", ", Modifiers.ScopeDimensions.All)}."));
            }

            // The cap is the whole point of marking a family dangerous. Leaving `max` off is how
            // "98.5% is close enough to immunity" becomes 100%.
            if (key.Danger && key.Max is null)
            {
                problems.Add(new("modifier_keys",
                    $"{key.Id} is marked danger and has no max. A dangerous family without a ceiling cannot be balanced after the fact."));
            }
        }
    }

    /// <summary>
    /// Validates the Base roster (docs/classes.md §3).
    ///
    /// <para>The load-bearing rule is the <b>growth budget</b>: every Base must distribute the
    /// same total per level. If one Base could exceed it, Base choice would stop being a trade
    /// and start being a menu where some options are strictly larger.</para>
    /// </summary>
    private static void ValidateBases(
        DataStore<BaseClassDefinition> bases,
        DataStore<Modifiers.ModifierKeyDefinition> modifierKeys,
        List<ContentProblem> problems)
    {
        var attributes = Enum.GetNames<AttributeType>().ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var @base in bases.GetAll())
        {
            var listed = 0.0;

            foreach (var (attribute, weight) in @base.Growth)
            {
                if (!attributes.Contains(attribute))
                    problems.Add(new("classes", $"{@base.Id} grows unknown attribute '{attribute}'."));

                if (weight < 0)
                    problems.Add(new("classes", $"{@base.Id} has negative growth for '{attribute}'."));

                listed += weight;
            }

            if (listed > AttributeGrowth.BudgetPerLevel + 0.001)
            {
                problems.Add(new("classes",
                    $"{@base.Id} growth weights total {listed:0.##}, over the {AttributeGrowth.BudgetPerLevel:0.##} budget."));
            }

            if (@base.Growth.Count == 0)
                problems.Add(new("classes", $"{@base.Id} declares no growth, so it has no identity."));

            if (string.IsNullOrWhiteSpace(@base.Engine))
                problems.Add(new("classes", $"{@base.Id} has no engine description — a Base is distinguished by its engine."));

            ValidateGauge(@base, modifierKeys, problems);
        }
    }

    /// <summary>
    /// Validates the Prefix roster (docs/classes.md §4).
    ///
    /// <para>The rule worth enforcing mechanically is that <b>a Prefix may never reference a
    /// Base</b>. That single constraint is what keeps the roster at 25 authored mechanics
    /// instead of 15 × 25 = 375 hand-written combinations, and it is exactly the kind of thing
    /// that erodes quietly when someone needs "just one special case".</para>
    /// </summary>
    private static void ValidatePrefixes(
        DataStore<PrefixDefinition> prefixes,
        DataStore<BaseClassDefinition> bases,
        DataStore<Modifiers.ModifierKeyDefinition> modifierKeys,
        List<ContentProblem> problems)
    {
        var baseIds = bases.GetAll().Select(b => b.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var prefix in prefixes.GetAll())
        {
            if (string.IsNullOrWhiteSpace(prefix.Mechanic))
                problems.Add(new("prefixes", $"{prefix.Id} states no mechanic — a Prefix adds one recognizable thing."));

            if (prefix.Rules.Count == 0 && prefix.Gauge is null && prefix.Modifiers.Count == 0)
                problems.Add(new("prefixes", $"{prefix.Id} does nothing at all."));

            foreach (var text in PrefixTextFields(prefix))
            {
                if (baseIds.Contains(text))
                    problems.Add(new("prefixes", $"{prefix.Id} references Base '{text}'. Prefixes must adapt through events, never by naming a Base."));
            }

            foreach (var rule in prefix.Rules)
                ValidateTriggerRule(rule, $"{prefix.Id} rule '{rule.Id}'", modifierKeys, problems);

            if (prefix.Gauge is { } gauge)
            {
                foreach (var feed in gauge.Feeds)
                    ValidateTriggerRule(feed, $"{prefix.Id} gauge feed '{feed.Id}'", modifierKeys, problems);

                foreach (var band in gauge.Bands.Where(b => !modifierKeys.Contains(b.Modifier)))
                    problems.Add(new("prefixes", $"{prefix.Id} gauge band references unknown modifier key '{band.Modifier}'."));
            }
        }
    }

    /// <summary>Known name-format styles (docs/classes.md). Presentation only — a Suffix's
    /// format must never influence its mechanics, so this is validated but never read by the
    /// rule engine.</summary>
    public static readonly IReadOnlySet<string> NameFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "standard", "citation", "investigation", "warning",
        "medical", "liability", "bureaucratic", "consequence", "notice",
    };

    /// <summary>
    /// Validates the Suffix roster (docs/classes.md §6).
    ///
    /// <para>The rule that matters: a Suffix with <i>any</i> expressions must have <b>one per
    /// channel</b>. A partially-expressed Suffix is worse than an unexpressed one — it looks
    /// usable, and then turns out to be meant for somebody else's build, which is precisely the
    /// failure the three-expression model exists to prevent.</para>
    /// </summary>
    private static void ValidateSuffixes(
        DataStore<SuffixDefinition> suffixes,
        DataStore<Modifiers.ModifierKeyDefinition> modifierKeys,
        List<ContentProblem> problems)
    {
        foreach (var suffix in suffixes.GetAll())
        {
            if (string.IsNullOrWhiteSpace(suffix.Fantasy))
                problems.Add(new("suffixes", $"{suffix.Id} states no fantasy — a Suffix is an idea before it is a mechanic."));

            if (!NameFormats.Contains(suffix.Format))
                problems.Add(new("suffixes", $"{suffix.Id} uses unknown name format '{suffix.Format}'."));

            if (suffix.Expressions.Count == 0)
                continue; // Roster entry awaiting design — legitimate, not an error.

            foreach (var channel in Enum.GetValues<ExpressionChannel>())
            {
                var matching = suffix.Expressions.Count(e => e.Channel == channel);

                if (matching == 0)
                    problems.Add(new("suffixes", $"{suffix.Id} has expressions but none for {channel} — that build could see it and not use it."));
                else if (matching > 1)
                    problems.Add(new("suffixes", $"{suffix.Id} has {matching} expressions for {channel}; exactly one is allowed."));
            }

            foreach (var expression in suffix.Expressions)
            {
                ValidateTriggerRule(
                    expression.Rule, $"{suffix.Id} {expression.Channel} expression", modifierKeys, problems);

                if (string.IsNullOrWhiteSpace(expression.Drawback))
                    problems.Add(new("suffixes", $"{suffix.Id} {expression.Channel} expression states no drawback."));
            }
        }
    }

    /// <summary>Every author-supplied string on a prefix that could smuggle in a Base id.</summary>
    private static IEnumerable<string> PrefixTextFields(PrefixDefinition prefix) =>
        prefix.Tags
            .Concat(prefix.Rules.SelectMany(r => r.Payload.Select(e => e.Text)))
            .Concat(prefix.Rules.SelectMany(r => r.When.Select(c => c.Text)))
            .Concat(prefix.Gauge?.Feeds.SelectMany(f => f.Payload.Select(e => e.Text)) ?? Enumerable.Empty<string>())
            .Where(t => !string.IsNullOrEmpty(t));

    private static void ValidateGauge(
        BaseClassDefinition @base,
        DataStore<Modifiers.ModifierKeyDefinition> modifierKeys,
        List<ContentProblem> problems)
    {
        if (@base.Gauge is not { } gauge)
            return; // Bases without a gauge are a deliberate design choice, not an omission.

        if (string.IsNullOrWhiteSpace(gauge.Name))
            problems.Add(new("classes", $"{@base.Id} has an unnamed gauge."));

        if (gauge.Max <= 0)
            problems.Add(new("classes", $"{@base.Id} gauge '{gauge.Name}' has a non-positive maximum."));

        foreach (var band in gauge.Bands)
        {
            if (!modifierKeys.Contains(band.Modifier))
                problems.Add(new("classes", $"{@base.Id} gauge band references unknown modifier key '{band.Modifier}'."));

            if (band.AtMost is { } atMost && atMost < band.AtLeast)
                problems.Add(new("classes", $"{@base.Id} gauge band has at_most below at_least."));
        }

        foreach (var feed in gauge.Feeds)
            ValidateTriggerRule(feed, $"{@base.Id} gauge feed", modifierKeys, problems);
    }

    /// <summary>
    /// Shared validation for any declarative hook — gauge feeds, prefix rules, suffix
    /// expressions. Everything a typo could hide behind is checked here: the event, the
    /// condition kinds, the effect kind, and any modifier key an effect names.
    /// </summary>
    internal static void ValidateTriggerRule(
        Dungeons.Rules.TriggerRule rule,
        string context,
        DataStore<Modifiers.ModifierKeyDefinition> modifierKeys,
        List<ContentProblem> problems)
    {
        if (!Dungeons.Events.GameEvents.All.Contains(rule.Event))
            problems.Add(new("rules", $"{context} listens for unknown event '{rule.Event}'."));

        foreach (var condition in rule.When)
        {
            if (!Dungeons.Rules.RuleVocabulary.Conditions.Contains(condition.Kind))
                problems.Add(new("rules", $"{context} uses unknown condition '{condition.Kind}'."));
        }

        // Payload, not Effect — a rule may author either the single `effect` or the multi
        // `effects[]` form, and checking only the first would leave the second unvalidated.
        if (rule.Payload.Count == 0)
            problems.Add(new("rules", $"{context} declares no effect."));

        foreach (var effect in rule.Payload)
        {
            if (!Dungeons.Rules.RuleVocabulary.Effects.Contains(effect.Kind))
                problems.Add(new("rules", $"{context} uses unknown effect '{effect.Kind}'."));

            if (Dungeons.Rules.RuleVocabulary.ModifierKeyed.Contains(effect.Kind)
                && !modifierKeys.Contains(effect.Text))
            {
                problems.Add(new("rules", $"{context} grants unknown modifier key '{effect.Text}'."));
            }
        }

        // Only Anomalous content — won from Overreach — may recurse past the default, and then
        // by exactly one. Anywhere else, this is someone reaching for a bigger number.
        if (rule.Proc.MaxDepth > Dungeons.Rules.ProcSafety.MaxDepth)
        {
            if (rule.Proc.MaxDepth > Dungeons.Rules.ProcSafety.AnomalousMaxDepth)
                problems.Add(new("rules",
                    $"{context} sets proc depth {rule.Proc.MaxDepth}; {Dungeons.Rules.ProcSafety.AnomalousMaxDepth} is the ceiling even for Anomalous affixes."));
            else if (!context.Contains("anomalous", StringComparison.OrdinalIgnoreCase))
                problems.Add(new("rules",
                    $"{context} raises proc depth above the default, which only Anomalous affixes may do."));
        }

        if (rule.Chance is < 0 or > 1)
            problems.Add(new("rules", $"{context} has a chance of {rule.Chance:0.##}, outside 0–1."));

        if (rule.CooldownTicks < 0)
            problems.Add(new("rules", $"{context} has a negative cooldown."));
    }

    private static void ValidateRoleWeights(CraftingActionDefinition craftingAction, List<ContentProblem> problems)
    {
        var weights = craftingAction.RoleWeights;

        if (weights.Substrate < 0.0 || weights.Reagent < 0.0 || weights.Catalyst < 0.0)
            problems.Add(new("processes", $"{craftingAction.Id} has a negative role weight."));

        // MaterialStrength is a weighted mean (§6.1); weights that don't sum to 1 would let a crafting action
        // inflate or deflate material strength for free, which is the exploit the mean exists to close.
        if (Math.Abs(weights.Total - 1.0) > RoleWeightTolerance)
            problems.Add(new("processes", $"{craftingAction.Id} role_weights sum to {weights.Total:0.###}, not 1.0."));
    }

    private static void ValidateChannel(
        CraftingActionDefinition craftingAction,
        DataStore<PropertyDefinition> properties,
        List<ContentProblem> problems)
    {
        if (craftingAction.AffectedQualities.Count == 0)
        {
            problems.Add(new("processes", $"{craftingAction.Id} declares no affected_qualities, so it could never change anything."));
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in craftingAction.AffectedQualities)
        {
            if (!seen.Add(entry.Property))
                problems.Add(new("processes", $"{craftingAction.Id} lists channel property '{entry.Property}' twice."));

            if (entry.Rate is <= 0.0 or > MaxChannelRate)
                problems.Add(new("processes", $"{craftingAction.Id} channel '{entry.Property}' rate {entry.Rate:0.##} is outside the 0–{MaxChannelRate:0} range (exclusive of 0)."));

            if (!properties.TryGetById(entry.Property, out var property))
            {
                problems.Add(new("processes", $"{craftingAction.Id} opens unknown property '{entry.Property}' (typo, or add it to game/data/properties/)."));
                continue;
            }

            // §2.3: Response properties are derived outputs and Sourcing describes how hard the
            // material was to obtain — neither may ever be a reaction input.
            if (property.Role is PropertyRole.Response or PropertyRole.Sourcing)
                problems.Add(new("processes", $"{craftingAction.Id} opens '{entry.Property}', which is a {property.Role} property and can never be a reaction input."));
        }
    }

    private static void ValidateProcessTagEffects(CraftingActionDefinition craftingAction, List<ContentProblem> problems)
    {
        foreach (var tag in craftingAction.TagEffects.Set)
            ValidateProcessTag(craftingAction, tag, "tag_effects.set", allowWildcard: false, problems);

        foreach (var tag in craftingAction.TagEffects.Clear)
            ValidateProcessTag(craftingAction, tag, "tag_effects.clear", allowWildcard: true, problems);

        // Setting and clearing the same exact tag is always an authoring mistake; a family
        // wildcard clear alongside a set in that family is the normal "replace" idiom.
        foreach (var tag in craftingAction.TagEffects.Set.Intersect(craftingAction.TagEffects.Clear, StringComparer.Ordinal))
            problems.Add(new("processes", $"{craftingAction.Id} both sets and clears tag '{tag}'."));
    }

    private static void ValidateProcessTag(
        CraftingActionDefinition craftingAction,
        string tag,
        string field,
        bool allowWildcard,
        List<ContentProblem> problems)
    {
        if (!TagFamilies.TryParse(tag, out var family, out var value))
        {
            problems.Add(new("processes", $"{craftingAction.Id} {field} has un-namespaced tag '{tag}' (expected family:value)."));
            return;
        }

        if (!TagFamilies.TryGet(family, out var def))
        {
            problems.Add(new("processes", $"{craftingAction.Id} {field} tag '{tag}' uses unknown family '{family}'."));
            return;
        }

        if (value == CraftingActionTagEffects.ClearFamilyWildcard)
        {
            if (!allowWildcard)
                problems.Add(new("processes", $"{craftingAction.Id} {field} may not use the '{family}:*' wildcard."));
            return;
        }

        if (def.ClosedValues is not null && !def.ClosedValues.Contains(value))
            problems.Add(new("processes", $"{craftingAction.Id} {field} tag '{tag}' is not a valid '{family}' value."));
    }

    /// <summary>
    /// Validates the <c>family:value</c> tag namespace on materials (docs/emergent-item-system §4.1):
    /// every tag is namespaced under a known family, closed families use allowed values, and
    /// per-family cardinality holds (comp/state/rarity exactly one, origin one-or-two, form ≥1).
    /// </summary>
    private static void ValidateMaterialTags(
        DataStore<MaterialDefinition> materials,
        List<ContentProblem> problems)
    {
        foreach (var material in materials.GetAll())
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var tag in material.Tags)
            {
                if (!TagFamilies.TryParse(tag, out var family, out var value))
                {
                    problems.Add(new("tags", $"{material.Id} has un-namespaced tag '{tag}' (expected family:value)."));
                    continue;
                }

                if (!TagFamilies.TryGet(family, out var def))
                {
                    problems.Add(new("tags", $"{material.Id} tag '{tag}' uses unknown family '{family}'."));
                    continue;
                }

                if (def.ClosedValues is not null && !def.ClosedValues.Contains(value))
                    problems.Add(new("tags", $"{material.Id} tag '{tag}' is not a valid '{family}' value."));

                counts[family] = counts.GetValueOrDefault(family) + 1;
            }

            foreach (var family in TagFamilies.All)
            {
                var count = counts.GetValueOrDefault(family.Name);
                if (count < family.Min)
                    problems.Add(new("tags", $"{material.Id} needs at least {family.Min} '{family.Name}:' tag (has {count})."));
                else if (count > family.Max)
                    problems.Add(new("tags", $"{material.Id} has {count} '{family.Name}:' tags (max {family.Max})."));
            }
        }
    }

    private static void ValidateActors(
        DataStore<ActorDefinition> actors,
        DataStore<Dungeons.Combat.MoveDefinition> moves,
        DataStore<Loot.LootTableDefinition> lootTables,
        DataStore<EnemyFamilyDefinition> families,
        DataStore<CombatRoleDefinition> roles,
        DataStore<AiProfileDefinition> aiProfiles,
        List<ContentProblem> problems)
    {
        // --- The composition layers stand alone first, so a broken shared piece reports once —
        // not once per actor that references it.

        foreach (var family in families.GetAll())
        {
            ValidateLanes(family.Id, "enemy_families", family.Resistances, problems);
            ValidateVulnerability(family.Id, "enemy_families", family.Vulnerable, problems);
            if (family.LootTableId is { Length: > 0 } familyLoot && !lootTables.Contains(familyLoot))
                problems.Add(new("enemy_families", $"{family.Id} references unknown loot table '{familyLoot}'."));
        }

        foreach (var role in roles.GetAll())
        {
            ValidateLanes(role.Id, "enemy_roles", role.Resistances, problems);
            ValidateVulnerability(role.Id, "enemy_roles", role.Vulnerable, problems);
            if (role.LootTableId is { Length: > 0 } roleLoot && !lootTables.Contains(roleLoot))
                problems.Add(new("enemy_roles", $"{role.Id} references unknown loot table '{roleLoot}'."));
            if (role.AiProfile is { } roleProfile && !aiProfiles.Contains(roleProfile))
                problems.Add(new("enemy_roles", $"{role.Id} references unknown AI profile '{roleProfile}'."));
        }

        foreach (var profile in aiProfiles.GetAll())
        {
            if (profile.AvoidRepeatWeight is < 0 or > 1)
                problems.Add(new("ai_profiles", $"{profile.Id} avoid_repeat_weight {profile.AvoidRepeatWeight} is outside [0, 1]."));

            foreach (var rule in profile.Rules)
                ValidateAiRule(profile.Id, "ai_profiles", rule, problems);
        }

        foreach (var actor in actors.GetAll())
        {
            // --- Layer references, and the fields the layers own -----------------------------
            var referencesResolve = true;
            if (actor.Family is { } familyId && !families.Contains(familyId))
            {
                problems.Add(new("actors", $"{actor.Id} references unknown family '{familyId}'."));
                referencesResolve = false;
            }
            if (actor.Role is { } roleId && !roles.Contains(roleId))
            {
                problems.Add(new("actors", $"{actor.Id} references unknown role '{roleId}'."));
                referencesResolve = false;
            }
            if (actor.AiProfile is { } profileId && !aiProfiles.Contains(profileId))
            {
                problems.Add(new("actors", $"{actor.Id} references unknown AI profile '{profileId}'."));
                referencesResolve = false;
            }

            if (actor.Family is not null)
            {
                // A layered actor authors deltas; an absolute field alongside a family would be
                // silently treated as one or the other, so it is a load error instead.
                if (!actor.Attributes.Equals(default(AttributeSet)))
                    problems.Add(new("actors",
                        $"{actor.Id} authors absolute attributes AND a family — use attribute_tweaks."));
                if (actor.Resources.Health != 1 || actor.Resources.Mana != 0 || actor.Resources.Stamina != 0)
                    problems.Add(new("actors",
                        $"{actor.Id} authors absolute resources AND a family — use resource_tweaks."));
            }

            foreach (var grant in actor.Moves)
            {
                if (!moves.Contains(grant.Id))
                {
                    problems.Add(new("actors", $"{actor.Id} grants unknown move '{grant.Id}'."));
                    continue;
                }

                // Enemies carry no equipment, so an equippedTag requirement can never pass —
                // the move would sit in the moveset, silently unusable, forever.
                if (moves.GetById(grant.Id).Requires.Any(c =>
                        string.Equals(c.Kind, Dungeons.Rules.RuleVocabulary.EquippedTag, StringComparison.OrdinalIgnoreCase)))
                    problems.Add(new("actors",
                        $"{actor.Id} grants '{grant.Id}', which requires equipment — enemies cannot satisfy equippedTag."));
            }

            if (actor.Moves.Count == 0)
                problems.Add(new("actors", $"{actor.Id} has no moves — it would stand there doing nothing."));

            // --- The RESOLVED brain must select from the actor's own moves -------------------
            // Rules come from the referenced profile plus inline extras; both must land.
            if (referencesResolve)
            {
                var resolved = ActorResolver.Resolve(actor, families, roles, aiProfiles);
                var grantedTags = actor.Moves
                    .Where(g => moves.Contains(g.Id))
                    .SelectMany(g => moves.GetById(g.Id).Tags)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var rule in resolved.Ai)
                {
                    if (!string.IsNullOrEmpty(rule.MoveTag))
                    {
                        if (!grantedTags.Contains(rule.MoveTag))
                            problems.Add(new("actors",
                                $"{actor.Id} AI matches tag '{rule.MoveTag}', which none of its moves carry."));
                    }
                    else if (actor.Moves.All(m => !string.Equals(m.Id, rule.Move, StringComparison.Ordinal)))
                    {
                        problems.Add(new("actors", $"{actor.Id} AI selects '{rule.Move}', which the actor does not have."));
                    }
                }
            }

            foreach (var rule in actor.Ai)
                ValidateAiRule(actor.Id, "actors", rule, problems);

            if (actor.LootTableId is { Length: > 0 } actorLoot && !lootTables.Contains(actorLoot))
                problems.Add(new("actors", $"{actor.Id} references unknown loot table '{actorLoot}'."));

            ValidateLanes(actor.Id, "actors", actor.Resistances, problems);
            ValidateVulnerability(actor.Id, "actors", actor.Vulnerable, problems);
        }
    }

    /// <summary>Shape checks a rule carries wherever it lives — profile or inline.</summary>
    private static void ValidateAiRule(string owner, string category, AiRuleSpec rule, List<ContentProblem> problems)
    {
        var hasMove = !string.IsNullOrEmpty(rule.Move);
        var hasTag = !string.IsNullOrEmpty(rule.MoveTag);
        if (hasMove == hasTag)
            problems.Add(new(category, $"{owner} AI rule must set exactly one of move/moveTag."));

        if (rule.Weight <= 0)
            problems.Add(new(category, $"{owner} AI rule for '{rule.Move}{rule.MoveTag}' has non-positive weight."));

        foreach (var condition in rule.When)
            if (!Dungeons.Rules.RuleVocabulary.Conditions.Contains(condition.Kind))
                problems.Add(new(category, $"{owner} AI uses unknown condition '{condition.Kind}'."));
    }

    private static void ValidateLanes(string owner, string category, Dictionary<string, double> resistances, List<ContentProblem> problems)
    {
        foreach (var lane in resistances.Keys)
            if (!DamageLanes.All.Contains(lane))
                problems.Add(new(category,
                    $"{owner} resists unknown lane '{lane}'. Valid lanes: {string.Join(", ", DamageLanes.All)}."));
    }

    /// <summary>Vulnerability is keyed by damage TYPE (D-02) — the one place the three physical
    /// types still matter defensively, so a lane name here is a real mistake.</summary>
    private static void ValidateVulnerability(string owner, string category, Dictionary<string, double> vulnerable, List<ContentProblem> problems)
    {
        foreach (var (type, multiplier) in vulnerable)
        {
            if (!Enum.TryParse<DamageType>(type, ignoreCase: true, out _))
                problems.Add(new(category,
                    $"{owner} is vulnerable to unknown damage type '{type}'. Valid: {string.Join(", ", Enum.GetNames<DamageType>())}."));

            if (multiplier < CombatTuning.MinVulnerability || multiplier > CombatTuning.MaxVulnerability)
                problems.Add(new(category,
                    $"{owner} vulnerability '{type}' is {multiplier}, outside the allowed " +
                    $"[{CombatTuning.MinVulnerability}, {CombatTuning.MaxVulnerability}] range."));
        }
    }

    /// <summary>§10 traits (C1a): every referenced property must exist, merges must resolve
    /// to known traits, and a merge-only trait (no condition) must actually be reachable as
    /// some merge's target — otherwise it is authored content nobody can ever see.</summary>
    private static void ValidateTraits(
        DataStore<Dungeons.Crafting.TraitDefinition> traits,
        IReadOnlySet<string> knownProperties,
        List<ContentProblem> problems)
    {
        var mergeTargets = traits.GetAll()
            .SelectMany(t => t.Merges.Select(m => m.Into))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var trait in traits.GetAll())
        {
            foreach (var property in trait.Condition.Keys
                         .Concat(trait.Consumes.Keys)
                         .Concat(trait.MagnitudeOf))
                if (!knownProperties.Contains(property))
                    problems.Add(new("traits", $"{trait.Id} references unknown property '{property}'."));

            foreach (var (property, amount) in trait.Consumes)
                if (amount <= 0)
                    problems.Add(new("traits", $"{trait.Id} consumes a non-positive amount of '{property}'."));

            if (trait.IsStateBorn && trait.MagnitudeOf.Count == 0)
                problems.Add(new("traits", $"{trait.Id} is state-born but names no magnitude_of properties."));

            foreach (var merge in trait.Merges)
            {
                if (merge.With == trait.Id)
                    problems.Add(new("traits", $"{trait.Id} merges with itself."));
                if (!traits.Contains(merge.With))
                    problems.Add(new("traits", $"{trait.Id} merges with unknown trait '{merge.With}'."));
                if (!traits.Contains(merge.Into))
                    problems.Add(new("traits", $"{trait.Id} merges into unknown trait '{merge.Into}'."));
            }

            if (!trait.IsStateBorn && !mergeTargets.Contains(trait.Id))
                problems.Add(new("traits",
                    $"{trait.Id} has no condition and is no merge's target — unreachable content."));

            if (!Dungeons.Crafting.EquipmentAssemblyTuning.TraitCategories.Contains(trait.Category))
                problems.Add(new("traits",
                    $"{trait.Id} has unknown category '{trait.Category}'. Valid: {string.Join(", ", Dungeons.Crafting.EquipmentAssemblyTuning.TraitCategories)}."));
        }
    }

    /// <summary>§16.2 forms (C2a): apertures gate known categories, stat maps read known
    /// properties from real slots, and granted moves resolve.</summary>
    /// <summary>
    /// Forms (C2a §16.2). Beyond the reference checks, four rules that each catch a form which
    /// <em>loads cleanly and is still broken</em> — the failure mode that costs a milestone
    /// because nothing throws:
    ///
    /// <list type="bullet">
    ///   <item><b>Mass shares must sum to 1.</b> They are shares. A form summing to 0.8 quietly
    ///   under-reads every <c>"*"</c> stat and under-weights every material influence.</item>
    ///   <item><b>Every slot gate must be satisfiable</b> by some shipped material, or the form
    ///   can never be assembled at all.</item>
    ///   <item><b>A weapon must grant moves.</b> Since E4 a weapon <i>is</i> its moves; one
    ///   granting none equips fine and leaves the player swinging nothing.</item>
    ///   <item><b>A form must carry the tag its modifier pool gates on.</b> Most affixes are
    ///   available to <c>weapon</c> / <c>armor</c> / <c>shield</c>; a weapon form missing the
    ///   <c>weapon</c> tag rolls no weapon modifiers and looks merely unlucky.</item>
    /// </list>
    /// </summary>
    private static void ValidateForms(
        DataStore<Dungeons.Crafting.EquipmentBlueprintDefinition> forms,
        DataStore<Dungeons.Combat.MoveDefinition> moves,
        DataStore<MaterialDefinition> materials,
        IReadOnlySet<string> knownProperties,
        List<ContentProblem> problems)
    {
        foreach (var form in forms.GetAll())
        {
            void Problem(string message) => problems.Add(new ContentProblem("forms", $"{form.Id} {message}"));

            if (form.Slots.Count == 0)
                problems.Add(new("forms", $"{form.Id} has no slots."));
            if (form.TraitCap < 1)
                problems.Add(new("forms", $"{form.Id} trait_cap must be at least 1."));

            if (form.Slots.Count > 0)
            {
                var totalMassShare = form.Slots.Values.Sum(slot => slot.MassShare);
                if (Math.Abs(totalMassShare - 1.0) > MassShareTolerance)
                    Problem($"mass shares sum to {totalMassShare:0.###}; they are shares and must sum to 1.");
            }

            foreach (var (slotName, slot) in form.Slots)
            {
                foreach (var category in slot.TraitExpression.Keys)
                    if (!Dungeons.Crafting.EquipmentAssemblyTuning.TraitCategories.Contains(category))
                        problems.Add(new("forms", $"{form.Id} slot '{slotName}' traitExpression gates unknown category '{category}'."));

                if (slot.MassShare <= 0)
                    Problem($"slot '{slotName}' has mass_share {slot.MassShare}; a component that is none of the item cannot exist.");

                // An unsatisfiable gate is a form nobody can ever assemble. Skipped for bundles
                // with no material store — those are partial fixtures, not shipped content.
                if (materials.Count > 0 && slot.RequiresTags.Count > 0
                    && !materials.GetAll().Any(material =>
                        slot.RequiresTags.Any(tag => material.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))))
                    Problem($"slot '{slotName}' accepts {string.Join("/", slot.RequiresTags)}, which no material carries — it could never be assembled.");
            }

            foreach (var (stat, reads) in form.StatMap)
                foreach (var read in reads)
                {
                    if (!knownProperties.Contains(read.Property))
                        problems.Add(new("forms", $"{form.Id} stat '{stat}' reads unknown property '{read.Property}'."));
                    if (read.Slot != Dungeons.Crafting.BlueprintSlots.AllSlots && !form.Slots.ContainsKey(read.Slot))
                        problems.Add(new("forms", $"{form.Id} stat '{stat}' reads unknown slot '{read.Slot}'."));
                }

            var tags = new HashSet<string>(form.Tags, StringComparer.OrdinalIgnoreCase);

            foreach (var grant in form.Moves)
            {
                if (!moves.Contains(grant.Id))
                {
                    problems.Add(new("forms", $"{form.Id} grants unknown move '{grant.Id}'."));
                    continue;
                }

                // A granted move gated on equipment this form does not carry can NEVER fire. It
                // sits in the moveset looking available forever, which is the actor-side
                // equippedTag rule wearing a different hat — and it shipped: the Warspear
                // granted the sword-gated Skewer for the whole of C2a.
                foreach (var condition in moves.GetById(grant.Id).Requires)
                {
                    if (!string.Equals(condition.Kind, Dungeons.Rules.RuleVocabulary.EquippedTag, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!tags.Contains(condition.Text))
                        Problem($"grants '{grant.Id}', which requires the '{condition.Text}' tag — this form does not carry it, so the move could never fire.");
                }
            }

            if (form.Type == EquipmentSlot.Weapon && form.Moves.Count == 0)
                Problem("is a weapon that grants no moves — since E4 a weapon IS its moves.");

            if (form.Type == EquipmentSlot.Weapon && !tags.Contains("weapon"))
                Problem("is a weapon but does not carry the 'weapon' tag, so no weapon modifier is available to it.");
            if (EquipmentSlots.GrantsArmor(form.Type) && !tags.Contains("armor") && !tags.Contains("shield"))
                Problem("is worn armour but carries neither the 'armor' nor the 'shield' tag, so no defensive modifier is available to it.");
        }
    }

    /// <summary>§5 essence (C1b): anchors must be real properties, oppositions must resolve,
    /// and every material's essence vector must use known keys in [0, 100]. The set is typed
    /// and closed — a typo'd essence key is a load error, never a silent zero.</summary>
    private static void ValidateEssences(
        DataStore<Dungeons.Crafting.EssenceDefinition> essences,
        DataStore<MaterialDefinition> materials,
        IReadOnlySet<string> knownProperties,
        List<ContentProblem> problems)
    {
        var keys = essences.GetAll().Select(e => e.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var essence in essences.GetAll())
        {
            if (string.IsNullOrEmpty(essence.Anchor) || !knownProperties.Contains(essence.Anchor))
                problems.Add(new("essences", $"{essence.Id} anchors on unknown property '{essence.Anchor}'."));

            foreach (var opposed in essence.Opposes)
            {
                if (!keys.Contains(opposed))
                    problems.Add(new("essences", $"{essence.Id} opposes unknown essence '{opposed}'."));
                if (string.Equals(opposed, essence.Key, StringComparison.OrdinalIgnoreCase))
                    problems.Add(new("essences", $"{essence.Id} opposes itself."));
            }
        }

        if (essences.Count == 0)
            return; // pre-C1b bundles (tests may build partial bundles); nothing to key against

        foreach (var material in materials.GetAll())
        {
            foreach (var (key, value) in material.Essence)
            {
                if (!keys.Contains(key))
                    problems.Add(new("essences",
                        $"{material.Id} authors unknown essence '{key}'. Valid: {string.Join(", ", keys.OrderBy(k => k))}."));
                if (value is < 0 or > 100)
                    problems.Add(new("essences", $"{material.Id} essence '{key}' is {value}, outside [0, 100]."));
            }
        }
    }

    private static void ValidateProfessionActions(
        DataStore<ProfessionActionDefinition> actions,
        DataStore<ProfessionDefinition> professions,
        DataStore<MaterialDefinition> materials,
        DataStore<RealmDefinition> realms,
        DataStore<Loot.LootTableDefinition> lootTables,
        List<ContentProblem> problems)
    {
        // Opportunity ids are referenced by the client when a pending offer is pursued, so
        // they have to be unique across the whole action set, not just within one action.
        var opportunityIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var action in actions.GetAll())
        {
            if (!professions.Contains(action.ProfessionId))
                problems.Add(new("profession_actions", $"{action.Id} references unknown profession '{action.ProfessionId}'."));

            if (action.SuccessChance is <= 0 or > 1)
                problems.Add(new("profession_actions", $"{action.Id} success chance is {action.SuccessChance}, outside (0, 1]."));

            if (action.BaseIntervalTicks < 1)
                problems.Add(new("profession_actions", $"{action.Id} interval is {action.BaseIntervalTicks} ticks; must be at least 1."));

            foreach (var io in action.Inputs.Concat(action.Outputs))
                if (!materials.Contains(io.ItemId))
                    problems.Add(new("profession_actions", $"{action.Id} references unknown material '{io.ItemId}'."));

            foreach (var bonus in action.BonusOutputs)
                if (!materials.Contains(bonus.ItemId))
                    problems.Add(new("profession_actions", $"{action.Id} bonus output references unknown material '{bonus.ItemId}'."));

            if (action.RealmKnowledgeGain is { } knowledge)
            {
                if (!realms.Contains(knowledge.RealmId))
                    problems.Add(new("profession_actions", $"{action.Id} teaches unknown realm '{knowledge.RealmId}'."));
                if (knowledge.Amount <= 0)
                    problems.Add(new("profession_actions", $"{action.Id} realm knowledge gain is {knowledge.Amount}; must be positive."));
            }

            if (action.LootTableId is { Length: > 0 } dropTable && !lootTables.Contains(dropTable))
                problems.Add(new("profession_actions", $"{action.Id} references unknown loot table '{dropTable}'."));

            foreach (var opportunity in action.Opportunities)
                ValidateOpportunity(action, opportunity, materials, opportunityIds, problems);
        }
    }

    private static void ValidateOpportunity(
        ProfessionActionDefinition action,
        ProfessionOpportunityDefinition opportunity,
        DataStore<MaterialDefinition> materials,
        HashSet<string> seenIds,
        List<ContentProblem> problems)
    {
        if (string.IsNullOrWhiteSpace(opportunity.Id))
        {
            problems.Add(new("profession_actions", $"{action.Id} has an opportunity with no id."));
            return;
        }

        if (!seenIds.Add(opportunity.Id))
            problems.Add(new("profession_actions", $"{action.Id} reuses opportunity id '{opportunity.Id}'."));

        if (string.IsNullOrWhiteSpace(opportunity.Prompt))
            problems.Add(new("profession_actions", $"{opportunity.Id} has no prompt; the offer is the decision."));

        if (opportunity.DiscoveryChance is <= 0 or > 1)
            problems.Add(new("profession_actions", $"{opportunity.Id} discovery chance is {opportunity.DiscoveryChance}, outside (0, 1]."));

        if (opportunity.RiskWeight is < 0 or > 1)
            problems.Add(new("profession_actions", $"{opportunity.Id} risk weight is {opportunity.RiskWeight}, outside [0, 1]."));

        if (opportunity.ExtraIntervalTicks < 1)
            problems.Add(new("profession_actions", $"{opportunity.Id} costs {opportunity.ExtraIntervalTicks} ticks; pursuing must cost time."));

        // A gate above the mastery ceiling is an offer nobody can ever be shown.
        if (opportunity.RequiredMasteryLevel is < 0 or > MasteryLeveling.MaxLevel)
            problems.Add(new("profession_actions",
                $"{opportunity.Id} needs mastery {opportunity.RequiredMasteryLevel}, outside 0–{MasteryLeveling.MaxLevel}; it could never be offered."));

        // An opportunity that pays nothing is a decision with one right answer.
        if (opportunity.Outputs.Count == 0 && opportunity.BonusOutputs.Count == 0 && opportunity.Experience <= 0)
            problems.Add(new("profession_actions", $"{opportunity.Id} has no payoff — outputs, bonus outputs and XP are all empty."));

        foreach (var io in opportunity.Inputs.Concat(opportunity.Outputs))
            if (!materials.Contains(io.ItemId))
                problems.Add(new("profession_actions", $"{opportunity.Id} references unknown material '{io.ItemId}'."));

        foreach (var bonus in opportunity.BonusOutputs)
            if (!materials.Contains(bonus.ItemId))
                problems.Add(new("profession_actions", $"{opportunity.Id} bonus output references unknown material '{bonus.ItemId}'."));
    }

    /// <summary>
    /// The mastery ladder has to be a ladder. Every rule here exists because the failure it
    /// catches is silent: a rung worth nothing per level, or capped at nothing, or unlocking
    /// above the level ceiling, all load cleanly and quietly do nothing forever — which is
    /// exactly the state Phase 8 was written to get mastery out of.
    /// </summary>
    private static void ValidateMasteryLadder(
        DataStore<MasteryBenefitDefinition> ladder,
        DataStore<ProfessionDefinition> professions,
        List<ContentProblem> problems)
    {
        var seen = new HashSet<(ProfessionBenefitKind, string?)>();

        foreach (var rung in ladder.GetAll())
        {
            if (rung.PerLevel <= 0)
                problems.Add(new("mastery", $"{rung.Id} grants {rung.PerLevel} per level; a rung that pays nothing is not a rung."));

            if (rung.Max <= 0)
                problems.Add(new("mastery", $"{rung.Id} is capped at {rung.Max}; it can never be worth anything."));

            if (rung.UnlockLevel < 1 || rung.UnlockLevel > MasteryLeveling.MaxLevel)
                problems.Add(new("mastery",
                    $"{rung.Id} unlocks at mastery {rung.UnlockLevel}, outside 1–{MasteryLeveling.MaxLevel}; it could never switch on."));

            // A rung whose cap needs more levels than exist is a promise the ceiling cannot keep.
            if (rung.PerLevel > 0 && rung.Max > MasteryLeveling.MaxLevel * rung.PerLevel)
                problems.Add(new("mastery",
                    $"{rung.Id} caps at {rung.Max} but reaches only {MasteryLeveling.MaxLevel * rung.PerLevel:0.###} at mastery {MasteryLeveling.MaxLevel}."));

            if (rung.ProfessionId is { Length: > 0 } professionId && !professions.Contains(professionId))
                problems.Add(new("mastery", $"{rung.Id} is scoped to unknown profession '{professionId}'."));

            if (!seen.Add((rung.Kind, rung.ProfessionId)))
                problems.Add(new("mastery",
                    $"{rung.Id} is a second {rung.Kind} rung for the same scope; one would silently win over the other."));

            if (string.IsNullOrWhiteSpace(rung.Description))
                problems.Add(new("mastery", $"{rung.Id} has no description; the player reads this ladder."));
        }

        // A PARTIAL ladder is the dangerous one: somebody authored rungs and forgot a kind, and
        // that benefit silently does nothing forever. An EMPTY ladder is a bundle that simply
        // carries no mastery content — every test fixture in the suite — and is left alone, the
        // same way an empty obstacle store is. That the *shipped* game carries all six is held
        // by `MasteryContentTests`, which is the right place for "this file must exist".
        if (ladder.Count == 0)
            return;

        foreach (var kind in Enum.GetValues<ProfessionBenefitKind>())
        {
            if (!ladder.GetAll().Any(rung => rung.Kind == kind && rung.ProfessionId is null))
                problems.Add(new("mastery", $"no general rung for {kind}; that benefit does nothing for any profession."));
        }
    }

    /// <summary>
    /// The cross-profession and global bonus table (Phase 10).
    ///
    /// <para>Written before the data it checks, on the precedent Phase 8 set: the mastery
    /// ladder's caps were wrong on their first run and the validator said so immediately. The
    /// dangerous mistake here is the same shape — a cap the source level can never reach is a
    /// promise the table cannot keep — plus two that are specific to synergies: a source that
    /// is also the target (a self-amplifying loop dressed as a synergy) and a duplicated scope
    /// (which reads as one bonus and pays twice).</para>
    /// </summary>
    private static void ValidateProfessionSynergies(
        DataStore<ProfessionSynergyDefinition> synergies,
        DataStore<ProfessionDefinition> professions,
        List<ContentProblem> problems)
    {
        var seen = new HashSet<(ProfessionBenefitKind, string?, string?)>();

        // A global synergy reads the sum of every profession's level, so its ceiling is the
        // roster's. With no roster loaded (a fixture bundle) there is nothing to measure
        // against, and the reachability check is skipped rather than guessed at.
        var rosterSize = professions.Count;
        var highestTotalLevel = rosterSize * ProfessionLeveling.MaxLevel;

        foreach (var synergy in synergies.GetAll())
        {
            if (synergy.PerLevel <= 0)
                problems.Add(new("synergies", $"{synergy.Id} grants {synergy.PerLevel} per level; a synergy that pays nothing is not a synergy."));

            if (synergy.Max <= 0)
                problems.Add(new("synergies", $"{synergy.Id} is capped at {synergy.Max}; it can never be worth anything."));

            if (synergy.SourceProfession is { Length: > 0 } source && !professions.Contains(source))
                problems.Add(new("synergies", $"{synergy.Id} is paid for by unknown profession '{source}'."));

            if (synergy.TargetProfession is { Length: > 0 } target && !professions.Contains(target))
                problems.Add(new("synergies", $"{synergy.Id} pays into unknown profession '{target}'."));

            if (synergy.SourceProfession is not null
                && string.Equals(synergy.SourceProfession, synergy.TargetProfession, StringComparison.Ordinal))
                problems.Add(new("synergies",
                    $"{synergy.Id} pays '{synergy.SourceProfession}' for its own level; that is a mastery rung, not a synergy."));

            var highestSourceLevel = synergy.IsGlobalSource ? highestTotalLevel : ProfessionLeveling.MaxLevel;

            if (synergy.UnlockLevel < 1 || (highestSourceLevel > 0 && synergy.UnlockLevel > highestSourceLevel))
                problems.Add(new("synergies",
                    $"{synergy.Id} unlocks at source level {synergy.UnlockLevel}, outside 1–{highestSourceLevel}; it could never switch on."));

            if (synergy.PerLevel > 0 && highestSourceLevel > 0 && synergy.Max > highestSourceLevel * synergy.PerLevel)
                problems.Add(new("synergies",
                    $"{synergy.Id} caps at {synergy.Max} but reaches only {highestSourceLevel * synergy.PerLevel:0.###} at source level {highestSourceLevel}."));

            if (!seen.Add((synergy.Kind, synergy.SourceProfession, synergy.TargetProfession)))
                problems.Add(new("synergies",
                    $"{synergy.Id} is a second {synergy.Kind} synergy for the same source and target; the pair would pay twice for one line the player reads."));

            if (string.IsNullOrWhiteSpace(synergy.Description))
                problems.Add(new("synergies", $"{synergy.Id} has no description; the player reads this table."));
        }
    }

    /// <summary>
    /// Auto-combat brains (Phase 10).
    ///
    /// <para>The rule worth having is the last one: <b>D-07 says automation is disadvantaged by
    /// reaction latency, never by a damage penalty</b> — which only holds if the latency is
    /// actually longer than the tight windows. A profile with a 2-tick reaction would parry, and
    /// the whole "active play earns its advantage by being present" argument would quietly stop
    /// being true. That is a content mistake nobody would notice by reading, so it is checked.</para>
    /// </summary>
    private static void ValidateAutoCombatProfiles(
        DataStore<AutoCombatProfileDefinition> profiles,
        DataStore<MoveDefinition> moves,
        List<ContentProblem> problems)
    {
        foreach (var profile in profiles.GetAll())
        {
            if (profile.Rules.Count == 0)
                problems.Add(new("auto_combat", $"{profile.Id} has no offensive rules; it would stand still and be hit."));

            foreach (var rule in profile.Rules)
            {
                ValidateAiRule(profile.Id, "auto_combat", rule, problems);

                // Only the id form can be checked: a tag rule is answered by whatever the
                // player happens to be carrying, which is the point of the tag form.
                if (rule.Move is { Length: > 0 } moveId && string.IsNullOrEmpty(rule.MoveTag) && !moves.Contains(moveId))
                    problems.Add(new("auto_combat", $"{profile.Id} names unknown move '{moveId}'."));
            }

            foreach (var rule in profile.Defence)
            {
                if (rule.Weight <= 0)
                    problems.Add(new("auto_combat", $"{profile.Id} defence rule for {rule.Stance} has non-positive weight."));

                foreach (var condition in rule.When)
                    if (!Dungeons.Rules.RuleVocabulary.Conditions.Contains(condition.Kind))
                        problems.Add(new("auto_combat", $"{profile.Id} defence uses unknown condition '{condition.Kind}'."));
            }

            if (profile.ReactionTicks < AutoCombatTuning.MinimumReactionTicks)
                problems.Add(new("auto_combat",
                    $"{profile.Id} reacts in {profile.ReactionTicks} ticks, inside the {CombatTuning.PerfectBlockWindowTicks}-tick " +
                    $"Perfect Block and {CombatTuning.ParryWindowTicks}-tick Parry windows. Automation must never reach those (D-07)."));

            if (profile.AvoidRepeatWeight is < 0 or > 1)
                problems.Add(new("auto_combat", $"{profile.Id} has avoid_repeat_weight {profile.AvoidRepeatWeight}, outside 0–1."));

            if (string.IsNullOrWhiteSpace(profile.Description))
                problems.Add(new("auto_combat", $"{profile.Id} has no description; the player chooses between these."));
        }
    }

    private static void ValidateTrainingObstacles(
        DataStore<TrainingObstacleDefinition> obstacles,
        List<ContentProblem> problems)
    {
        var knownBonuses = new HashSet<string>(CourseBonusKeys.All, StringComparer.Ordinal);

        foreach (var obstacle in obstacles.GetAll())
        {
            if (obstacle.IntervalTicks < 1)
                problems.Add(new("training_obstacles", $"{obstacle.Id} costs {obstacle.IntervalTicks} ticks; a lap must take time."));

            if (obstacle.Experience <= 0)
                problems.Add(new("training_obstacles", $"{obstacle.Id} grants no XP; the course is Agility's only faucet."));

            if (obstacle.Bonuses.Count == 0)
                problems.Add(new("training_obstacles", $"{obstacle.Id} grants no bonus; fitting it would be a choice with no consequence."));

            foreach (var bonus in obstacle.Bonuses)
            {
                if (!knownBonuses.Contains(bonus.Key))
                    problems.Add(new("training_obstacles", $"{obstacle.Id} grants unknown course bonus '{bonus.Key}'."));
                if (bonus.Value <= 0)
                    problems.Add(new("training_obstacles", $"{obstacle.Id} bonus '{bonus.Key}' is {bonus.Value}; must be positive."));
            }
        }
    }

    /// <summary>
    /// Hideout stations are pure routing, so every rule here is about reachability: each
    /// reference resolves, and — the load-bearing one — <b>every profession is hosted by
    /// exactly one station</b>. Without it a new profession could ship with no way to reach
    /// it, or with its ladder drawn on two screens that then drift apart.
    /// </summary>
    private static void ValidateStations(ContentBundle content, List<ContentProblem> problems)
    {
        var stationHostingProfession = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var station in content.Stations.GetAll())
        {
            void Problem(string message) => problems.Add(new("stations", $"{station.Id}: {message}"));

            if (station.Professions.Count == 0)
                Problem("hosts no profession; a station with nothing to train is unreachable furniture.");

            foreach (var professionId in station.Professions)
            {
                if (!content.Professions.Contains(professionId))
                {
                    Problem($"hosts unknown profession '{professionId}'.");
                    continue;
                }

                if (stationHostingProfession.TryGetValue(professionId, out var alreadyHosting))
                    Problem($"also hosts '{professionId}', which already belongs to {alreadyHosting}.");
                else
                    stationHostingProfession[professionId] = station.Id;
            }

            foreach (var craftingActionId in station.CraftingActions)
                if (!content.CraftingActions.Contains(craftingActionId))
                    Problem($"offers unknown crafting action '{craftingActionId}'.");

            foreach (var blueprintId in station.Blueprints)
                if (!content.Forms.Contains(blueprintId))
                    Problem($"assembles unknown blueprint '{blueprintId}'.");

            foreach (var verbActionId in station.VerbActions)
                if (!content.VerbActions.Contains(verbActionId))
                    Problem($"offers unknown verb action '{verbActionId}'.");
        }

        // The reverse-reachability rules below are only meaningful once stations exist at all —
        // an empty store is a bundle assembled for some other test, not content that forgot
        // twenty stations.
        if (content.Stations.Count == 0)
            return;

        foreach (var profession in content.Professions.GetAll())
            if (!stationHostingProfession.ContainsKey(profession.Id))
                problems.Add(new("stations",
                    $"{profession.Id} has no station; there would be no way to reach it from the Hideout."));

        // Same standard the move vocabulary is held to: content nobody can reach is a mistake,
        // not a feature waiting for a screen.
        var offeredCraftingActions = content.Stations.GetAll().SelectMany(s => s.CraftingActions).ToHashSet(StringComparer.Ordinal);
        foreach (var craftingAction in content.CraftingActions.GetAll())
            if (!offeredCraftingActions.Contains(craftingAction.Id))
                problems.Add(new("stations",
                    $"{craftingAction.Id} is offered at no station — the player could never run it."));

        var assembledBlueprints = content.Stations.GetAll().SelectMany(s => s.Blueprints).ToHashSet(StringComparer.Ordinal);
        foreach (var blueprint in content.Forms.GetAll())
            if (!assembledBlueprints.Contains(blueprint.Id))
                problems.Add(new("stations",
                    $"{blueprint.Id} is assembled at no station — the player could never make one."));

        var offeredVerbActions = content.Stations.GetAll().SelectMany(s => s.VerbActions).ToHashSet(StringComparer.Ordinal);
        foreach (var verbAction in content.VerbActions.GetAll())
            if (!offeredVerbActions.Contains(verbAction.Id))
                problems.Add(new("stations",
                    $"{verbAction.Id} is offered at no station — the player could never run it."));
    }

    private static void ValidateInteractions(
        DataStore<CraftingInteractionDefinition> interactions,
        DataStore<MaterialDefinition> materials,
        DataStore<ConsumableDefinition> consumables,
        DataStore<ProfessionDefinition> professions,
        List<ContentProblem> problems)
    {
        foreach (var interaction in interactions.GetAll())
        {
            foreach (var input in interaction.Inputs)
                if (!materials.Contains(input.ItemId))
                    problems.Add(new("crafting", $"{interaction.Id} input references unknown material '{input.ItemId}'."));

            // A result may be a stackable material or a consumable item.
            if (!materials.Contains(interaction.ResultItemId) && !consumables.Contains(interaction.ResultItemId))
                problems.Add(new("crafting", $"{interaction.Id} result '{interaction.ResultItemId}' is neither a known material nor consumable."));

            foreach (var req in interaction.ProfessionRequirements)
                if (!professions.Contains(req.ProfessionId))
                    problems.Add(new("crafting", $"{interaction.Id} requires unknown profession '{req.ProfessionId}'."));
        }
    }

    private static void ValidateRealms(
        DataStore<RealmDefinition> realms,
        DataStore<ActorDefinition> actors,
        DataStore<ProfessionActionDefinition> actions,
        DataStore<Loot.LootTableDefinition> lootTables,
        List<ContentProblem> problems)
    {
        foreach (var realm in realms.GetAll())
        {
            foreach (var loc in realm.Locations)
            {
                foreach (var connection in loc.Connections)
                {
                    if (!realm.HasLocation(connection))
                    {
                        problems.Add(new("realms", $"{realm.Id}/{loc.Id} connects to unknown location '{connection}'."));
                        continue;
                    }

                    // Edges must be symmetric so the party can move back and forth.
                    if (!realm.GetLocation(connection).Connections.Contains(loc.Id))
                        problems.Add(new("realms", $"{realm.Id}: edge {loc.Id} → {connection} is not symmetric."));
                }

                switch (loc.Type)
                {
                    case RealmLocationType.Combat:
                        if (string.IsNullOrEmpty(loc.ActorId))
                            problems.Add(new("realms", $"{realm.Id}/{loc.Id} is a Combat node with no actor."));
                        else if (!actors.Contains(loc.ActorId))
                            problems.Add(new("realms", $"{realm.Id}/{loc.Id} references unknown actor '{loc.ActorId}'."));
                        break;

                    case RealmLocationType.Gather:
                        if (string.IsNullOrEmpty(loc.ProfessionActionId))
                            problems.Add(new("realms", $"{realm.Id}/{loc.Id} is a Gather node with no profession action."));
                        else if (!actions.Contains(loc.ProfessionActionId))
                            problems.Add(new("realms", $"{realm.Id}/{loc.Id} references unknown action '{loc.ProfessionActionId}'."));
                        break;

                    case RealmLocationType.Event:
                        // An event with neither text nor a table is a node that does nothing.
                        if (string.IsNullOrEmpty(loc.EventText) && string.IsNullOrEmpty(loc.LootTableId))
                            problems.Add(new("realms", $"{realm.Id}/{loc.Id} is an Event node with no text and no loot."));
                        break;

                    // Each of the Phase 6 kinds is defined by ONE number or reference. Without
                    // it the node loads, sits on the map, and does nothing when stood on — the
                    // failure mode that costs a playtest because nothing errors.
                    case RealmLocationType.Camp:
                        if (loc.RestoreFraction is <= 0 or > 1)
                            problems.Add(new("realms", $"{realm.Id}/{loc.Id} is a Camp with restore_fraction {loc.RestoreFraction}; must be in (0, 1]."));
                        break;

                    case RealmLocationType.Shrine:
                        if (string.IsNullOrEmpty(loc.EventText))
                            problems.Add(new("realms", $"{realm.Id}/{loc.Id} is a Shrine with no text — the blessing is the words."));
                        break;

                    case RealmLocationType.Merchant:
                        if (loc.Cost <= 0)
                            problems.Add(new("realms", $"{realm.Id}/{loc.Id} is a Merchant charging {loc.Cost}; goods must cost coin."));
                        if (string.IsNullOrEmpty(loc.LootTableId))
                            problems.Add(new("realms", $"{realm.Id}/{loc.Id} is a Merchant with no stock table."));
                        break;

                    case RealmLocationType.Hazard:
                        if (loc.HazardDamage <= 0)
                            problems.Add(new("realms", $"{realm.Id}/{loc.Id} is a Hazard dealing {loc.HazardDamage}; crossing must cost something."));
                        break;
                }

                // A hidden node nothing visible connects to can never be reached, even once
                // Knowledge reveals it — it would appear on the map behind a door with no wall.
                if (loc.Hidden
                    && !realm.Locations.Any(other =>
                        !other.Hidden && other.Depth == loc.Depth && other.Connections.Contains(loc.Id)))
                    problems.Add(new("realms",
                        $"{realm.Id}/{loc.Id} is Hidden but no visible node at its depth connects to it — revealing it would not help."));

                if (loc.LootTableId is { Length: > 0 } nodeLoot && !lootTables.Contains(nodeLoot))
                    problems.Add(new("realms", $"{realm.Id}/{loc.Id} references unknown loot table '{nodeLoot}'."));
            }
        }
    }

    /// <summary>
    /// Statuses, and the references to them (E2, docs/statuses.md §6.2).
    ///
    /// <para>The rule that matters most is the last one: <b>every <c>applyStatus</c> in shipped
    /// content must name a real status.</b> Fourteen ids were authored against no status system
    /// at all and sat in <c>TriggerRuleEngine.Unhandled</c> for two milestones. This is what
    /// stops that recurring.</para>
    /// </summary>
    private static void ValidateStatuses(ContentBundle content, List<ContentProblem> problems)
    {
        var statuses = content.Statuses;

        foreach (var status in statuses.GetAll())
        {
            if (status.Lane is { } lane && !DamageLanes.All.Contains(lane))
                problems.Add(new("statuses", $"{status.Id} uses unknown lane '{lane}'."));

            if (status.RequiresStatus is { } gate && !statuses.Contains(gate))
                problems.Add(new("statuses", $"{status.Id} requires unknown status '{gate}'."));

            if (status.RequiresStatus == status.Id)
                problems.Add(new("statuses", $"{status.Id} requires itself."));

            // A control outside the Resolve gate would be a permanent lock, and buildup on a
            // non-control is a number nothing reads.
            if (status.IsControl && status.ControlBuildup <= 0 && status.Id != "status.stun")
                problems.Add(new("statuses", $"{status.Id} is a control with no control_buildup — it could never be applied."));
            if (!status.IsControl && status.ControlBuildup > 0)
                problems.Add(new("statuses", $"{status.Id} is not a control but declares control_buildup."));

            if (status.StackPolicy != StackPolicy.Stack && status.MaxStacks > 1)
                problems.Add(new("statuses", $"{status.Id} allows {status.MaxStacks} stacks but its policy is {status.StackPolicy}."));

            if (status.TickInterval > 0 && status.PerTick.Count == 0 && status.Magnitude.Basis == MagnitudeBasis.Flat && status.Magnitude.Coefficient == 0)
                problems.Add(new("statuses", $"{status.Id} ticks but does nothing on tick."));

            foreach (var modifier in status.WhileActive)
                if (!content.ModifierKeys.Contains(modifier.Key))
                    problems.Add(new("statuses", $"{status.Id} contributes to unknown modifier key '{modifier.Key}'."));
        }

        // Every status reference in character content must resolve.
        void CheckRules(IEnumerable<Rules.TriggerRule> rules, string owner)
        {
            foreach (var rule in rules)
            {
                foreach (var effect in rule.Payload)
                {
                    if (!string.Equals(effect.Kind, Rules.RuleVocabulary.ApplyStatus, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (string.IsNullOrEmpty(effect.Text) || statuses.Contains(effect.Text))
                        continue;

                    problems.Add(new("statuses", $"{owner} applies unknown status '{effect.Text}'."));
                }
            }
        }

        foreach (var prefix in content.Prefixes.GetAll())
        {
            CheckRules(prefix.Rules, prefix.Id);
            if (prefix.Gauge is { } gauge)
                CheckRules(gauge.Feeds, prefix.Id);
        }

        foreach (var suffix in content.Suffixes.GetAll())
            CheckRules(suffix.Expressions.Select(e => e.Rule), suffix.Id);
    }

    private static void ValidateEquipment(
        DataStore<EquipmentDefinition> equipment,
        DataStore<Dungeons.Combat.MoveDefinition> moves,
        DataStore<Dungeons.Combat.MoveModifierDefinition> moveModifiers,
        IReadOnlySet<string> knownProperties,
        List<ContentProblem> problems)
    {
        foreach (var def in equipment.GetAll())
        {
            // Weapon-granted moves are mandatory, not optional — the Fighter's identity is
            // "moveset comes from the weapon" (docs/moves.md §5.1). And a piece worn in an
            // armour-bearing slot with no armour block mitigates nothing while looking as
            // though it should.
            if (def.Slot == EquipmentSlot.Weapon && def.Moves.Count == 0)
                problems.Add(new("equipment", $"{def.Id} is a Weapon but grants no moves."));
            else if (EquipmentSlots.GrantsArmor(def.Slot) && def.Armor is null)
                problems.Add(new("equipment", $"{def.Id} is worn in the {def.Slot} slot but has no armor stats block."));

            foreach (var grant in def.Moves)
                if (!moves.Contains(grant.Id))
                    problems.Add(new("equipment", $"{def.Id} grants unknown move '{grant.Id}'."));

            foreach (var modifierId in def.MoveModifierIds)
                if (!moveModifiers.Contains(modifierId))
                    problems.Add(new("equipment", $"{def.Id} grants unknown move modifier '{modifierId}'."));

            foreach (var property in def.Properties.Keys)
                if (!knownProperties.Contains(property))
                    problems.Add(new("equipment", $"{def.Id} has unknown property '{property}' (typo, or add it to game/data/properties/)."));

            // Resistances are keyed by damage LANE, not by damage-type name (D-02). Authoring
            // "Slashing" here used to silently resist nothing once the lanes collapsed.
            foreach (var lane in def.Armor?.Resistances.Keys ?? Enumerable.Empty<string>())
                if (!DamageLanes.All.Contains(lane))
                    problems.Add(new("equipment",
                        $"{def.Id} resists unknown lane '{lane}'. Valid lanes: {string.Join(", ", DamageLanes.All)}."));
        }
    }

    /// <summary>Move and modifier grants on character components must resolve — no allowlist.</summary>
    private static void ValidateComponentMoves(ContentBundle content, List<ContentProblem> problems)
    {
        void Check(IEnumerable<CharacterComponentDefinition> components, string kind)
        {
            foreach (var component in components)
            {
                foreach (var grant in component.Moves)
                    if (!content.Moves.Contains(grant.Id))
                        problems.Add(new(kind, $"{component.Id} grants unknown move '{grant.Id}'."));

                foreach (var modifierId in component.MoveModifierIds)
                    if (!content.MoveModifiers.Contains(modifierId))
                        problems.Add(new(kind, $"{component.Id} grants unknown move modifier '{modifierId}'."));
            }
        }

        Check(content.Species.GetAll(), "species");
        Check(content.Classes.GetAll(), "classes");
        Check(content.Prefixes.GetAll(), "prefixes");
        Check(content.Suffixes.GetAll(), "suffixes");
    }

    /// <summary>
    /// The move rules from docs/moves.md §6 — everything a typo could hide behind, plus the two
    /// structural ones: no <c>triggerMove</c> may target a move that can itself
    /// <c>triggerMove</c>, and every move must be reachable from some source (orphan content is
    /// content nobody can ever see).
    ///
    /// <para>Organised in four independent blocks — read only the one you care about:</para>
    /// <list type="number">
    /// <item>per-move: tags, costs, requirements, targeting, packets, riders</item>
    /// <item>move modifiers: match shape and op vocabulary</item>
    /// <item>techniques: the move they teach must exist</item>
    /// <item>reachability: every move must be granted by something</item>
    /// </list>
    /// </summary>
    private static void ValidateMoves(ContentBundle content, List<ContentProblem> problems)
    {
        var moves = content.Moves;

        // Every gauge name in shipped classes/prefixes, so a cost can name one.
        var gaugeNames = content.Classes.GetAll().Select(c => c.Gauge?.Name)
            .Concat(content.Prefixes.GetAll().Select(p => p.Gauge?.Name))
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var poolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "health", "stamina", "mana" };

        // --- Per-move rules -------------------------------------------------------------------

        foreach (var move in moves.GetAll())
        {
            foreach (var tag in move.Tags)
                if (!Dungeons.Combat.MoveTags.IsValidOnMove(tag, out var tagProblem))
                    problems.Add(new("moves", $"{move.Id}: {tagProblem}"));

            if (!move.Tags.Any(t => t.StartsWith("action:", StringComparison.OrdinalIgnoreCase)))
                problems.Add(new("moves", $"{move.Id} carries no action: tag — CanAct gating and every action-scoped hook would miss it."));

            foreach (var condition in move.Requires)
                if (!Dungeons.Rules.RuleVocabulary.Conditions.Contains(condition.Kind))
                    problems.Add(new("moves", $"{move.Id} requires unknown condition '{condition.Kind}'."));

            foreach (var cost in move.Costs)
            {
                if (!poolNames.Contains(cost.Resource) && !gaugeNames.Contains(cost.Resource))
                    problems.Add(new("moves", $"{move.Id} costs unknown resource '{cost.Resource}' (not a pool, not an authored gauge)."));
                if (cost.Amount <= 0)
                    problems.Add(new("moves", $"{move.Id} has a non-positive cost for '{cost.Resource}'."));
            }

            foreach (var packet in move.Packets)
            {
                if (packet.Aspect is { } aspect && !Dungeons.Combat.DamageAspects.All.Contains(aspect))
                    problems.Add(new("moves", $"{move.Id} packet has unknown aspect '{aspect}'."));
                if (packet.Amount <= 0)
                    problems.Add(new("moves", $"{move.Id} has a non-positive packet."));
            }

            foreach (var effect in move.Effects)
            {
                if (!Dungeons.Rules.RuleVocabulary.Effects.Contains(effect.Kind))
                    problems.Add(new("moves", $"{move.Id} rider uses unknown effect '{effect.Kind}'."));

                if (string.Equals(effect.Kind, Dungeons.Rules.RuleVocabulary.ApplyStatus, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(effect.Text) && !content.Statuses.Contains(effect.Text))
                    problems.Add(new("moves", $"{move.Id} applies unknown status '{effect.Text}'."));

                if (effect.Chance is < 0 or > 1)
                    problems.Add(new("moves", $"{move.Id} rider chance {effect.Chance:0.##} is outside 0–1."));

                if (string.Equals(effect.Kind, Dungeons.Rules.RuleVocabulary.TriggerMove, StringComparison.OrdinalIgnoreCase))
                {
                    if (!moves.TryGetById(effect.Text, out var target))
                    {
                        problems.Add(new("moves", $"{move.Id} triggers unknown move '{effect.Text}'."));
                    }
                    else if (target.Effects.Any(e =>
                        string.Equals(e.Kind, Dungeons.Rules.RuleVocabulary.TriggerMove, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(e.Kind, Dungeons.Rules.RuleVocabulary.RecallMove, StringComparison.OrdinalIgnoreCase)))
                    {
                        // The one recursion the proc budget cannot bound on its own, refused
                        // statically: a chain of triggerMoves is authored, not emergent.
                        problems.Add(new("moves", $"{move.Id} triggers '{target.Id}', which can itself trigger a move."));
                    }
                }
            }
        }

        // --- Modifiers ------------------------------------------------------------------------

        foreach (var modifier in content.MoveModifiers.GetAll())
        {
            if (moves.GetAll().All(m => !modifier.Match.Matches(m)))
                problems.Add(new("move_modifiers", $"{modifier.Id} matches no move in the game — dead content."));

            var convertedPerLane = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var op in modifier.Ops)
            {
                if (!Dungeons.Combat.MoveOps.All.Contains(op.Op))
                {
                    problems.Add(new("move_modifiers", $"{modifier.Id} uses unknown op '{op.Op}'."));
                    continue;
                }

                switch (op.Op.ToLowerInvariant())
                {
                    case "convert":
                    case "addasextra":
                        if (op.From is null || op.To is null || !Dungeons.Combat.DamageLanes.All.Contains(op.From)
                            || (!Dungeons.Combat.DamageLanes.All.Contains(op.To) && !Dungeons.Combat.DamageAspects.All.Contains(op.To)))
                            problems.Add(new("move_modifiers", $"{modifier.Id} {op.Op} needs valid from/to lanes."));
                        if (op.Fraction is <= 0 or > 1)
                            problems.Add(new("move_modifiers", $"{modifier.Id} {op.Op} fraction must be in (0, 1] — there is no bare addAspect, and over-conversion is how a strike gets relabelled (D-01)."));
                        if (string.Equals(op.Op, "convert", StringComparison.OrdinalIgnoreCase) && op.From is not null)
                            convertedPerLane[op.From] = convertedPerLane.GetValueOrDefault(op.From) + op.Fraction;
                        break;

                    case "scaletiming" when !Dungeons.Combat.MoveOps.TimingFields.Contains(op.Field):
                        problems.Add(new("move_modifiers", $"{modifier.Id} scaleTiming field '{op.Field}' is not telegraph/windup/recovery."));
                        break;

                    case "setflag" when !Dungeons.Combat.MoveOps.Flags.Contains(op.Field):
                        problems.Add(new("move_modifiers", $"{modifier.Id} setFlag '{op.Field}' is not a known flag."));
                        break;

                    case "addpacket" when op.Packet is null:
                        problems.Add(new("move_modifiers", $"{modifier.Id} addPacket carries no packet."));
                        break;

                    case "addeffect" when op.Effect is null || !Dungeons.Rules.RuleVocabulary.Effects.Contains(op.Effect.Kind):
                        problems.Add(new("move_modifiers", $"{modifier.Id} addEffect carries no valid effect."));
                        break;

                    case "addtag" when !Dungeons.Combat.MoveTags.IsValidOnMove(op.Tag, out var why):
                        problems.Add(new("move_modifiers", $"{modifier.Id} addTag: {why}"));
                        break;
                }
            }

            foreach (var (lane, total) in convertedPerLane)
                if (total > 1.0001)
                    problems.Add(new("move_modifiers", $"{modifier.Id} converts {total:P0} of the {lane} lane — over 100%."));
        }

        // --- Techniques (M2′ acquisition) -----------------------------------------------------
        //
        // A technique that teaches a missing move is a dead item; fail at load, not on Learn.

        foreach (var technique in content.Techniques.GetAll())
        {
            if (string.IsNullOrWhiteSpace(technique.Teaches))
                problems.Add(new("techniques", $"{technique.Id} teaches nothing."));
            else if (!moves.Contains(technique.Teaches))
                problems.Add(new("techniques", $"{technique.Id} teaches unknown move '{technique.Teaches}'."));
        }

        // --- Reachability ---------------------------------------------------------------------
        //
        // Every move must be granted by SOMETHING — a component, a weapon, an actor, a
        // technique item, or an effect. An orphan is content nobody can ever see, which reads
        // as shipped and isn't.

        var reachable = new HashSet<string>(StringComparer.Ordinal);

        foreach (var technique in content.Techniques.GetAll())
            reachable.Add(technique.Teaches);

        foreach (var form in content.Forms.GetAll())
            foreach (var grant in form.Moves)
                reachable.Add(grant.Id);

        foreach (var component in content.Species.GetAll().Cast<CharacterComponentDefinition>()
                     .Concat(content.Classes.GetAll())
                     .Concat(content.Prefixes.GetAll())
                     .Concat(content.Suffixes.GetAll()))
            foreach (var grant in component.Moves)
                reachable.Add(grant.Id);

        foreach (var equip in content.Equipment.GetAll())
            foreach (var grant in equip.Moves)
                reachable.Add(grant.Id);

        foreach (var actor in content.Actors.GetAll())
            foreach (var grant in actor.Moves)
                reachable.Add(grant.Id);

        void CollectFromEffects(IEnumerable<Dungeons.Rules.EffectSpec> effects)
        {
            foreach (var effect in effects)
                if ((string.Equals(effect.Kind, Dungeons.Rules.RuleVocabulary.GrantMove, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(effect.Kind, Dungeons.Rules.RuleVocabulary.TriggerMove, StringComparison.OrdinalIgnoreCase))
                    && !string.IsNullOrEmpty(effect.Text))
                    reachable.Add(effect.Text);
        }

        foreach (var move in moves.GetAll())
            CollectFromEffects(move.Effects);
        foreach (var status in content.Statuses.GetAll())
        {
            CollectFromEffects(status.OnApply);
            CollectFromEffects(status.PerTick);
            CollectFromEffects(status.OnExpire);
        }
        foreach (var prefix in content.Prefixes.GetAll())
        {
            foreach (var rule in prefix.Rules)
                CollectFromEffects(rule.Payload);
            foreach (var feed in prefix.Gauge?.Feeds ?? Enumerable.Empty<Dungeons.Rules.TriggerRule>())
                CollectFromEffects(feed.Payload);
        }
        foreach (var suffix in content.Suffixes.GetAll())
            foreach (var expression in suffix.Expressions)
                CollectFromEffects(expression.Rule.Payload);

        foreach (var move in moves.GetAll())
            if (!reachable.Contains(move.Id))
                problems.Add(new("moves", $"{move.Id} is granted by nothing — orphan content."));
    }

    /// <summary>
    /// Loot tables (M6). The rules here are the ones a typo can silently turn into "this source
    /// drops nothing" — the worst kind of content bug, because nothing throws and the table just
    /// quietly stops paying out.
    ///
    /// <para>Three of them are design rules rather than shape checks, and are the reason this
    /// method exists at all: a nested table must not form a cycle (an infinite roll); an entry
    /// must name exactly one of item/table/nothing (anything else is an author who meant
    /// something and got neither); and a material entry may not restate rarity, because the
    /// material's own <c>rarity:</c> tag is the single source of truth for it.</para>
    /// </summary>
    private static void ValidateLootTables(ContentBundle content, List<ContentProblem> problems)
    {
        var tables = content.LootTables;

        foreach (var table in tables.GetAll())
        {
            void Problem(string message) => problems.Add(new ContentProblem("loot_tables", $"{table.Id}: {message}"));

            if (string.IsNullOrWhiteSpace(table.Name))
                Problem("has no name — loot is player-facing, and the log prints this.");

            if (Loot.LootReachability.FormsCycle(tables, table.Id))
                Problem("reaches itself through a nested table — that roll would never end.");

            if (table.AlwaysDrops.Count == 0 && table.ChanceDrops.Count == 0
                && table.WeightedDraws.Count == 0 && table.Gold is null)
                Problem("has no entries and no gold — it can never pay out.");

            foreach (var entry in table.AlwaysDrops)
                ValidateLootEntry(table.Id, entry, content, requiresWeight: false, problems);
            foreach (var entry in table.ChanceDrops)
            {
                if (entry.Chance is <= 0 or > 1)
                    Problem($"chance drop '{DescribeEntry(entry)}' has chance {entry.Chance}, outside (0, 1].");
                ValidateLootEntry(table.Id, entry, content, requiresWeight: false, problems);
            }

            foreach (var draw in table.WeightedDraws)
            {
                if (draw.Picks < 1)
                    Problem($"a weighted draw picks {draw.Picks} times — it would never fire.");
                if (draw.Picks > Loot.LootTuning.MaxPicksPerDraw)
                    Problem($"a weighted draw picks {draw.Picks} times, above the {Loot.LootTuning.MaxPicksPerDraw} cap.");
                if (draw.Entries.Count == 0)
                    Problem("a weighted draw has no entries.");
                if (draw.Entries.Count > 0 && draw.Entries.All(entry => entry.DropsNothing))
                    Problem("a weighted draw contains nothing but misses.");

                foreach (var entry in draw.Entries)
                    ValidateLootEntry(table.Id, entry, content, requiresWeight: true, problems);
            }

            if (table.Gold is { } gold)
            {
                if (gold.MinAmount < 0 || gold.MaxAmount < 0)
                    Problem($"gold range [{gold.MinAmount}, {gold.MaxAmount}] is negative.");
                if (gold.MinAmount > gold.MaxAmount)
                    Problem($"gold range [{gold.MinAmount}, {gold.MaxAmount}] is inverted.");
                if (gold.MaxAmount == 0)
                    Problem("declares gold but its maximum is 0.");
                if (gold.Chance is <= 0 or > 1)
                    Problem($"gold chance is {gold.Chance}, outside (0, 1].");
            }
        }
    }

    /// <summary>One entry, wherever it lives. <paramref name="requiresWeight"/> is set inside a
    /// weighted draw, where a non-positive weight means the entry can never be picked.</summary>
    private static void ValidateLootEntry(
        string tableId,
        Loot.LootEntryDefinition entry,
        ContentBundle content,
        bool requiresWeight,
        List<ContentProblem> problems)
    {
        void Problem(string message) =>
            problems.Add(new ContentProblem("loot_tables", $"{tableId}: entry '{DescribeEntry(entry)}' {message}"));

        var declared = 0;
        if (!string.IsNullOrEmpty(entry.ItemId)) declared++;
        if (!string.IsNullOrEmpty(entry.TableId)) declared++;
        if (entry.DropsNothing) declared++;

        if (declared != 1)
            Problem("must set exactly one of itemId / tableId / dropsNothing.");

        if (entry.ItemId is { Length: > 0 } itemId && !IsDroppableItem(itemId, content))
            Problem($"names unknown item '{itemId}' (not a material, consumable or technique).");

        if (entry.TableId is { Length: > 0 } nested && !content.LootTables.Contains(nested))
            Problem($"nests unknown loot table '{nested}'.");

        if (entry.MinQuantity < 1)
            Problem($"has minQuantity {entry.MinQuantity}; must be at least 1.");
        if (entry.MinQuantity > entry.MaxQuantity)
            Problem($"has an inverted quantity range [{entry.MinQuantity}, {entry.MaxQuantity}].");

        if (requiresWeight && entry.Weight <= 0)
            Problem($"has weight {entry.Weight} inside a weighted draw; it could never be picked.");

        // The single-source-of-truth rule for rarity. A material states its own; anything else
        // (a technique manual, a schematic, a consumable) has nowhere to state it but here.
        if (entry.Rarity is not null && entry.ItemId is { Length: > 0 } rated
            && content.Materials.TryGetById(rated, out var material)
            && material.Tags.Any(tag => tag.StartsWith("rarity:", StringComparison.OrdinalIgnoreCase)))
            Problem($"declares a rarity, but '{rated}' already carries a rarity: tag — the tag is authoritative.");

        if (entry.When is { } condition)
        {
            if (condition.MinDepth < 0)
                Problem($"has a negative minDepth ({condition.MinDepth}).");
            if (condition.MaxDepth is { } ceiling && ceiling < condition.MinDepth)
                Problem($"has an inverted depth range [{condition.MinDepth}, {ceiling}].");
            foreach (var tag in condition.RequiresTags.Intersect(condition.ExcludesTags, StringComparer.OrdinalIgnoreCase))
                Problem($"both requires and excludes tag '{tag}' — it can never drop.");
        }
    }

    /// <summary>What a loot entry is allowed to name: anything that can sit in a bag as a
    /// stack. Equipment is deliberately absent — D28 says realms drop inputs, and a test
    /// enforces the enemy half of that rule on top of this.</summary>
    private static bool IsDroppableItem(string itemId, ContentBundle content) =>
        content.Materials.Contains(itemId)
        || content.Consumables.Contains(itemId)
        || content.Techniques.Contains(itemId);

    private static string DescribeEntry(Loot.LootEntryDefinition entry) =>
        entry.ItemId ?? entry.TableId ?? (entry.DropsNothing ? "(nothing)" : "(empty)");
}
