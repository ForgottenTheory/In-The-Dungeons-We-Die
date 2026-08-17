using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dungeons.Characters;
using Dungeons.Characters.Composition;
using Dungeons.Characters.Rules;
using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Events;
using Dungeons.Game.Infrastructure;
using Dungeons.Items;
using Dungeons.Persistence;
using Dungeons.Presentation;
using Dungeons.Professions;
using Dungeons.Randomness;
using Dungeons.Realms;
using Dungeons.Rules;
using Dungeons.Simulation;
using Godot;

namespace Dungeons.Game;

/// <summary>
/// Composition root autoload. It constructs the engine-independent Core services,
/// loads data-driven content through the Godot file bridge, and exposes a small
/// application-facing surface to the UI. It wires systems together — it must not
/// grow gameplay rules of its own (see docs/godot-ui-mvp.md §24).
/// </summary>
public partial class GameRoot : Node
{
    private static readonly AttributeSet Baseline = AttributeSet.Uniform(5);

    /// <summary>Simulation ticks advanced per real second while running.</summary>
    public const int TicksPerSecond = 20;

    /// <summary>Debug rotation through the fully-expressed suffixes — the ones that actually
    /// do something on every channel today.</summary>
    private static readonly string[] SuffixCycle =
    {
        "suffix.unreasonable_confidence",
        "suffix.exploding_kneecaps",
        "suffix.the_last_laugh",
        "suffix.mandatory_overtime",
        "suffix.absolutely_no_refunds",
    };

    private readonly TickEngine _tick = new();
    private readonly SaveStore _saveStore = new();
    private readonly Inventory _stash = new();
    private readonly LearnedMoves _learnedMoves = new();

    private ContentBundle _content = new();
    private DataStore<MaterialDefinition> _materials = new();
    private DataStore<ProfessionDefinition> _professionDefs = new();
    private DataStore<ProfessionActionDefinition> _actionDefs = new();

    private DataStore<CraftingInteractionDefinition> _interactions = new();
    private DataStore<MoveDefinition> _moves = new();
    private DataStore<MoveModifierDefinition> _moveModifierStore = new();
    private DataStore<ActorDefinition> _actors = new();
    private DataStore<RealmDefinition> _realms = new();
    private DataStore<ConsumableDefinition> _consumables = new();
    private DataStore<TechniqueDefinition> _techniques = new();
    private DataStore<EnemyFamilyDefinition> _enemyFamilies = new();
    private DataStore<CombatRoleDefinition> _enemyRoles = new();
    private DataStore<AiProfileDefinition> _aiProfiles = new();
    private DataStore<EquipmentDefinition> _equipment = new();

    private readonly Equipment _playerEquipment = new();
    private readonly InstanceIdSource _instanceIds = new();

    private const string StarterWeaponId = "equip.rusty_sword";
    private const string StarterArmorId = "equip.tattered_armor";

    private CharacterComposer _composer = null!;
    private ProfessionSystem _professions = null!;
    private PassiveProfessionRunner _passiveRunner = null!;
    private DiscoverySystem _discoveries = null!;
    private CraftingExperimentSystem _crafting = null!;
    private IEmergentRegistry _emergentRegistry = null!;
    private FabricationEngine _fabrication = null!;
    private IReactionEngine _reactions = null!;
    private MaterialProfileResolver _profiles = null!;
    private PropertyGlossary _glossary = null!;
    private SeededRandom _affixRng = null!;
    private BuildResolver _buildResolver = null!;
    private CombatEncounter _encounter = null!;
    private bool _everFought;

    /// <summary>
    /// The one event bus. Combat publishes to it; <see cref="_ruleEngine"/> listens. Synchronous
    /// and ordered by design (DECISIONS D23) — the simulation must replay from a seed.
    /// </summary>
    private readonly GameEventBus _events = new();
    private TriggerRuleEngine _ruleEngine = null!;
    private StatusController _statuses = null!;

    /// <summary>
    /// The build's live gauges. One long-lived controller reconfigured on every rebuild, so the
    /// encounter can hold a stable reference while the Character Lab swaps components.
    /// </summary>
    private readonly GaugeController _gauges = new(Array.Empty<GaugeDefinition>());
    private CombatantModifiers _modifiers = null!;
    private IConditionWorld? _conditionWorld;

    private RealmRun? _run;
    private string? _realmCombatLocationId;
    private readonly Dictionary<string, int> _realmKnowledge = new();

    /// <summary>Where loot currently flows: the run inventory in a Realm, else the Stash.</summary>
    private Inventory CurrentBag => _run is { Active: true } ? _run.RunInventory : _stash;

    private CharacterBuild _build = new(
        new SpeciesId("species.fey_touched"), new BaseClassId("class.wizard"),
        new PrefixId("prefix.galvanic"), new SuffixId(SuffixCycle[0]));
    private int _suffixIndex;

    private bool _running;
    private double _tickAccumulator;

    public event Action<string>? LogEmitted;
    public event Action? CharacterChanged;
    public event Action? InventoryChanged;
    public event Action? RunningChanged;
    public event Action? DiscoveryChanged;
    public event Action? CombatChanged;
    public event Action? RealmChanged;

    public long CurrentTick => _tick.CurrentTick;
    public bool IsRunning => _running;
    public Character? Character { get; private set; }

    public override void _Ready()
    {
        var content = ContentLoader.LoadAll("res://data");
        ValidateContentOrThrow(content);

        _content = content;
        _materials = content.Materials;
        _professionDefs = content.Professions;
        _actionDefs = content.Actions;
        _interactions = content.Interactions;
        _moves = content.Moves;
        _moveModifierStore = content.MoveModifiers;
        _actors = content.Actors;
        _realms = content.Realms;
        _consumables = content.Consumables;
        _techniques = content.Techniques;
        _enemyFamilies = content.EnemyFamilies;
        _enemyRoles = content.EnemyRoles;
        _aiProfiles = content.AiProfiles;
        _equipment = content.Equipment;

        var rules = new RuleRegistry(new ICharacterRule[]
        {
            new UnreasonableConfidenceRule(),
            new InappropriateOptimismRule(),
        });
        _composer = new CharacterComposer(content.Species, content.Classes, content.Prefixes, content.Suffixes, rules);
        _buildResolver = new BuildResolver(content);

        // The build's Prefix/Suffix/gauge hooks become live here. Until E3 registers effect
        // handlers nothing they *do* lands, but every rule that fires is recorded — which is
        // exactly the point of E0: the class system stops being theoretical.
        // The world provider is attached after the encounter exists (below); rules attach here so
        // the build's hooks are live from the first frame.
        _ruleEngine = new TriggerRuleEngine(
            _events, new SeededRandom(0x21FE5), () => _tick.CurrentTick,
            new DeferredConditionWorld(() => _conditionWorld));

        RebuildCharacter();
        EquipStarterLoadout();

        // Gathering deposits into the current bag: the Stash in the Hideout, the run
        // inventory while in a Realm (unsecured until extraction).
        _professions = new ProfessionSystem(_actionDefs, () => CurrentBag, new SeededRandom(20260814));
        _passiveRunner = new PassiveProfessionRunner(_tick, _professions);
        _professions.ActionCompleted += OnActionCompleted;
        _professions.LeveledUp += OnLeveledUp;
        _passiveRunner.Stalled += OnPassiveStalled;
        _stash.Changed += () => InventoryChanged?.Invoke();
        _playerEquipment.Changed += () => CharacterChanged?.Invoke();

        // Crafting happens in the Hideout, against the Stash (field crafting deferred).
        _discoveries = new DiscoverySystem();
        _crafting = new CraftingExperimentSystem(
            _interactions, _materials, _stash, _discoveries,
            professionLevel: id => _professions.GetProgress(id).Level,
            instanceIds: _instanceIds);
        _discoveries.Discovered += OnDiscovered;

        // The emergent crafting engine (docs/emergent-item-system.md P1). It replaces recipe
        // matching entirely; the interaction system above survives only to keep the Healing
        // Salve brewable until fabrication lands in P5c.
        _profiles = new MaterialProfileResolver(content.Properties);
        _glossary = new PropertyGlossary(content.Properties);
        _emergentRegistry = new EmergentRegistry(_materials);
        _reactions = new ReactionEngine(
            content,
            () => CurrentBag,
            _profiles,
            _emergentRegistry,
            new NameGenerator(_materials, content.Properties, content.NameGrammar),
            new TagDeriver(content.Properties),
            new ByproductResolver(content.Byproducts),
            new TraitResolver(content.Traits),
            professionLevel: id => _professions.GetProgress(id).Level,
            new SeededRandom(0xC12AF7));

        _affixRng = new SeededRandom(0xD1CE5);
        _fabrication = new FabricationEngine(content, () => CurrentBag, _profiles, _instanceIds, _affixRng);

        var combatRng = new SeededRandom(0x0C0FFEE);
        _statuses = new StatusController(content.Statuses, _events, () => _tick.CurrentTick);

        // E3c-2: the modifier read path. Build statics, status `while_active`, gauge bands and
        // timed grants all assemble here, and combat reads them. Every one of those four was
        // authored and inert before this.
        _modifiers = new CombatantModifiers(
            content.ModifierKeys,
            isOwner: c => c.Team == CombatTeam.Player,
            // Build statics plus whatever the worn items' affixes grant (R4b) — equipment is
            // just another contribution source, with per-affix provenance.
            buildModifiers: () => _buildResolver.Resolve(_build).Modifiers.Contributions
                .Concat(EquippedAffixContributions()),
            _statuses, _gauges);

        _encounter = new CombatEncounter(
            _tick, new HitPipeline(combatRng, _modifiers), _moves, combatRng, _events,
            _statuses, _gauges, _modifiers, _moveModifierStore);

        // E3c: effects stop landing in `Unhandled`. Eleven kinds are combat's (E4 added the
        // move-granting four); the rest belong to systems that do not exist yet and stay
        // visibly inert. This also hands the encounter its effect sink for move riders.
        _ruleEngine.RegisterCombatHandlers(_encounter, combatRng);

        // E3c-3: the stateful conditions get something to ask. Equipped tags come from the worn
        // items' definitions, so `equippedTag` reads what the player is actually wearing.
        _conditionWorld = new CombatConditionWorld(_encounter, EquippedTags);
        _encounter.ConditionWorld = _conditionWorld;
        _encounter.Logged += Emit;
        _encounter.StateChanged += () => CombatChanged?.Invoke();
        _encounter.Ended += OnCombatEnded;
        _encounter.HitResolved += OnHitResolved;

        GD.Print($"[GameRoot] Ready. {_materials.Count} materials, {_professionDefs.Count} professions, {_actionDefs.Count} actions, {_interactions.Count} interactions.");
    }

    /// <summary>
    /// Runs load-time cross-reference validation over the freshly-loaded content stores
    /// and fails loudly if anything is broken, so a bad reference is caught at startup
    /// rather than as a mid-play <see cref="KeyNotFoundException"/> (ROADMAP Phase 4).
    /// </summary>
    private void ValidateContentOrThrow(ContentBundle content)
    {
        var problems = ContentValidator.Validate(content);

        if (problems.Count == 0)
            return;

        foreach (var problem in problems)
            GD.PushError($"[Content] {problem}");

        throw new ContentValidationException(problems);
    }

    public override void _Process(double delta)
    {
        if (!_running)
            return;

        _tickAccumulator += delta * TicksPerSecond;
        var guard = 0;
        while (_tickAccumulator >= 1.0 && guard++ < 10000)
        {
            _tick.Advance(1);
            _tickAccumulator -= 1.0;
        }
    }

    // --- Simulation ---------------------------------------------------------

    public void SetRunning(bool running)
    {
        if (_running == running)
            return;
        _running = running;
        _tickAccumulator = 0;
        Emit(running ? "[Sim] Running." : "[Sim] Paused.");
        RunningChanged?.Invoke();
    }

    public void AdvanceTick(long ticks) => _tick.Advance(ticks);

    // --- Character Lab -------------------------------------------------------
    //
    // Thin forwards over Core's BuildResolver. Every rule about how Base + Prefix + Suffix
    // compose lives there; this just exposes it and formats the report.

    public IReadOnlyList<BaseClassDefinition> Bases =>
        _content.Classes.GetAll().OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase).ToList();

    public IReadOnlyList<PrefixDefinition> PrefixCatalog =>
        _content.Prefixes.GetAll().OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();

    public IReadOnlyList<SuffixDefinition> SuffixCatalog =>
        _content.Suffixes.GetAll()
            .OrderByDescending(s => s.IsFullyExpressed)   // the ones that do something, first
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public CharacterBuild CurrentBuild => _build;

    public ResolvedBuild ResolveBuild(CharacterBuild build) => _buildResolver.Resolve(build);

    /// <summary>Swaps one or more components and rebuilds. Null leaves that slot alone.</summary>
    public void SetBuild(string? baseId = null, string? prefixId = null, string? suffixId = null)
    {
        var previous = _buildResolver.Resolve(_build);

        _build = _build with
        {
            BaseClassId = baseId is null ? _build.BaseClassId : new BaseClassId(baseId),
            PrefixId = prefixId is null ? _build.PrefixId : new PrefixId(prefixId),
            SuffixId = suffixId is null ? _build.SuffixId : new SuffixId(suffixId),
        };

        RebuildCharacter();

        foreach (var change in BuildResolver.Diff(previous, _buildResolver.Resolve(_build)))
            Emit("  " + change);
    }

    /// <summary>Full readout of the current build, for the Lab.</summary>
    public string BuildReport()
    {
        var build = _buildResolver.Resolve(_build);
        var sb = new StringBuilder();

        sb.AppendLine(build.Name);
        sb.AppendLine();
        sb.AppendLine($"Engine     {build.Base.Engine}");
        sb.AppendLine($"Weakness   {build.Base.Weakness}");
        sb.AppendLine($"Resource   {build.Base.PrimaryResource}    Channel  {build.Channel}");
        sb.AppendLine();

        sb.AppendLine("Growth per level (budget " + AttributeGrowth.BudgetPerLevel.ToString("0.#") + ")");
        foreach (var (attribute, weight) in build.GrowthPerLevel.OrderByDescending(p => p.Value))
            sb.AppendLine($"  {attribute,-13} {weight,5:0.###}   → +{build.GrowthAt(21)[attribute]} by L21");
        sb.AppendLine();

        sb.AppendLine(build.Gauges.Count == 0
            ? "Gauges     (none — this Base runs without a meter)"
            : "Gauges");
        foreach (var gauge in build.Gauges)
            sb.AppendLine($"  {gauge.Name,-12} {gauge.Behaviour}, max {gauge.Max:0}, {gauge.Feeds.Count} feed(s), {gauge.Bands.Count} band(s)");
        sb.AppendLine();

        sb.AppendLine($"Hooks ({build.Rules.Count})");
        foreach (var rule in build.Rules)
            sb.AppendLine($"  {rule.Origin,-28} on {rule.Rule.Event} → {string.Join(" + ", rule.Rule.Payload.Select(e => e.Kind))}");

        if (build.Suffix is { } suffix)
        {
            sb.AppendLine();
            sb.AppendLine($"Suffix     {suffix.Fantasy}");
            sb.AppendLine(suffix.IsFullyExpressed
                ? $"           {suffix.For(build.Channel)!.Drawback}"
                : "           (roster entry — no mechanics authored yet)");
        }

        // E0: hooks are live. Until E3 registers effect handlers, firing and landing in
        // Unhandled is the expected outcome — the point is that it is now *visible*.
        sb.AppendLine();
        sb.AppendLine($"Live hooks  {_ruleEngine.Fired.Count} fired  ·  {_ruleEngine.Unhandled.Count} awaiting a handler");
        foreach (var recent in _ruleEngine.Fired.TakeLast(6))
            sb.AppendLine($"  fired  {recent.Source,-22} {recent.Trigger.Kind} → {recent.Kind}");

        return sb.ToString().TrimEnd();
    }

    // --- Character ----------------------------------------------------------

    public void CycleSuffix()
    {
        _suffixIndex = (_suffixIndex + 1) % SuffixCycle.Length;
        _build = _build with { SuffixId = new SuffixId(SuffixCycle[_suffixIndex]) };
        RebuildCharacter();
    }

    public void DamageCharacterPercent(double fraction)
    {
        if (Character is null)
            return;
        var dealt = Character.TakeDamage((int)Math.Round(Character.Health.Max * fraction));
        Emit($"[Character] Took {dealt} damage (Health {Character.Health.Current}/{Character.Health.Max}).");
        CharacterChanged?.Invoke();
    }

    public void HealCharacterFull()
    {
        if (Character is null)
            return;
        Character.RestoreAll();
        Emit("[Character] Restored to full.");
        CharacterChanged?.Invoke();
    }

    // --- Professions --------------------------------------------------------

    public IReadOnlyList<ProfessionActionDefinition> Actions =>
        _actionDefs.GetAll().OrderBy(a => a.ProfessionId).ThenBy(a => a.Id).ToList();

    public string ProfessionName(string professionId) =>
        _professionDefs.TryGetById(professionId, out var def) ? def.Name : professionId;

    public bool IsPassiveRunning => _passiveRunner.IsRunning;
    public string? CurrentPassiveActionId => _passiveRunner.CurrentActionId;
    public double PassiveProgress => _passiveRunner.Progress();

    public void StartPassive(string actionId)
    {
        if (_passiveRunner.Start(actionId))
        {
            Emit($"[Passive] Started {ActionName(actionId)}.");
            SetRunning(true); // passive gathering only advances while the sim runs
        }
        else
        {
            Emit($"[Passive] Cannot start {ActionName(actionId)} ({_professions.CheckExecutable(actionId)}).");
        }
    }

    public void StopPassive()
    {
        if (!_passiveRunner.IsRunning)
            return;
        var name = ActionName(_passiveRunner.CurrentActionId!);
        _passiveRunner.Stop();
        Emit($"[Passive] Stopped {name}.");
    }

    public void ActiveAttempt(string actionId, double performance)
    {
        var outcome = _professions.Execute(actionId, performance, isActive: true);
        if (!outcome.Success)
        {
            Emit($"[Active] {ActionName(actionId)} failed ({outcome.Failure}).");
            return;
        }

        Emit($"[Active] {ActionName(actionId)} (timing {performance:P0}) → {DescribeProduced(outcome)} (xp +{outcome.XpGained}).");
    }

    public string ProfessionSummary()
    {
        var sb = new StringBuilder();
        foreach (var def in _professionDefs.GetAll().OrderBy(p => p.Name))
        {
            var progress = _professions.GetProgress(def.Id);
            sb.AppendLine($"{def.Name,-10} L{progress.Level}  (xp {progress.Xp}, {progress.ProgressToNextLevel:P0} to next)");
        }

        return sb.ToString().TrimEnd();
    }

    public string InventoryReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("STASH (secured):");
        sb.AppendLine(FormatBag(_stash));

        if (InRealm)
        {
            sb.AppendLine();
            sb.AppendLine("UNSECURED — lost if you die:");
            sb.AppendLine(FormatBag(_run!.RunInventory));
        }

        return sb.ToString().TrimEnd();
    }

    private string FormatBag(Inventory bag)
    {
        var lines = bag.Snapshot().OrderBy(s => s.ItemId)
            .Select(s => $"  {ItemName(s.ItemId),-16} x{s.Quantity}")
            .ToList();

        foreach (var instance in bag.Instances.OrderBy(i => i.DisplayName))
            lines.Add("  " + ItemLabel(instance));

        return lines.Count == 0 ? "  (empty)" : string.Join("\n", lines);
    }

    // --- Emergent crafting ---------------------------------------------------
    //
    // These are thin forwards. Every rule lives in Core's ReactionEngine; GameRoot only turns
    // an outcome into log lines and change events, so the flagged Application-layer extraction
    // does not get any harder than it already is.

    /// <summary>Every process the player can choose between, gentlest first.</summary>
    public IReadOnlyList<ProcessDefinition> Processes =>
        _content.Processes.GetAll().OrderBy(p => p.Severity).ToList();

    /// <summary>
    /// Materials currently on hand, for the crafting pickers. Emergent archetypes appear here
    /// alongside authored ones with no special-casing — that is the whole point of registering
    /// them into the same store (DECISIONS D20).
    /// </summary>
    public IReadOnlyList<(string Id, string Name, int Quantity)> MaterialsOnHand =>
        CurrentBag.Snapshot()
            .Where(s => _materials.Contains(s.ItemId))
            .Select(s => (s.ItemId, _materials.GetById(s.ItemId).Name, s.Quantity))
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>A material's emergent profile, for the crafting inspector. Null if unknown.</summary>
    /// <summary>The material inspector, in the player crafting language (D30). Thin forward —
    /// the reading and the wording live in Core (<c>MaterialReadings</c>/<c>SemanticFormat</c>).</summary>
    public string MaterialSummary(string materialId)
    {
        if (!_materials.TryGetById(materialId, out var material))
            return string.Empty;

        var reading = MaterialReadings.From(
            material, _profiles.Resolve(material), _content.Properties, _content.Traits, _content.Essences);
        return SemanticFormat.Material(reading, _glossary);
    }

    /// <summary>The same inspector in the numeric voice — the §2F Advanced toggle's text.</summary>
    public string MaterialSummaryAdvanced(string materialId) =>
        _materials.TryGetById(materialId, out var material)
            ? AdvancedFormat.Material(material, _profiles.Resolve(material))
            : string.Empty;

    /// <summary>A process picker line in the player language (D30).</summary>
    public string ProcessLabel(ProcessDefinition process) =>
        SemanticFormat.Process(process, ProfessionName(process.Profession));

    /// <summary>What a process drives, in words — the channel line under the picker.</summary>
    public string ProcessChannelLabel(ProcessDefinition process) =>
        SemanticFormat.Channel(process, _glossary);

    /// <summary>The pre-commit reading (D30): groups, risk band, emergence — built from the
    /// projection's typed movements. The UI styles it; every word comes from Core.</summary>
    public CraftReading ProjectionReading(CraftProjection projection, string substrateId) =>
        _materials.TryGetById(substrateId, out var substrate)
            ? CraftReadings.From(projection, substrate.Name, _profiles.Resolve(substrate), _content)
            : CraftReadings.Failed(CraftFailure.UnknownSubstrate, substrateId);

    /// <summary>The reading as typed lines the client colours by kind.</summary>
    public IReadOnlyList<ProjectionLine> ProjectionLines(CraftReading reading) =>
        SemanticFormat.ProjectionLines(reading, _glossary);

    /// <summary>The pre-commit panel text in the player language (D30).</summary>
    public string ProjectionText(CraftProjection projection, string substrateId) =>
        SemanticFormat.Projection(ProjectionReading(projection, substrateId), _glossary);

    /// <summary>The compact glyph+pips strip for a picker row ("▲●●●●●  !●●●●○").</summary>
    public string MaterialStrip(string materialId)
    {
        if (!_materials.TryGetById(materialId, out var material))
            return string.Empty;

        var reading = MaterialReadings.From(
            material, _profiles.Resolve(material), _content.Properties, _content.Traits, _content.Essences);
        return SemanticFormat.MaterialStrip(reading, _glossary);
    }

    /// <summary>The pre-commit panel in the numeric voice (§2F Advanced).</summary>
    public string ProjectionTextAdvanced(CraftProjection projection, string substrateId) =>
        AdvancedFormat.Projection(
            projection, _materials.TryGetById(substrateId, out var substrate) ? substrate.Name : substrateId);

    /// <summary>
    /// What a craft would cost and risk, <b>before</b> committing to it
    /// (docs/emergent-item-system.md §6.2c). Integrity 0 destroys the material, so the UI must
    /// always show this first.
    /// </summary>
    public CraftProjection ProjectCraft(string processId, string substrateId, IReadOnlyList<string> reagentIds, string? catalystId = null) =>
        _reactions.Project(new CraftRequest(processId, substrateId, reagentIds, catalystId));

    /// <summary>Runs a craft and reports it. Order of reagents is the mechanic (§0 Decision 2).</summary>
    public CraftOutcome Craft(string processId, string substrateId, IReadOnlyList<string> reagentIds, string? catalystId = null)
    {
        var outcome = _reactions.Resolve(new CraftRequest(processId, substrateId, reagentIds, catalystId));

        if (!outcome.Success)
        {
            Emit("[Craft] " + CraftFormat.Failure(outcome.Failure));
            return outcome;
        }

        foreach (var entry in outcome.Log.Entries)
            Emit("  " + new string(' ', entry.Indent * 2) + entry.Text);

        if (outcome.WasDestroyed)
            Emit($"[Craft] {outcome.ResultName} was destroyed. Recovered: {DescribeStacks(outcome.Byproducts)}.");
        else if (outcome.IsFirstDiscovery)
            Emit($"[Craft] First discovery — {outcome.ResultName} ×{outcome.Quantity}!");
        else
            Emit($"[Craft] Made {outcome.ResultName} ×{outcome.Quantity}.");

        InventoryChanged?.Invoke();
        DiscoveryChanged?.Invoke();
        return outcome;
    }

    private string DescribeStacks(IReadOnlyList<ItemStack> stacks) =>
        stacks.Count == 0
            ? "nothing"
            : string.Join(", ", stacks.Select(s => $"{ItemName(s.ItemId)} ×{s.Quantity}"));

    /// <summary>The emergent materials this save has produced (§12.4).</summary>
    public IReadOnlyCollection<MaterialDefinition> DiscoveredArchetypes => _emergentRegistry.All;

    // --- Legacy fixed-interaction crafting -----------------------------------
    //
    // Superseded by the reaction engine. Only the Healing Salve recipe remains, because
    // consumables are produced by fabrication (P5c) and there is no emergent path to one yet.
    // Delete this whole section when P5c lands.

    /// <summary>Brews a Healing Salve from gathered herbs — the crafted Realm supply.</summary>
    public void BrewHealingSalve() => Experiment("material.sageleaf");

    public void Experiment(params string[] itemIds)
    {
        var outcome = _crafting.Experiment(itemIds);
        if (outcome.Success)
        {
            var props = outcome.ResultProperties.Count > 0
                ? " (" + string.Join(", ", outcome.ResultProperties.Select(p => $"{p.Property} {p.Value:0.##}")) + ")"
                : string.Empty;
            var kind = outcome.ProducedInstance is not null ? " [instance]" : string.Empty;
            Emit($"[Craft] Made {outcome.ResultQuantity} {ItemName(outcome.ResultItemId!)}{kind}{props}.");
            return;
        }

        Emit(outcome.Failure switch
        {
            ExperimentFailure.ProfessionTooLow =>
                $"[Craft] Failed — needs {ProfessionName(outcome.UnmetProfessionId!)} level {outcome.UnmetRequiredLevel}.",
            ExperimentFailure.MissingInputs => "[Craft] Failed — missing materials.",
            ExperimentFailure.NoMatch => "[Craft] Nothing happens. No known interaction for those materials.",
            _ => "[Craft] Failed.",
        });
    }

    /// <summary>
    /// Debug helper: a spread of materials chosen to make the crafting bench worth playing
    /// with immediately — substrates of different forms, reagents spanning the media (soluble,
    /// volatile, hard), and enough skill to reach every process.
    /// </summary>
    public void GrantCraftTestMaterials()
    {
        foreach (var id in new[]
        {
            // Substrates: metal, stone, wood, herb — different forms take different processes.
            "material.iron_ingot", "material.iron_ore", "material.granite", "material.oak_log", "material.sageleaf",
            // Fabrication components (C2b): binding hides and an attunement vessel.
            "material.leather", "material.rawhide", "material.ley_crystal",
            // Reagents: soluble (sap, springwater), volatile (cores), hard (stormglass, granite).
            "material.ember_sap", "material.springwater", "material.oak_bark",
            "material.ember_core", "material.frost_core", "material.storm_core", "material.stormglass",
        })
        {
            if (_materials.Contains(id))
                _stash.Add(id, 20);
        }

        _professions.GetProgress("profession.herblore").AddXp(ProfessionLeveling.XpForLevel(15));
        _professions.GetProgress("profession.smithing").AddXp(ProfessionLeveling.XpForLevel(15));

        Emit("[Debug] Granted crafting materials and Herblore/Smithing level 15 (every process unlocked).");
        InventoryChanged?.Invoke();
    }

    public string CraftingReport()
    {
        var sb = new StringBuilder();
        foreach (var interaction in _interactions.GetAll())
        {
            var known = _discoveries.IsDiscovered(interaction.DiscoveryId);
            var inputs = string.Join(" + ", interaction.Inputs.Select(i => $"{i.Quantity} {ItemName(i.ItemId)}"));
            var reqs = interaction.ProfessionRequirements.Count == 0
                ? "no requirement"
                : string.Join(", ", interaction.ProfessionRequirements.Select(r => $"{ProfessionName(r.ProfessionId)} L{r.Level} (have L{_professions.GetProgress(r.ProfessionId).Level})"));
            var name = known ? interaction.Name : "??? (undiscovered)";
            sb.AppendLine($"{name}");
            sb.AppendLine($"  {inputs}  →  {ItemName(interaction.ResultItemId)}");
            sb.AppendLine($"  requires {reqs}");
        }

        return sb.ToString().TrimEnd();
    }

    // --- Combat -------------------------------------------------------------

    public IReadOnlyList<ActorDefinition> EnemyActors => _actors.GetAll().OrderBy(a => a.Name).ToList();
    public bool IsCombatActive => _encounter.IsActive;

    /// <summary>Sandbox fight (not tied to a Realm location).</summary>
    public void StartCombat(string actorId)
    {
        _realmCombatLocationId = null;
        StartCombatInternal(actorId);
    }

    private void StartCombatInternal(string actorId)
    {
        if (Character is null)
            return;
        if (_encounter.IsActive)
        {
            Emit("[Combat] Already fighting.");
            return;
        }
        if (Character.Health.IsDepleted)
        {
            Emit("[Combat] Too wounded to fight — heal first.");
            return;
        }

        _passiveRunner.Stop(); // one activity at a time
        _everFought = true;
        var actor = _actors.GetById(actorId);
        var resolvedActor = ActorResolver.Resolve(actor, _enemyFamilies, _enemyRoles, _aiProfiles);
        var player = Combatant.FromCharacter(Character, ResolvePlayerMoveset(), ResolvePlayerArmor());
        _encounter.PlayerCanParry = PlayerCanParry; // gear-granted (D-26), snapshotted at Start
        _encounter.Start(player, new[] { Combatant.FromActor(resolvedActor, ResolveActorMoveset(actor)) });
        SetRunning(true); // telegraphs advance in real time
        CombatChanged?.Invoke();
    }

    public void CombatAttack() => _encounter.Attack();

    /// <summary>Parry is a capability the gear grants (D-26): true while any worn item's
    /// definition carries the `parry` tag — a form declares it, never a class.</summary>
    public bool PlayerCanParry => EquippedTags().Contains("parry", StringComparer.OrdinalIgnoreCase);

    public void CombatParry() => _encounter.Parry();

    /// <summary>Uses a specific move from the player's moveset (E4).</summary>
    public void CombatUseMove(string moveId) => _encounter.UseMove(moveId);

    /// <summary>The player's resolved moveset — live from the encounter mid-fight, otherwise
    /// resolved fresh from the current build + equipment. The Combat tab's move buttons.</summary>
    public IReadOnlyList<ResolvedMove> PlayerMoveset =>
        _encounter.IsActive ? _encounter.PlayerMoves : ResolvePlayerMoveset();

    /// <summary>The player's current moveset, for the Combat tab and the Character Lab —
    /// name, id, costs, and where each move came from.</summary>
    public IReadOnlyList<string> MovesetReadout =>
        PlayerMoveset
        .Select(m =>
        {
            var costs = m.Costs.Count == 0 ? "free" : string.Join(", ", m.Costs);
            return $"{m.Name} [{m.Id}] — {costs} — from {m.Provenance[0]}";
        })
        .ToList();
    public void CombatBlock() => _encounter.Block();
    public void CombatDodge() => _encounter.Dodge();
    public void CombatWait() => _encounter.Wait();

    // --- Fabrication (C2a) --------------------------------------------------

    /// <summary>Forms the player can fabricate into, for the Crafting tab.</summary>
    public IReadOnlyList<FormTemplateDefinition> Forms =>
        _content.Forms.GetAll().OrderBy(f => f.Name).ToList();

    /// <summary>Materials on hand eligible for a form slot (any-of tag gate) — the per-slot
    /// component pickers' source (C2b).</summary>
    public IReadOnlyList<(string Id, string Name, int Quantity)> EligibleForSlot(string formId, string slotName)
    {
        if (!_content.Forms.TryGetById(formId, out var form) || !form.Slots.TryGetValue(slotName, out var slot))
            return Array.Empty<(string, string, int)>();

        return MaterialsOnHand
            .Where(m => _materials.TryGetById(m.Id, out var def)
                && (slot.RequiresTags.Count == 0
                    || slot.RequiresTags.Any(t => def.Tags.Contains(t, StringComparer.OrdinalIgnoreCase))))
            .ToList();
    }

    /// <summary>Multi-component fabrication (C2b): one material per named slot. Terminal —
    /// materials consumed, an ItemInstance lands in the current bag.</summary>
    public FabricationOutcome FabricateItem(string formId, IReadOnlyDictionary<string, string> slotMaterials)
    {
        if (!_content.Forms.TryGetById(formId, out var form))
            return FabricationOutcome.Failed(FabricationFailure.UnknownForm);

        var outcome = _fabrication.Fabricate(new FabricationRequest(formId, slotMaterials));

        if (!outcome.Success)
        {
            Emit($"[Fabricate] {form.Name}: {SemanticFormat.FabricationFailureText(outcome.Failure)}");
            return outcome;
        }

        var traits = outcome.Expressed.Count == 0
            ? ""
            : $" — {string.Join(", ", outcome.Expressed.Select(t => TraitName(t.Id)))}";
        var dormant = outcome.Dormant.Count == 0 ? "" : $" ({outcome.Dormant.Count} dormant)";
        Emit($"[Fabricate] {(outcome.IsFirstOfItsKind ? "✦ " : "")}{outcome.Name}{traits}{dormant}.");
        InventoryChanged?.Invoke();
        return outcome;
    }

    private string TraitName(string traitId) =>
        _content.Traits.TryGetById(traitId, out var def) ? def.Name : traitId;

    /// <summary>The pre-commit fabrication view — same composition, no side effects (R3).</summary>
    public FabricationProjection ProjectFabrication(string formId, IReadOnlyDictionary<string, string> slotMaterials) =>
        _fabrication.Project(new FabricationRequest(formId, slotMaterials));

    /// <summary>The fabrication preview card, read through the same seam the minted item uses.
    /// Promises the deterministic layer (stats, innates) and translates the genome's supported
    /// families — the engineering half of the casino (D-21/D29).</summary>
    public string FabricationPreviewText(string formId, IReadOnlyDictionary<string, string> slotMaterials)
    {
        var projection = ProjectFabrication(formId, slotMaterials);
        if (!projection.CanFabricate)
            return SemanticFormat.FabricationFailureText(projection.Failure);

        var form = _content.Forms.GetById(formId);
        var reading = ItemReadings.From(projection, form, _content);
        return SemanticFormat.Fabrication(
            projection, reading, ItemReadings.Supports(projection.Genome, _content));
    }

    /// <summary>Why a material suits (or doesn't suit) a slot — §2E context at the bench.</summary>
    public string SlotFitText(string formId, string slotName, string materialId)
    {
        if (!_content.Forms.TryGetById(formId, out var form)
            || !_materials.TryGetById(materialId, out var material))
            return string.Empty;

        var reading = SlotReadings.For(form, slotName, material, _profiles.Resolve(material), _content.Traits);
        return SemanticFormat.SlotFit(reading, _glossary);
    }

    /// <summary>Debug-only: reroll a stash instance's prefixes and suffixes. Innates never
    /// reroll (U-7 — the genome speaking). The player-facing reroll path is E7's operations;
    /// this exists so the casino can be verified without loot faucets.</summary>
    public ItemInstance? DebugRerollAffixes(long instanceId)
    {
        var instance = _stash.GetInstance(instanceId);
        if (instance?.Genome is not { } genome)
            return null;

        var affixes = new List<Dungeons.Affixes.RolledAffix>(
            Dungeons.Affixes.AffixRoller.Innates(genome, _content.Affixes.GetAll()));
        affixes.AddRange(Dungeons.Affixes.AffixRoller.Roll(genome, "prefix", _content.Affixes.GetAll(), _affixRng));
        affixes.AddRange(Dungeons.Affixes.AffixRoller.Roll(genome, "suffix", _content.Affixes.GetAll(), _affixRng));

        var rerolled = new ItemInstance
        {
            InstanceId = instance.InstanceId,
            BaseDefinitionId = instance.BaseDefinitionId,
            ItemType = instance.ItemType,
            DisplayName = instance.DisplayName,
            Quality = instance.Quality,
            Properties = instance.Properties,
            Provenance = instance.Provenance,
            Traits = instance.Traits,
            Genome = instance.Genome,
            Affixes = affixes,
        };

        _stash.RemoveInstance(instanceId);
        _stash.AddInstance(rerolled);
        Emit($"[Debug] Rerolled {rerolled.DisplayName}: {affixes.Count} modifiers.");
        InventoryChanged?.Invoke();
        return rerolled;
    }

    /// <summary>The full §6 reveal card for an owned item.</summary>
    public string ItemCardText(ItemInstance instance) =>
        _content.Equipment.TryGetById(instance.BaseDefinitionId, out var definition)
            ? SemanticFormat.Item(ItemReadings.From(instance, definition, _content))
            : instance.DisplayName;

    /// <summary>The one-line item label for lists — replaces the property wall (D30).</summary>
    public string ItemLabel(ItemInstance instance) =>
        _content.Equipment.TryGetById(instance.BaseDefinitionId, out var definition)
            ? SemanticFormat.ItemStrip(ItemReadings.From(instance, definition, _content))
            : instance.DisplayName;

    // --- Techniques (M2′ acquisition) ---------------------------------------

    /// <summary>Technique items in the stash, with quantity and whether the move is known.</summary>
    public IReadOnlyList<(TechniqueDefinition Technique, int Quantity, bool Known)> OwnedTechniques =>
        _techniques.GetAll()
            .Select(t => (t, _stash.GetQuantity(t.Id), _learnedMoves.Knows(t.Teaches)))
            .Where(row => row.Item2 > 0)
            .OrderBy(row => row.t.Name)
            .ToList();

    /// <summary>Consumes one technique item from the stash and learns its move. Refuses —
    /// without consuming — when the move is already known.</summary>
    public void LearnTechnique(string techniqueId)
    {
        if (!_techniques.TryGetById(techniqueId, out var technique))
            return;
        if (!_stash.Contains(techniqueId))
        {
            Emit($"[Technique] No {technique.Name} in the stash.");
            return;
        }
        if (_learnedMoves.Knows(technique.Teaches))
        {
            Emit($"[Technique] {MoveName(technique.Teaches)} is already known — {technique.Name} kept.");
            return;
        }

        _stash.TryRemove(techniqueId, 1);
        _learnedMoves.Learn(technique.Teaches);
        Emit($"[Technique] Learned {MoveName(technique.Teaches)} from {technique.Name}.");
        InventoryChanged?.Invoke();
        CharacterChanged?.Invoke(); // the moveset changed
    }

    /// <summary>Debug: one copy of every authored technique item into the stash.</summary>
    public void GrantTestTechniques()
    {
        foreach (var technique in _techniques.GetAll())
            _stash.Add(technique.Id, 1);
        Emit($"[Technique] Granted {_techniques.Count} technique item(s) to the stash.");
        InventoryChanged?.Invoke();
    }

    private string MoveName(string moveId) =>
        _moves.TryGetById(moveId, out var move) ? move.Name : moveId;

    /// <summary>Consumables the player currently carries in the active bag.</summary>
    public IReadOnlyList<ConsumableDefinition> UsableConsumables =>
        _consumables.GetAll().Where(c => CurrentBag.Contains(c.Id)).OrderBy(c => c.Name).ToList();

    public void CombatUseConsumable(string itemId)
    {
        if (!_encounter.IsActive)
            return;
        if (!_consumables.TryGetById(itemId, out var consumable))
            return;
        if (!CurrentBag.Contains(itemId))
        {
            Emit($"[Combat] No {consumable.Name} to use.");
            return;
        }

        CurrentBag.TryRemove(itemId, 1);
        _encounter.UseHealingItem(consumable.Name, consumable.HealAmount);
        InventoryChanged?.Invoke();
    }

    public string CombatReport()
    {
        if (!_everFought)
            return "(no combat yet — start a fight below)";

        var now = _tick.CurrentTick;
        var sb = new StringBuilder();
        foreach (var c in _encounter.Combatants)
        {
            var stance = c.IsDodging(now) ? "  [DODGING]" : c.IsBlocking(now) ? "  [BLOCKING]" : string.Empty;
            sb.AppendLine($"{c.Name,-16} HP {c.Health.Current,3}/{c.Health.Max}   STA {c.Stamina.Current,3}/{c.Stamina.Max}{stance}");
        }

        if (_encounter.IsActive)
        {
            sb.AppendLine(_encounter.PlayerReady ? "You are READY to act." : "You are recovering…");
            foreach (var intent in _encounter.Intents)
            {
                var seconds = Math.Max(0, intent.ExecuteTick - now) / (double)TicksPerSecond;
                var type = intent.Move.Packets.Count > 0 ? intent.Move.Packets[0].Type.ToString() : intent.Move.Kind.ToString();
                sb.AppendLine($"⚠ {intent.Attacker.Name}: {intent.Move.Name} — impact in {seconds:0.0}s ({type})");
            }
        }
        else
        {
            sb.AppendLine("Combat over.");
        }

        return sb.ToString().TrimEnd();
    }

    private void OnCombatEnded(CombatOutcome outcome)
    {
        if (outcome.Result == CombatResult.Victory)
        {
            foreach (var enemy in outcome.DefeatedEnemies)
            {
                if (string.IsNullOrEmpty(enemy.LootItemId))
                    continue;
                CurrentBag.Add(enemy.LootItemId, 1);
                Emit($"[Loot] {ItemName(enemy.LootItemId)} x1.");
            }
        }

        SetRunning(false);
        CombatChanged?.Invoke();

        // Resolve the fight against the Realm run, if it started at a location.
        var locationId = _realmCombatLocationId;
        _realmCombatLocationId = null;
        if (locationId is null || _run is null)
            return;

        if (outcome.Result == CombatResult.Victory)
        {
            _run.MarkCleared(locationId);
            AddKnowledge(_run.Realm.Id, RealmTuning.KnowledgePerCombatCleared);
            Emit($"[Realm] Cleared {_run.Realm.GetLocation(locationId).Name}.");
            RealmChanged?.Invoke();
        }
        else
        {
            EndRealmRun(died: true);
        }
    }

    // --- Equipment ----------------------------------------------------------

    /// <summary>Equipment blueprints that can be granted into the stash (excludes the starter kit).</summary>
    public IReadOnlyList<EquipmentDefinition> EquipmentCatalog =>
        _equipment.GetAll().Where(e => e.Id != StarterWeaponId && e.Id != StarterArmorId).OrderBy(e => e.Name).ToList();

    /// <summary>The instance equipped in the weapon/armor slot, or null when empty.</summary>
    public ItemInstance? EquippedWeapon => _playerEquipment.InSlot(EquipmentSlot.Weapon);
    public ItemInstance? EquippedArmor => _playerEquipment.InSlot(EquipmentSlot.Armor);

    /// <summary>Unequipped weapons/armor sitting in the Stash, ready to equip.</summary>
    public IReadOnlyList<ItemInstance> StashEquipment =>
        _stash.Instances.Where(i => i.ItemType is ItemType.Weapon or ItemType.Armor)
            .OrderBy(i => i.DisplayName).ToList();

    /// <summary>Debug: instantiate a piece of equipment and drop it in the Stash to be equipped from there.</summary>
    public void GrantToStash(string equipmentDefId)
    {
        if (!_equipment.TryGetById(equipmentDefId, out var def))
            return;
        _stash.AddInstance(InstantiateEquipment(def));
        Emit($"[Equipment] Added {def.Name} to the stash.");
    }

    /// <summary>Equips a Stash instance in its slot; banks whatever it displaces back to the Stash.</summary>
    public void EquipFromStash(long instanceId)
    {
        var instance = _stash.GetInstance(instanceId);
        if (instance is null)
            return;
        if (!_equipment.TryGetById(instance.BaseDefinitionId, out var def))
        {
            Emit($"[Equipment] {instance.DisplayName} is not equippable.");
            return;
        }

        _stash.RemoveInstance(instanceId);
        var displaced = _playerEquipment.Equip(def.Slot, instance);
        if (displaced is not null)
            _stash.AddInstance(displaced);
        AttachBuildRules(); // affix rules swap with the gear (R4b)
        Emit($"[Equipment] Equipped {instance.DisplayName}.");
    }

    /// <summary>Removes the item in a slot and returns it to the Stash (the player may fight unarmed).</summary>
    public void UnequipToStash(EquipmentSlot slot)
    {
        var removed = _playerEquipment.Unequip(slot);
        if (removed is null)
            return;
        _stash.AddInstance(removed);
        AttachBuildRules(); // an unequipped item's affix rules must stop firing
        Emit($"[Equipment] Unequipped {removed.DisplayName}.");
    }

    public string EquippedWeaponSummary()
    {
        // The weapon IS its moves now (E4). Summarise the default attack — the first one.
        var moveset = ResolvePlayerMoveset();
        var attack = moveset.FirstOrDefault(m => string.Equals(m.ActionKind, "attack", StringComparison.OrdinalIgnoreCase));
        if (attack is null)
            return "no attack";

        var damage = attack.Packets.Sum(p => p.Amount);
        var type = attack.Packets.Count > 0 ? attack.Packets[0].Type.ToString() : "—";
        return $"{attack.Name}: {damage:0.#} {type}, impact {attack.Timing.TimeToImpactTicks}t";
    }

    public string EquippedArmorSummary()
    {
        var armorProfile = ResolvePlayerArmor();

        // D-05a: resistances display as `capped / raw` wherever they appear — the raw number
        // only shows once it exceeds the cap, so the complexity appears exactly when earned.
        var resist = armorProfile.Resistances.Count == 0
            ? string.Empty
            : "  resist " + string.Join(", ", armorProfile.Resistances.Select(r =>
            {
                var capped = Dungeons.Combat.Combatant.CapResistance(r.Value);
                return r.Value > capped + 0.0001
                    ? $"{r.Key} {capped:P0}/{r.Value:P0}"
                    : $"{r.Key} {capped:P0}";
            }));
        return $"{armorProfile.Armor:0.#} armor{resist}";
    }


    public string EquipmentReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Weapon: {(EquippedWeapon?.DisplayName ?? "— (unarmed)")}  →  {EquippedWeaponSummary()}");
        sb.Append($"Armor:  {(EquippedArmor?.DisplayName ?? "— (none)")}  →  {EquippedArmorSummary()}");
        return sb.ToString();
    }

    private void EquipStarterLoadout()
    {
        if (_playerEquipment.InSlot(EquipmentSlot.Weapon) is null && _equipment.Contains(StarterWeaponId))
            _playerEquipment.Equip(EquipmentSlot.Weapon, InstantiateEquipment(_equipment.GetById(StarterWeaponId)));
        if (_playerEquipment.InSlot(EquipmentSlot.Armor) is null && _equipment.Contains(StarterArmorId))
            _playerEquipment.Equip(EquipmentSlot.Armor, InstantiateEquipment(_equipment.GetById(StarterArmorId)));
    }

    private ItemInstance InstantiateEquipment(EquipmentDefinition def) => new()
    {
        InstanceId = _instanceIds.Next(),
        BaseDefinitionId = def.Id,
        ItemType = def.ItemType,
        DisplayName = def.Name,
        Properties = def.BaseProperties,
        Provenance = new[] { def.Id },
    };

    /// <summary>
    /// The player's moveset, composed fresh from every source (E4, docs/moves.md §5.1):
    /// <b>weapon first</b> — so the default attack is the weapon's, which is the Fighter's whole
    /// identity — then the build's components (species' bare fists included). Modifiers from
    /// components and worn equipment apply across the lot.
    /// </summary>
    private IReadOnlyList<ResolvedMove> ResolvePlayerMoveset()
    {
        if (Character is null)
            return Array.Empty<ResolvedMove>();

        var grants = new List<MoveGrant>();
        var modifierGrants = new List<MoveModifierGrant>();

        var weapon = _playerEquipment.InSlot(EquipmentSlot.Weapon);
        if (weapon is not null && _equipment.TryGetById(weapon.BaseDefinitionId, out var weaponDef))
        {
            // The resolver hands back per-instance definitions (mass applied), so the builder
            // must not re-resolve the ids from the shared store.
            foreach (var move in EquipmentResolver.ResolveWeaponMoves(weaponDef, weapon, _moves))
                grants.Add(new MoveGrant(new MoveGrantSpec { Id = move.Id }, weapon.DisplayName));
        }

        grants.AddRange(Character.Blueprint.MoveGrants);

        // Learned techniques (M2′): universal library moves this character has studied.
        foreach (var moveId in _learnedMoves.All)
            grants.Add(new MoveGrant(new MoveGrantSpec { Id = moveId }, "learned"));

        foreach (var (modifierId, source) in Character.Blueprint.MoveModifierGrants)
            if (_moveModifierStore.TryGetById(modifierId, out var definition))
                modifierGrants.Add(new MoveModifierGrant(definition, source));

        foreach (var item in _playerEquipment.Slots.Values)
            if (_equipment.TryGetById(item.BaseDefinitionId, out var def))
                foreach (var modifierId in def.MoveModifierIds)
                    if (_moveModifierStore.TryGetById(modifierId, out var definition))
                        modifierGrants.Add(new MoveModifierGrant(definition, item.DisplayName));

        // R4c-2: worn affixes author move modifiers too — the 11-op system's third grantor,
        // through the same builder path as equipment and character components.
        foreach (var (instance, rolled, affixDef) in EquippedAffixes())
            foreach (var grant in affixDef.Grants)
                if (string.Equals(grant.Type, "moveModifier", StringComparison.OrdinalIgnoreCase)
                    && _moveModifierStore.TryGetById(grant.Key, out var moveModifier))
                    modifierGrants.Add(new MoveModifierGrant(moveModifier, $"{affixDef.Name} ({instance.DisplayName})"));

        // Weapon-adjusted definitions override the store's for the weapon's own ids.
        var weaponAdjusted = new DataStore<MoveDefinition>();
        var adjustedIds = new HashSet<string>(StringComparer.Ordinal);
        if (weapon is not null && _equipment.TryGetById(weapon.BaseDefinitionId, out var wd))
        {
            foreach (var move in EquipmentResolver.ResolveWeaponMoves(wd, weapon, _moves))
            {
                weaponAdjusted.Add(move);
                adjustedIds.Add(move.Id);
            }
        }
        foreach (var move in _moves.GetAll().Where(m => !adjustedIds.Contains(m.Id)))
            weaponAdjusted.Add(move);

        var conflicts = new MovesetBuilder(weaponAdjusted).Build(grants, modifierGrants, out var moveset);
        foreach (var conflict in conflicts)
            Emit($"[Moveset] {conflict}");

        return moveset;
    }

    /// <summary>An enemy's moveset — the same builder players use, no modifiers yet.</summary>
    private IReadOnlyList<ResolvedMove> ResolveActorMoveset(ActorDefinition actor)
    {
        var conflicts = new MovesetBuilder(_moves).Build(
            actor.Moves.Select(m => new MoveGrant(m, actor.Name)),
            Array.Empty<MoveModifierGrant>(),
            out var moveset);

        foreach (var conflict in conflicts)
            Emit($"[Moveset] {actor.Id}: {conflict}");

        return moveset;
    }

    private ArmorProfile ResolvePlayerArmor()
    {
        var instance = _playerEquipment.InSlot(EquipmentSlot.Armor);
        if (instance is not null && _equipment.TryGetById(instance.BaseDefinitionId, out var def))
            return EquipmentResolver.ResolveArmor(def, instance);
        return ArmorProfile.None;
    }

    // --- Realm --------------------------------------------------------------

    public IReadOnlyList<RealmDefinition> Realms => _realms.GetAll().OrderBy(r => r.Name).ToList();
    public bool InRealm => _run is { Active: true };
    public RealmRun? Run => _run;
    public bool RealmBusy => _encounter.IsActive;
    public bool RealmCanDescend => _run?.CanDescend ?? false;
    public bool RealmCanExtract => _run?.CanExtract ?? false;
    public int Knowledge(string realmId) => _realmKnowledge.TryGetValue(realmId, out var k) ? k : 0;

    public string ActorName(string actorId) => _actors.TryGetById(actorId, out var a) ? a.Name : actorId;

    /// <summary>Label for the current location's primary action, or null if it has none.</summary>
    public string? RealmActionLabel()
    {
        if (_run is null)
            return null;
        var loc = _run.CurrentLocation;
        return loc.Type switch
        {
            RealmLocationType.Combat => _run.IsCleared(loc.Id) ? null : $"Fight {ActorName(loc.ActorId ?? string.Empty)}",
            RealmLocationType.Gather => "Gather here",
            RealmLocationType.Event => _run.IsCleared(loc.Id) ? null : "Investigate",
            _ => null,
        };
    }

    public void EnterRealm(string realmId)
    {
        if (Character is null || InRealm)
            return;

        var realm = _realms.GetById(realmId);
        _passiveRunner.Stop();
        Character.RestoreAll(); // rested and prepared before the expedition
        _run = new RealmRun(realm, tier: 1);
        _run.RunInventory.Changed += () => InventoryChanged?.Invoke();
        AddKnowledge(realmId, RealmTuning.KnowledgePerEnter);
        Emit($"[Realm] Entered {realm.Name} (Tier {_run.Tier}, Depth {_run.CurrentDepth}).");
        RealmChanged?.Invoke();
    }

    public void RealmTravel(string locationId)
    {
        if (_run is null)
            return;
        if (_encounter.IsActive)
        {
            Emit("[Realm] Finish the fight before moving on.");
            return;
        }

        var isNew = !_run.Visited.Contains(locationId);
        if (!_run.TravelTo(locationId))
            return;

        if (isNew)
            AddKnowledge(_run.Realm.Id, RealmTuning.KnowledgePerTravel);
        Emit($"[Realm] Travelled to {_run.CurrentLocation.Name}.");
        RealmChanged?.Invoke();
    }

    /// <summary>Performs the current location's primary action (fight / gather / investigate).</summary>
    public void RealmAction()
    {
        if (_run is null || _encounter.IsActive)
            return;

        var loc = _run.CurrentLocation;
        switch (loc.Type)
        {
            case RealmLocationType.Combat:
                if (_run.IsCleared(loc.Id))
                {
                    Emit("[Realm] This area is already cleared.");
                    return;
                }
                if (loc.ActorId is null)
                    return;
                _realmCombatLocationId = loc.Id;
                StartCombatInternal(loc.ActorId);
                RealmChanged?.Invoke();
                break;

            case RealmLocationType.Gather:
                if (loc.ProfessionActionId is null)
                    return;
                _professions.Execute(loc.ProfessionActionId); // logs + inventory via existing wiring
                RealmChanged?.Invoke();
                break;

            case RealmLocationType.Event:
                if (_run.IsCleared(loc.Id))
                {
                    Emit("[Realm] Nothing else of interest here.");
                    return;
                }
                Emit($"[Event] {loc.EventText}");
                if (!string.IsNullOrEmpty(loc.RewardItemId))
                {
                    CurrentBag.Add(loc.RewardItemId, loc.RewardQuantity);
                    Emit($"[Loot] {ItemName(loc.RewardItemId)} x{loc.RewardQuantity}.");
                }
                _run.MarkCleared(loc.Id);
                AddKnowledge(_run.Realm.Id, RealmTuning.KnowledgePerEvent);
                RealmChanged?.Invoke();
                break;

            default:
                Emit("[Realm] Nothing to do here — travel onward.");
                break;
        }
    }

    public void RealmGoDeeper()
    {
        if (_run is null)
            return;
        if (!_run.Descend())
        {
            Emit("[Realm] You can only descend at The Descent.");
            return;
        }

        AddKnowledge(_run.Realm.Id, RealmTuning.KnowledgePerDescend);
        Emit($"[Realm] You press deeper. Depth {_run.CurrentDepth}. The danger rises.");
        RealmChanged?.Invoke();
    }

    public void RealmExtract()
    {
        if (_run is null)
            return;
        if (!_run.CanExtract)
        {
            Emit("[Realm] No extraction point here.");
            return;
        }

        EndRealmRun(died: false);
    }

    public string RealmReport()
    {
        if (_run is null)
        {
            var known = _realms.GetAll().Select(r => $"{r.Name}: Knowledge {Knowledge(r.Id)}");
            return "In the Hideout.\n" + string.Join("\n", known);
        }

        var run = _run;
        var loc = run.CurrentLocation;
        var cleared = loc.Type == RealmLocationType.Combat && run.IsCleared(loc.Id) ? " (cleared)" : string.Empty;
        var hp = Character is null ? "-" : $"{Character.Health.Current}/{Character.Health.Max}";

        var unsecured = run.RunInventory.Snapshot().Sum(s => s.Quantity);
        var sb = new StringBuilder();
        sb.AppendLine($"{run.Realm.Name} — Tier {run.Tier}   Depth {run.CurrentDepth}");
        sb.AppendLine($"Location: {loc.Name} [{loc.Type}]{cleared}");
        sb.AppendLine($"Party HP: {hp}    Knowledge: {Knowledge(run.Realm.Id)}");
        sb.AppendLine($"Unsecured loot at risk: {unsecured} item(s)");
        if (_encounter.IsActive)
            sb.AppendLine("In combat — see the Combat panel.");
        return sb.ToString().TrimEnd();
    }

    private void EndRealmRun(bool died)
    {
        if (_run is null)
            return;
        var realmId = _run.Realm.Id;

        if (died)
        {
            var lost = RealmExtraction.Forfeit(_run);
            Emit($"[Realm] You have died. {lost.TotalQuantity} unsecured item(s) lost. Your Stash and equipped gear are safe.");
        }
        else
        {
            var secured = RealmExtraction.Secure(_run, _stash);
            AddKnowledge(realmId, RealmTuning.KnowledgePerExtract);
            Emit($"[Extraction] Secured {secured.TotalQuantity} item(s) to your Stash. Returned to the Hideout.");
        }

        _run = null;
        _realmCombatLocationId = null;
        RealmChanged?.Invoke();
        InventoryChanged?.Invoke();
    }

    private void AddKnowledge(string realmId, int amount) =>
        _realmKnowledge[realmId] = Knowledge(realmId) + amount;

    // --- Save ---------------------------------------------------------------

    public void SaveGame()
    {
        var data = SaveMapper.Capture(_build, _stash, _professions, _discoveries, _realmKnowledge, _tick.CurrentTick, _playerEquipment, _instanceIds, _emergentRegistry, learnedMoves: _learnedMoves,
            emergentEquipment: _equipment.GetAll().Where(e => e.Id.StartsWith("equip.emergent.", StringComparison.Ordinal)));
        _saveStore.Save(data);
        Emit($"[Save] Saved — {data.Professions.Count} profession(s), {data.Stash.Count} stash stack(s), " +
             $"{data.StashInstances.Count} instance(s), {data.Equipment.Count} equipped, {data.Discoveries.Count} discovery(ies).");
    }

    public void LoadGame()
    {
        if (InRealm)
        {
            Emit("[Load] Extract or finish the run before loading.");
            return;
        }

        var save = _saveStore.Load();
        if (save is null)
        {
            Emit("[Load] No save file found.");
            return;
        }

        SaveMapper.Apply(save, _stash, _professions, _discoveries, _realmKnowledge, _playerEquipment, _instanceIds, _emergentRegistry, learnedMoves: _learnedMoves, equipmentStore: _equipment);
        if (save.Build is not null)
        {
            _build = save.Build;
            RebuildCharacter(); // raises CharacterChanged
        }

        EquipStarterLoadout(); // fill any empty slots (fresh/old saves) so the player is never unarmed

        Emit($"[Load] Loaded save (schema v{save.SchemaVersion}, saved at tick {save.SavedAtTick}).");
        InventoryChanged?.Invoke();
        DiscoveryChanged?.Invoke();
        RealmChanged?.Invoke();
    }

    public void ReportStatus()
    {
        Emit($"[Content] {_materials.Count} materials, {_professionDefs.Count} professions, {_actionDefs.Count} actions.");
        if (Character is not null)
            Emit($"[Character] Active build: {Character.DisplayName}");
    }

    public string CharacterReport()
    {
        if (Character is null)
            return "No character.";

        var c = Character;
        var effective = c.EffectiveAttributes;
        var baseAttributes = c.BaseAttributes;

        var sb = new StringBuilder();
        sb.AppendLine(c.DisplayName);
        sb.AppendLine($"Primary resource: {c.Blueprint.PrimaryResource}");
        sb.AppendLine($"HP {c.Health.Current}/{c.Health.Max}    Mana {c.Mana.Current}/{c.Mana.Max}    Stamina {c.Stamina.Current}/{c.Stamina.Max}");
        sb.AppendLine(EquipmentReport());
        sb.AppendLine("Attributes  (effective / base):");
        foreach (var attribute in AttributeTypes.All)
            sb.AppendLine($"  {attribute,-13} {effective[attribute],3} / {baseAttributes[attribute]}");
        sb.AppendLine($"Tags: {string.Join(", ", c.Blueprint.Tags)}");
        if (c.Blueprint.Rules.Count > 0)
        {
            sb.AppendLine("Rules:");
            foreach (var rule in c.Blueprint.Rules)
                sb.AppendLine($"  • {rule.Description}");
        }

        return sb.ToString().TrimEnd();
    }

    // --- Internals ----------------------------------------------------------

    private void RebuildCharacter()
    {
        Character = new Character(_composer.Compose(_build, Baseline));
        AttachBuildRules();
        Emit($"[Character] Built {Character.DisplayName}.");
        CharacterChanged?.Invoke();
    }

    /// <summary>
    /// Re-attaches the resolved build's hooks to the rule engine. Called on every rebuild, so
    /// swapping a component in the Character Lab swaps its live hooks too — detach-all first,
    /// or the old Prefix keeps firing.
    /// </summary>
    private void AttachBuildRules()
    {
        // No null guard on purpose: _ruleEngine is constructed before the first RebuildCharacter,
        // and if that ordering is ever broken a startup NullReference is far better than a build
        // whose hooks silently never attach.
        _ruleEngine.DetachAll();
        var resolved = _buildResolver.Resolve(_build);
        foreach (var attached in resolved.Rules)
            _ruleEngine.Attach(attached.Rule, attached.Source);

        // R4b: triggered affixes on worn items attach beside the build's own hooks and swap
        // with the gear — an unequipped item's rules must stop firing exactly like a retired
        // Prefix's would.
        foreach (var (instance, rolled, definition) in EquippedAffixes())
        {
            foreach (var rule in Dungeons.Affixes.AffixGrants.Rules(rolled, definition))
                _ruleEngine.Attach(rule, $"{definition.Name} ({instance.DisplayName})");
        }

        // The gauge set is part of the build, so it swaps with it — otherwise a retired Prefix's
        // meter would keep filling from feeds that no longer exist.
        _gauges.Reconfigure(resolved.Gauges, _tick.CurrentTick);
    }

    /// <summary>Every rolled affix on every worn item, with its definition resolved.</summary>
    private IEnumerable<(ItemInstance Instance, Dungeons.Affixes.RolledAffix Rolled, Dungeons.Affixes.AffixDefinition Definition)> EquippedAffixes()
    {
        foreach (var instance in _playerEquipment.Slots.Values)
        {
            foreach (var rolled in instance.Affixes)
            {
                if (_content.Affixes.TryGetById(rolled.AffixId, out var definition))
                    yield return (instance, rolled, definition);
            }
        }
    }

    /// <summary>Stat grants from worn items' affixes, as ordinary scoped contributions.</summary>
    private IEnumerable<Dungeons.Modifiers.ModifierContribution> EquippedAffixContributions() =>
        EquippedAffixes().SelectMany(a =>
            Dungeons.Affixes.AffixGrants.Contributions(a.Rolled, a.Definition, $"{a.Definition.Name} ({a.Instance.DisplayName})"));

    /// <summary>Tags of everything currently worn, for the <c>equippedTag</c> condition.</summary>
    private IEnumerable<string> EquippedTags() =>
        _playerEquipment.Slots.Values
            .Select(item => _equipment.TryGetById(item.BaseDefinitionId, out var def) ? def : null)
            .Where(def => def is not null)
            .SelectMany(def => def!.Tags);

    /// <summary>Conditions no rule could evaluate — the condition half of
    /// <see cref="UnhandledHooks"/>, and empty in a correctly-wired build.</summary>
    public IReadOnlyList<string> UnevaluatedConditions => _ruleEngine.UnevaluatedConditions;

    /// <summary>Every live gauge and its current fill — the Character Lab's "is the meter moving?"
    /// answer, and the readout that makes Charge visible while a fight is running.</summary>
    public IReadOnlyList<string> GaugeReadout =>
        _gauges.Pools.Select(p => $"{p.Name} {p.Current:0.#}/{p.Max:0.#} ({p.Fraction:P0})").ToList();

    /// <summary>Every hook that fired, newest last — the Character Lab's "is this thing on?" answer.</summary>
    public IReadOnlyList<string> FiredHooks =>
        _ruleEngine.Fired.Select(f => $"{f.Source}: {f.Kind} ({f.Trigger.Kind})").ToList();

    /// <summary>
    /// Hooks that fired into a system that does not exist yet. Content routinely references
    /// unbuilt systems, and it must be <b>visibly inert rather than silently missing</b>
    /// (DECISIONS D23) — until E3 registers handlers, most of this list is expected.
    /// </summary>
    public IReadOnlyList<string> UnhandledHooks =>
        _ruleEngine.Unhandled.Select(u => $"{u.Source}: {u.Kind}").ToList();

    /// <summary>
    /// Renders the Hit Log into the event log when <see cref="ShowHitLog"/> is on. Off by
    /// default: the trace is a debugging and Combat-Lab surface, and printing seven lines per
    /// swing would drown the narration it exists to explain.
    /// </summary>
    private void OnHitResolved(HitResult hit)
    {
        if (!ShowHitLog)
            return;

        foreach (var line in hit.Log.Lines)
            Emit("    " + line);
    }

    /// <summary>Debug toggle for the per-hit damage trace (docs/damage-and-defense.md §3.3).</summary>
    public bool ShowHitLog { get; set; }

    /// <summary>The last hit's full trace, for the Combat tab / Combat Lab.</summary>
    public string LastHitLog =>
        _encounter.LastHit is null
            ? "(no hit resolved yet)"
            : _encounter.LastHit.Log.Render($"Last hit — {_encounter.LastHit.Amount} {_encounter.LastHit.Type}");

    private void OnActionCompleted(ActionOutcome outcome)
    {
        // Active completions are logged by ActiveAttempt with timing detail; only
        // narrate passive completions here to avoid double logging.
        if (outcome.WasActive)
            return;
        Emit($"[{ProfessionOf(outcome.ActionId)}] {ActionName(outcome.ActionId)} → {DescribeProduced(outcome)} (xp +{outcome.XpGained}).");
    }

    private void OnLeveledUp(ProfessionLevelUp up) =>
        Emit($"[Level] {ProfessionName(up.ProfessionId)} reached level {up.NewLevel}!");

    private void OnPassiveStalled(ActionOutcome outcome) =>
        Emit($"[Passive] Stopped — {ActionName(outcome.ActionId)} ({outcome.Failure}).");

    private void OnDiscovered(string discoveryId)
    {
        var interaction = _interactions.GetAll().FirstOrDefault(i => i.DiscoveryId == discoveryId);
        var name = interaction?.Name ?? discoveryId;
        Emit($"[Discovery] You discovered {name}!");
        DiscoveryChanged?.Invoke();
    }

    private string DescribeProduced(ActionOutcome outcome)
    {
        if (outcome.Produced.Count == 0)
            return "nothing";
        return string.Join(", ", outcome.Produced.Select(s => $"+{s.Quantity} {ItemName(s.ItemId)}"));
    }

    private string ProfessionOf(string actionId) =>
        _actionDefs.TryGetById(actionId, out var a) ? ProfessionName(a.ProfessionId) : "?";

    private string ActionName(string actionId) =>
        _actionDefs.TryGetById(actionId, out var a) ? a.Name : actionId;

    private string ItemName(string itemId)
    {
        if (_materials.TryGetById(itemId, out var m))
            return m.Name;
        if (_consumables.TryGetById(itemId, out var c))
            return c.Name;
        if (_techniques.TryGetById(itemId, out var t))
            return t.Name;
        return itemId;
    }

    private void Emit(string message)
    {
        GD.Print(message);
        LogEmitted?.Invoke(message);
    }
}
