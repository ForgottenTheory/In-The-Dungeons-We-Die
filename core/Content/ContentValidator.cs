using Dungeons.Characters;
using Dungeons.Characters.Composition;
using Dungeons.Combat;
using Dungeons.Crafting;
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

    /// <summary>Valid range for the optional authored potency/integrity overrides
    /// (docs/emergent-item-system.md §6). Integrity 0 means destroyed, so it is not authorable.</summary>
    public const int MinPotency = 1;
    public const int MaxPotency = 100;
    public const int MinIntegrity = 1;
    public const int MaxIntegrity = 100;

    /// <summary>Valid ranges for a <see cref="ProcessDefinition"/> (docs/emergent-item-system.md §7).
    /// A channel rate above 1 would mean a single step overshoots the reagent it is converging
    /// toward, which §8.2 exists to make impossible.</summary>
    public const double MinSeverity = 0.0;
    public const double MaxSeverity = 1.0;
    public const double MaxChannelRate = 1.0;
    public const double RoleWeightTolerance = 0.001;

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
        ValidateProcesses(content.Processes, content.Properties, content.Professions, problems);
        ValidateByproducts(content.Byproducts, content.Materials, problems);
        ValidateNameGrammar(content.NameGrammar, content.Properties, problems);
        ValidateModifierKeys(content.ModifierKeys, problems);
        ValidateBases(content.Classes, content.ModifierKeys, problems);
        ValidatePrefixes(content.Prefixes, content.Classes, content.ModifierKeys, problems);
        ValidateSuffixes(content.Suffixes, content.ModifierKeys, problems);
        ValidateActors(content.Actors, content.Moves, content.Materials, problems);
        ValidateMoves(content, problems);
        ValidateProfessionActions(content.Actions, content.Professions, content.Materials, problems);
        ValidateInteractions(content.Interactions, content.Materials, content.Consumables, content.Professions, problems);
        ValidateRealms(content.Realms, content.Actors, content.Actions, content.Materials, content.Consumables, problems);
        ValidateEquipment(content.Equipment, content.Moves, content.MoveModifiers, knownProperties, problems);
        ValidateStatuses(content, problems);
        ValidateComponentMoves(content, problems);

        return problems;
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
            if (material.Potency is { } potency && (potency < MinPotency || potency > MaxPotency))
                problems.Add(new("materials", $"{material.Id} potency override {potency} is outside the {MinPotency}–{MaxPotency} range."));

            if (material.Integrity is { } integrity && (integrity < MinIntegrity || integrity > MaxIntegrity))
                problems.Add(new("materials", $"{material.Id} integrity override {integrity} is outside the {MinIntegrity}–{MaxIntegrity} range."));
        }
    }

    /// <summary>
    /// Validates crafting processes (docs/emergent-item-system.md §7). Processes are the only
    /// authored content the reaction engine needs, so a typo here silently changes the physics
    /// of the whole game rather than breaking one recipe — hence the thorough checks.
    /// </summary>
    private static void ValidateProcesses(
        DataStore<ProcessDefinition> processes,
        DataStore<PropertyDefinition> properties,
        DataStore<ProfessionDefinition> professions,
        List<ContentProblem> problems)
    {
        foreach (var process in processes.GetAll())
        {
            if (!process.IsUngated && !professions.Contains(process.Profession))
                problems.Add(new("processes", $"{process.Id} requires unknown profession '{process.Profession}'."));

            if (process.Severity is < MinSeverity or > MaxSeverity)
                problems.Add(new("processes", $"{process.Id} severity {process.Severity:0.##} is outside the {MinSeverity:0}–{MaxSeverity:0} range."));

            if (process.EssenceRate is < 0.0 or > 1.0)
                problems.Add(new("processes", $"{process.Id} essence_rate {process.EssenceRate:0.##} is outside the 0–1 range."));

            ValidateRoleWeights(process, problems);
            ValidateChannel(process, properties, problems);
            ValidateProcessTagEffects(process, problems);

            foreach (var tag in process.Requires.SubstrateTags)
                ValidateProcessTag(process, tag, "requires.substrate_tags", allowWildcard: false, problems);

            if (process.Requires.ProfessionLevel < 0)
                problems.Add(new("processes", $"{process.Id} requires a negative profession level."));

            if (process.IsUngated && process.Requires.ProfessionLevel > 0)
                problems.Add(new("processes", $"{process.Id} is ungated but requires profession level {process.Requires.ProfessionLevel}, which can never be met."));
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

    private static void ValidateRoleWeights(ProcessDefinition process, List<ContentProblem> problems)
    {
        var weights = process.RoleWeights;

        if (weights.Substrate < 0.0 || weights.Reagent < 0.0 || weights.Catalyst < 0.0)
            problems.Add(new("processes", $"{process.Id} has a negative role weight."));

        // Potency is a weighted mean (§6.1); weights that don't sum to 1 would let a process
        // inflate or deflate potency for free, which is the exploit the mean exists to close.
        if (Math.Abs(weights.Total - 1.0) > RoleWeightTolerance)
            problems.Add(new("processes", $"{process.Id} role_weights sum to {weights.Total:0.###}, not 1.0."));
    }

    private static void ValidateChannel(
        ProcessDefinition process,
        DataStore<PropertyDefinition> properties,
        List<ContentProblem> problems)
    {
        if (process.Channel.Count == 0)
        {
            problems.Add(new("processes", $"{process.Id} opens no channel, so it could never change anything."));
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in process.Channel)
        {
            if (!seen.Add(entry.Property))
                problems.Add(new("processes", $"{process.Id} lists channel property '{entry.Property}' twice."));

            if (entry.Rate is <= 0.0 or > MaxChannelRate)
                problems.Add(new("processes", $"{process.Id} channel '{entry.Property}' rate {entry.Rate:0.##} is outside the 0–{MaxChannelRate:0} range (exclusive of 0)."));

            if (!properties.TryGetById(entry.Property, out var property))
            {
                problems.Add(new("processes", $"{process.Id} opens unknown property '{entry.Property}' (typo, or add it to game/data/properties/)."));
                continue;
            }

            // §2.3: Response properties are derived outputs and Sourcing describes how hard the
            // material was to obtain — neither may ever be a reaction input.
            if (property.Role is PropertyRole.Response or PropertyRole.Sourcing)
                problems.Add(new("processes", $"{process.Id} opens '{entry.Property}', which is a {property.Role} property and can never be a reaction input."));
        }
    }

    private static void ValidateProcessTagEffects(ProcessDefinition process, List<ContentProblem> problems)
    {
        foreach (var tag in process.TagEffects.Set)
            ValidateProcessTag(process, tag, "tag_effects.set", allowWildcard: false, problems);

        foreach (var tag in process.TagEffects.Clear)
            ValidateProcessTag(process, tag, "tag_effects.clear", allowWildcard: true, problems);

        // Setting and clearing the same exact tag is always an authoring mistake; a family
        // wildcard clear alongside a set in that family is the normal "replace" idiom.
        foreach (var tag in process.TagEffects.Set.Intersect(process.TagEffects.Clear, StringComparer.Ordinal))
            problems.Add(new("processes", $"{process.Id} both sets and clears tag '{tag}'."));
    }

    private static void ValidateProcessTag(
        ProcessDefinition process,
        string tag,
        string field,
        bool allowWildcard,
        List<ContentProblem> problems)
    {
        if (!TagFamilies.TryParse(tag, out var family, out var value))
        {
            problems.Add(new("processes", $"{process.Id} {field} has un-namespaced tag '{tag}' (expected family:value)."));
            return;
        }

        if (!TagFamilies.TryGet(family, out var def))
        {
            problems.Add(new("processes", $"{process.Id} {field} tag '{tag}' uses unknown family '{family}'."));
            return;
        }

        if (value == ProcessTagEffects.ClearFamilyWildcard)
        {
            if (!allowWildcard)
                problems.Add(new("processes", $"{process.Id} {field} may not use the '{family}:*' wildcard."));
            return;
        }

        if (def.ClosedValues is not null && !def.ClosedValues.Contains(value))
            problems.Add(new("processes", $"{process.Id} {field} tag '{tag}' is not a valid '{family}' value."));
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
        DataStore<MaterialDefinition> materials,
        List<ContentProblem> problems)
    {
        foreach (var actor in actors.GetAll())
        {
            foreach (var grant in actor.Moves)
                if (!moves.Contains(grant.Id))
                    problems.Add(new("actors", $"{actor.Id} grants unknown move '{grant.Id}'."));

            if (actor.Moves.Count == 0)
                problems.Add(new("actors", $"{actor.Id} has no moves — it would stand there doing nothing."));

            // The AI profile chooses among the actor's own moves, with the shared condition
            // vocabulary. A rule naming a move the actor doesn't have is dead weight forever.
            foreach (var rule in actor.Ai)
            {
                if (actor.Moves.All(m => !string.Equals(m.Id, rule.Move, StringComparison.Ordinal)))
                    problems.Add(new("actors", $"{actor.Id} AI selects '{rule.Move}', which the actor does not have."));

                if (rule.Weight <= 0)
                    problems.Add(new("actors", $"{actor.Id} AI rule for '{rule.Move}' has non-positive weight."));

                foreach (var condition in rule.When)
                    if (!Dungeons.Rules.RuleVocabulary.Conditions.Contains(condition.Kind))
                        problems.Add(new("actors", $"{actor.Id} AI uses unknown condition '{condition.Kind}'."));
            }

            if (!string.IsNullOrEmpty(actor.LootItemId) && !materials.Contains(actor.LootItemId))
                problems.Add(new("actors", $"{actor.Id} drops unknown material '{actor.LootItemId}'."));

            foreach (var lane in actor.Resistances.Keys)
                if (!DamageLanes.All.Contains(lane))
                    problems.Add(new("actors",
                        $"{actor.Id} resists unknown lane '{lane}'. Valid lanes: {string.Join(", ", DamageLanes.All)}."));

            // Vulnerability is keyed by damage TYPE (D-02) — the one place the three physical
            // types still matter defensively, so a lane name here is a real mistake.
            foreach (var (type, multiplier) in actor.Vulnerable)
            {
                if (!Enum.TryParse<DamageType>(type, ignoreCase: true, out _))
                    problems.Add(new("actors",
                        $"{actor.Id} is vulnerable to unknown damage type '{type}'. Valid: {string.Join(", ", Enum.GetNames<DamageType>())}."));

                if (multiplier < CombatTuning.MinVulnerability || multiplier > CombatTuning.MaxVulnerability)
                    problems.Add(new("actors",
                        $"{actor.Id} vulnerability '{type}' is {multiplier}, outside the allowed " +
                        $"[{CombatTuning.MinVulnerability}, {CombatTuning.MaxVulnerability}] range."));
            }
        }
    }

    private static void ValidateProfessionActions(
        DataStore<ProfessionActionDefinition> actions,
        DataStore<ProfessionDefinition> professions,
        DataStore<MaterialDefinition> materials,
        List<ContentProblem> problems)
    {
        foreach (var action in actions.GetAll())
        {
            if (!professions.Contains(action.ProfessionId))
                problems.Add(new("profession_actions", $"{action.Id} references unknown profession '{action.ProfessionId}'."));

            foreach (var io in action.Inputs.Concat(action.Outputs))
                if (!materials.Contains(io.ItemId))
                    problems.Add(new("profession_actions", $"{action.Id} references unknown material '{io.ItemId}'."));

            foreach (var bonus in action.BonusOutputs)
                if (!materials.Contains(bonus.ItemId))
                    problems.Add(new("profession_actions", $"{action.Id} bonus output references unknown material '{bonus.ItemId}'."));
        }
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
        DataStore<MaterialDefinition> materials,
        DataStore<ConsumableDefinition> consumables,
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
                        // A reward may be a stackable material or a consumable item.
                        if (!string.IsNullOrEmpty(loc.RewardItemId)
                            && !materials.Contains(loc.RewardItemId) && !consumables.Contains(loc.RewardItemId))
                            problems.Add(new("realms", $"{realm.Id}/{loc.Id} rewards unknown item '{loc.RewardItemId}'."));
                        break;
                }
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
            switch (def.Slot)
            {
                // Weapon-granted moves are mandatory, not optional — the Fighter's identity is
                // "moveset comes from the weapon" (docs/moves.md §5.1).
                case EquipmentSlot.Weapon when def.Moves.Count == 0:
                    problems.Add(new("equipment", $"{def.Id} is a Weapon but grants no moves."));
                    break;
                case EquipmentSlot.Armor when def.Armor is null:
                    problems.Add(new("equipment", $"{def.Id} is Armor but has no armor stats block."));
                    break;
            }

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
    /// structural ones: no triggerMove may target a move that can itself triggerMove, and every
    /// move must be reachable from some source (orphan content is content nobody can ever see).
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
        var pools = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "health", "stamina", "mana" };

        foreach (var move in moves.GetAll())
        {
            foreach (var tag in move.Tags)
                if (!Dungeons.Combat.MoveTags.IsValidOnMove(tag, out var why))
                    problems.Add(new("moves", $"{move.Id}: {why}"));

            if (!move.Tags.Any(t => t.StartsWith("action:", StringComparison.OrdinalIgnoreCase)))
                problems.Add(new("moves", $"{move.Id} carries no action: tag — CanAct gating and every action-scoped hook would miss it."));

            foreach (var condition in move.Requires)
                if (!Dungeons.Rules.RuleVocabulary.Conditions.Contains(condition.Kind))
                    problems.Add(new("moves", $"{move.Id} requires unknown condition '{condition.Kind}'."));

            foreach (var cost in move.Costs)
            {
                if (!pools.Contains(cost.Resource) && !gaugeNames.Contains(cost.Resource))
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
}
