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
using Dungeons.Crafting.Identity;
using Dungeons.Events;
using Dungeons.Game.Infrastructure;
using Dungeons.Hideout;
using Dungeons.Items;
using Dungeons.Loot;
using Dungeons.Modifiers;
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

    /// <summary>
    /// Ceiling on how much simulation one frame may catch up on. Guards against a very long
    /// frame (a breakpoint, a stalled window) asking for minutes of ticks at once.
    /// </summary>
    private const int MaxTicksPerFrame = 10_000;

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
    private DataStore<ProfessionDefinition> _professionStore = new();
    private DataStore<ProfessionActionDefinition> _actionStore = new();
    private DataStore<TrainingObstacleDefinition> _obstacleStore = new();
    private DataStore<StationDefinition> _stationStore = new();

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
    private DataStore<AutoCombatProfileDefinition> _autoCombatProfiles = new();
    private DataStore<EquipmentDefinition> _equipment = new();

    private readonly Equipment _playerEquipment = new();
    private readonly InstanceIdSource _instanceIds = new();

    private const string StarterWeaponId = "equip.rusty_sword";
    private const string StarterArmorId = "equip.tattered_armor";

    private CharacterComposer _composer = null!;
    private LootResolver _lootResolver = null!;
    private ProfessionSystem _professions = null!;
    private PassiveProfessionRunner _passiveRunner = null!;
    private FarmingPlots _farmingPlots = null!;
    private TrainingCourse _trainingCourse = null!;

    /// <summary>The offer waiting on the player's pursue/ignore decision, if any. One at a
    /// time: a second discovery while one is pending would turn a decision into a queue.</summary>
    private PendingOpportunity? _pendingOpportunity;

    /// <summary>The in-flight pursuit's scheduled resolution, so the UI can show a bar.</summary>
    private ScheduledAction? _pursuitInProgress;
    private long _pursuitStartTick;
    private long _pursuitEndTick;
    private DiscoverySystem _discoveries = null!;
    private CraftingExperimentSystem _legacyInteractionCrafting = null!;
    private IEmergentRegistry _emergentRegistry = null!;
    private VerbActionRunner _verbActionRunner = null!;
    private ItemEffectResolver _itemEffectResolver = null!;
    private IdentityFabricationEngine _identityFabrication = null!;
    private BuildResolver _buildResolver = null!;
    private CombatEncounter _encounter = null!;

    /// <summary>The encounter's seeded stream. Shared with the auto-combat pilot on purpose: one
    /// stream means an automated fight replays from the seed like any other, and the pilot's
    /// choices vary between fights instead of repeating the same sequence every time.</summary>
    private SeededRandom _combatRandom = null!;
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
    private readonly RunLoadout _loadout = new();
    private readonly CharacterProgress _characterProgress = new();

    /// <summary>
    /// Where everything the player acquires currently lands: the <b>unsecured</b> run inventory
    /// while inside a Realm, otherwise the Stash. This one expression is the extraction risk
    /// model — every system that produces an item deposits here without knowing which it is.
    /// </summary>
    private Inventory ActiveInventory => _run is { Active: true } ? _run.RunInventory : _stash;

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

    /// <summary>An opportunity is waiting on a pursue/ignore decision, or null once it is
    /// resolved either way.</summary>
    public event Action<PendingOpportunity?>? OpportunityOffered;

    public long CurrentTick => _tick.CurrentTick;
    public bool IsRunning => _running;
    public Character? Character { get; private set; }

    public override void _Ready()
    {
        var content = ContentLoader.LoadAll("res://data");
        ValidateContentOrThrow(content);

        _content = content;
        _materials = content.Materials;
        _professionStore = content.Professions;
        _actionStore = content.Actions;
        _obstacleStore = content.TrainingObstacles;
        _stationStore = content.Stations;
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
        _autoCombatProfiles = content.AutoCombatProfiles;
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

        // Loot draws from its own seeded stream, so a change to combat or crafting rolls does
        // not silently reshuffle what drops.
        _lootResolver = new LootResolver(content, new SeededRandom(0x1007ab1e));

        // Gathering deposits into the current bag: the Stash in the Hideout, the run
        // inventory while in a Realm (unsecured until extraction). The drop-table delegate is
        // the seam: Core decides *whether* an action turned something up, the client supplies
        // the circumstances (depth, realm, active-or-passive) that decide *what*.
        _professions = new ProfessionSystem(
            _actionStore,
            () => ActiveInventory,
            new SeededRandom(20260814),
            rollDropTable: (tableId, wasActive) => _lootResolver.Roll(
                tableId,
                LootCircumstances(new[] { wasActive ? LootContextTags.Active : LootContextTags.Passive })));

        // What progress is worth is content: the mastery ladder (Phase 8) and the synergy table
        // (Phase 10) fold into one answer here. Without this they load and nothing reads them,
        // which is the exact state mastery spent the whole project in.
        _professions.Benefits = new ProfessionBenefits(
            new MasteryBenefits(content.MasteryBenefits),
            new ProfessionSynergies(content.Synergies),
            levelOf: professionId => _professions.GetProgress(professionId).Level,
            totalLevel: () => TotalProfessionLevel);
        _passiveRunner = new PassiveProfessionRunner(_tick, _professions);
        _professions.ActionCompleted += OnActionCompleted;
        _professions.OpportunityResolved += OnOpportunityResolved;
        _professions.LeveledUp += OnLeveledUp;
        _passiveRunner.Stalled += OnPassiveStalled;
        _passiveRunner.Resumed += OnPassiveResumed;

        // Farming's plots always grow into the Stash: a crop is tended at the Hideout, not
        // carried through a Realm, so it is never at risk from a death.
        _farmingPlots = new FarmingPlots(_actionStore, _professions, () => _stash);
        _trainingCourse = new TrainingCourse(_obstacleStore, _professions);
        _stash.Changed += () => InventoryChanged?.Invoke();
        _playerEquipment.Changed += () => CharacterChanged?.Invoke();

        // Crafting happens in the Hideout, against the Stash (field crafting deferred).
        _discoveries = new DiscoverySystem();
        _legacyInteractionCrafting = new CraftingExperimentSystem(
            _interactions, _materials, _stash, _discoveries,
            professionLevel: id => _professions.GetProgress(id).Level,
            instanceIds: _instanceIds);
        _discoveries.Discovered += OnDiscovered;

        // The emergent registry: minted materials register here and load back from saves. The
        // interaction system above survives only to keep the Healing Salve brewable until
        // consumable forms land (P5c).
        _emergentRegistry = new EmergentRegistry(_materials);

        // The identity bench (migration Phase 2c, D42–D48) — the crafting stack, and since
        // Phase 7 (D54) the only one. It deposits into ActiveInventory, so extraction risk
        // applies to bench work for free.
        _verbActionRunner = new VerbActionRunner(
            content,
            new IdentityCraftingEngine(content, new SeededRandom(0x1DE47175)),
            _emergentRegistry,
            () => ActiveInventory,
            professionProgress: id => _professions.GetProgress(id));

        // The identity forge (migration Phase 3, D50/D51) — the item-generation half of the
        // replacement stack. One resolver instance serves both minting and the equip-time
        // recompile, so a worn item's grants can never drift from what its mint promised.
        _itemEffectResolver = new ItemEffectResolver(content);
        _identityFabrication = new IdentityFabricationEngine(
            content, () => ActiveInventory, _instanceIds, new SeededRandom(0x1DEF0126));

        var combatRandom = new SeededRandom(0x0C0FFEE);
        _combatRandom = combatRandom;
        _statuses = new StatusController(content.Statuses, _events, () => _tick.CurrentTick);

        // E3c-2: the modifier read path. Build statics, status `while_active`, gauge bands and
        // timed grants all assemble here, and combat reads them. Every one of those four was
        // authored and inert before this.
        _modifiers = new CombatantModifiers(
            content.ModifierKeys,
            isOwner: c => c.Team == CombatTeam.Player,
            // Build statics plus whatever the worn items grant — affixes (R4b) and identity
            // sentences (Phase 3) alike: equipment is just another contribution source,
            // with per-item provenance.
            buildModifiers: () => _buildResolver.Resolve(_build).Modifiers.Contributions
                .Concat(EquippedIdentityContributions()),
            _statuses, _gauges);

        _encounter = new CombatEncounter(
            _tick, new HitPipeline(combatRandom, _modifiers), _moves, combatRandom, _events,
            _statuses, _gauges, _modifiers, _moveModifierStore);

        // E3c: effects stop landing in `Unhandled`. Eleven kinds are combat's (E4 added the
        // move-granting four); the rest belong to systems that do not exist yet and stay
        // visibly inert. This also hands the encounter its effect sink for move riders.
        _ruleEngine.RegisterCombatHandlers(_encounter, combatRandom);

        // E3c-3: the stateful conditions get something to ask. Equipped tags come from the worn
        // items' definitions, so `equippedTag` reads what the player is actually wearing.
        _conditionWorld = new CombatConditionWorld(_encounter, EquippedTags);
        _encounter.ConditionWorld = _conditionWorld;
        _encounter.Logged += Emit;
        _encounter.StateChanged += () => CombatChanged?.Invoke();
        _encounter.Ended += OnCombatEnded;
        _encounter.HitResolved += OnHitResolved;

        EnsureRealmSelected();

        GD.Print($"[GameRoot] Ready. {_materials.Count} materials, {_professionStore.Count} professions, {_actionStore.Count} actions, {_interactions.Count} interactions.");
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

        // The iteration cap only matters after a long freeze (a breakpoint, a stalled frame):
        // without it the accumulator could ask for minutes of simulation inside one frame.
        var ticksThisFrame = 0;
        while (_tickAccumulator >= 1.0 && ticksThisFrame++ < MaxTicksPerFrame)
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
        var report = new StringBuilder();

        report.AppendLine(build.Name);
        report.AppendLine();
        report.AppendLine($"Engine     {build.Base.Engine}");
        report.AppendLine($"Weakness   {build.Base.Weakness}");
        report.AppendLine($"Resource   {build.Base.PrimaryResource}    Channel  {build.Channel}");
        report.AppendLine();

        report.AppendLine("Growth per level (budget " + AttributeGrowth.BudgetPerLevel.ToString("0.#") + ")");
        foreach (var (attribute, weight) in build.GrowthPerLevel.OrderByDescending(p => p.Value))
            report.AppendLine($"  {attribute,-13} {weight,5:0.###}   → +{build.GrowthAt(21)[attribute]} by L21");
        report.AppendLine();

        report.AppendLine(build.Gauges.Count == 0
            ? "Gauges     (none — this Base runs without a meter)"
            : "Gauges");
        foreach (var gauge in build.Gauges)
            report.AppendLine($"  {gauge.Name,-12} {gauge.Behaviour}, max {gauge.Max:0}, {gauge.Feeds.Count} feed(s), {gauge.Bands.Count} band(s)");
        report.AppendLine();

        report.AppendLine($"Hooks ({build.Rules.Count})");
        foreach (var rule in build.Rules)
            report.AppendLine($"  {rule.Origin,-28} on {rule.Rule.Event} → {string.Join(" + ", rule.Rule.Payload.Select(e => e.Kind))}");

        if (build.Suffix is { } suffix)
        {
            report.AppendLine();
            report.AppendLine($"Suffix     {suffix.Fantasy}");
            report.AppendLine(suffix.IsFullyExpressed
                ? $"           {suffix.For(build.Channel)!.Drawback}"
                : "           (roster entry — no mechanics authored yet)");
        }

        // E0: hooks are live. Until E3 registers effect handlers, firing and landing in
        // Unhandled is the expected outcome — the point is that it is now *visible*.
        report.AppendLine();
        report.AppendLine($"Live hooks  {_ruleEngine.Fired.Count} fired  ·  {_ruleEngine.Unhandled.Count} awaiting a handler");
        foreach (var recent in _ruleEngine.Fired.TakeLast(6))
            report.AppendLine($"  fired  {recent.Source,-22} {recent.Trigger.Kind} → {recent.Kind}");

        return report.ToString().TrimEnd();
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
        _actionStore.GetAll().OrderBy(a => a.ProfessionId).ThenBy(a => a.Id).ToList();

    public string ProfessionName(string professionId) =>
        _professionStore.TryGetById(professionId, out var def) ? def.Name : professionId;

    public ProfessionDefinition? ProfessionById(string professionId) =>
        _professionStore.TryGetById(professionId, out var def) ? def : null;

    public bool IsPassiveRunning => _passiveRunner.IsRunning;

    /// <summary>True when a selection is held but its materials ran out — it is waiting, not
    /// forgotten (Phase 10 auto-repeat).</summary>
    public bool IsPassiveWaiting => _passiveRunner.IsWaiting;

    /// <summary>The standing training selection — what the player chose, whether or not it can
    /// run this instant.</summary>
    public string? CurrentPassiveActionId => _passiveRunner.SelectedActionId;
    public double PassiveProgress => _passiveRunner.Progress();

    /// <summary>The sum of every profession's level — what a global synergy is measured
    /// against. The roster lives in content, so a profession never touched still counts as the
    /// level it starts at rather than as nothing.</summary>
    public int TotalProfessionLevel =>
        _professionStore.GetAll().Sum(profession => _professions.GetProgress(profession.Id).Level);

    public void StartPassive(string actionId)
    {
        if (!_passiveRunner.Start(actionId))
        {
            Emit($"[Passive] Cannot start {ActionName(actionId)} ({_professions.CheckExecutable(actionId)}).");
            return;
        }

        Emit(_passiveRunner.IsWaiting
            ? $"[Passive] {ActionName(actionId)} selected — waiting on materials. It starts by itself when they arrive."
            : $"[Passive] Started {ActionName(actionId)}.");
        SetRunning(true); // passive gathering only advances while the sim runs
    }

    public void StopPassive()
    {
        if (_passiveRunner.SelectedActionId is not { } actionId)
            return;
        _passiveRunner.Stop();
        Emit($"[Passive] Stopped {ActionName(actionId)}.");
    }

    public void ActiveAttempt(string actionId, double performance)
    {
        var outcome = _professions.Execute(actionId, performance, isActive: true);
        if (!outcome.Success)
        {
            Emit($"[Active] {ActionName(actionId)} failed ({outcome.Failure}).");
            return;
        }

        if (outcome.AttemptMissed)
            Emit($"[Active] {ActionName(actionId)} — it got away (xp +{outcome.XpGained}).");
        else
            Emit($"[Active] {ActionName(actionId)} (timing {performance:P0}) → {DescribeProduced(outcome)} (xp +{outcome.XpGained}).");

        if (outcome.RealmKnowledgeGained is { } knowledge)
        {
            AddKnowledge(knowledge.RealmId, knowledge.Amount);
            Emit($"[Survey] Realm Knowledge +{knowledge.Amount} ({Knowledge(knowledge.RealmId)} total).");
        }

        OfferOpportunity(actionId, outcome.DiscoveredOpportunity);
    }

    // --- The active layer: Discover → Pursue / Ignore ------------------------
    //
    // Core resolves the gamble instantly and deterministically; *when* the result arrives is
    // the client's business, which is why the time cost lives here on the TickEngine rather
    // than in ProfessionSystem (docs/code-map.md §10.14).

    /// <summary>An offer waiting on the player, and the action that surfaced it.</summary>
    public sealed record PendingOpportunity(string ActionId, ProfessionOpportunityDefinition Offer);

    public PendingOpportunity? PendingOffer => _pendingOpportunity;

    public bool IsPursuingOpportunity => _pursuitInProgress is not null;

    /// <summary>Progress through an in-flight pursuit in [0, 1], for the UI bar.</summary>
    public double PursuitProgress()
    {
        if (_pursuitInProgress is null)
            return 0.0;
        var span = _pursuitEndTick - _pursuitStartTick;
        return span <= 0 ? 0.0 : Math.Clamp((double)(_tick.CurrentTick - _pursuitStartTick) / span, 0.0, 1.0);
    }

    private void OfferOpportunity(string actionId, ProfessionOpportunityDefinition? offer)
    {
        if (offer is null)
            return;

        _pendingOpportunity = new PendingOpportunity(actionId, offer);
        Emit($"[Opportunity] {offer.Name} — {offer.Prompt}");
        OpportunityOffered?.Invoke(_pendingOpportunity);
    }

    /// <summary>Takes the pending offer. The extra time is spent on the shared tick engine, so
    /// inside a Realm it is genuinely time not spent heading for the portal.</summary>
    public void PursuePendingOpportunity()
    {
        if (_pendingOpportunity is null || _pursuitInProgress is not null)
            return;

        var pending = _pendingOpportunity;
        _pendingOpportunity = null;

        _pursuitStartTick = _tick.CurrentTick;
        _pursuitEndTick = _tick.CurrentTick + pending.Offer.ExtraIntervalTicks;
        // The card is dismissed below, when the pursuit starts — deliberately not again here.
        // Another attempt may have surfaced a fresh offer in the meantime, and hiding it when
        // this pursuit lands would throw away a decision the player has not made yet.
        _pursuitInProgress = _tick.Schedule(pending.Offer.ExtraIntervalTicks, () =>
        {
            _pursuitInProgress = null;
            _professions.PursueOpportunity(pending.ActionId, pending.Offer.Id); // logs via OnOpportunityResolved
        });

        Emit($"[Opportunity] Pursuing {pending.Offer.Name}…");
        SetRunning(true); // the pursuit only resolves while the sim runs
        OpportunityOffered?.Invoke(null);
    }

    /// <summary>Walks away. Costs nothing — the attempt's own yield already landed.</summary>
    public void DeclinePendingOpportunity()
    {
        if (_pendingOpportunity is null)
            return;

        Emit($"[Opportunity] Left {_pendingOpportunity.Offer.Name} alone.");
        _pendingOpportunity = null;
        OpportunityOffered?.Invoke(null);
    }

    private void OnOpportunityResolved(OpportunityOutcome outcome)
    {
        var offer = _professions.TryGetOpportunity(outcome.ActionId, outcome.OpportunityId, out var definition)
            ? definition.Name
            : outcome.OpportunityId;

        Emit(outcome.Success
            ? $"[Opportunity] {offer} paid off → {DescribeStacks(outcome.Produced)} (xp +{outcome.XpGained})."
            : $"[Opportunity] {offer} came to nothing (xp +{outcome.XpGained}).");
    }

    public string ProfessionSummary()
    {
        var report = new StringBuilder();
        foreach (var category in Enum.GetValues<ProfessionCategory>())
        {
            var inCategory = _professionStore.GetAll()
                .Where(p => p.Category == category)
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToList();
            if (inCategory.Count == 0)
                continue;

            report.AppendLine($"— {category} —");
            foreach (var def in inCategory)
            {
                var progress = _professions.GetProgress(def.Id);
                report.AppendLine($"{def.Name,-16} L{progress.Level,-3} (xp {progress.Xp}, {progress.ProgressToNextLevel:P0} to next)");
            }
        }

        return report.ToString().TrimEnd();
    }

    /// <summary>Every profession, grouped for the UI's category headings.</summary>
    public IReadOnlyList<ProfessionDefinition> ProfessionsIn(ProfessionCategory category) =>
        _professionStore.GetAll()
            .Where(p => p.Category == category)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

    public IReadOnlyList<ProfessionActionDefinition> ActionsFor(string professionId) =>
        _actionStore.GetAll()
            .Where(a => a.ProfessionId == professionId)
            .OrderBy(a => a.RequiredLevel)
            .ThenBy(a => a.Id, StringComparer.Ordinal)
            .ToList();

    public int ProfessionLevel(string professionId) => _professions.GetProgress(professionId).Level;

    public bool CanRunAction(string actionId) => _professions.CanExecute(actionId);

    /// <summary>
    /// One action's mastery and what it currently buys, for the ladder.
    ///
    /// <para>The benefits are named in gameplay language rather than as the raw fractions the
    /// ladder stores — "quicker", "saves", "doubles" — because a row of three percentages is
    /// simulation language on a normal play surface (D30). What the player needs to know is
    /// which things have switched on.</para>
    /// </summary>
    public string MasteryReadout(string actionId)
    {
        var mastery = _professions.MasteryOf(actionId);
        var level = MasteryLeveling.LevelFor(mastery);
        if (level == 0)
            return "mastery —";

        var professionId = _professions.GetAction(actionId).ProfessionId;
        var benefits = _professions.Benefits.Mastery; // this line reads what MASTERY bought, not the total
        var bought = new List<string>();

        void Note(ProfessionBenefitKind kind, string label)
        {
            if (benefits.ValueOf(kind, professionId, mastery) > 0)
                bought.Add(label);
        }

        Note(ProfessionBenefitKind.IntervalReduction, "quicker");
        Note(ProfessionBenefitKind.BonusOutputChance, "luckier");
        Note(ProfessionBenefitKind.InputPreservation, "saves materials");
        Note(ProfessionBenefitKind.OutputDoubling, "doubles");

        var next = NextMasteryUnlock(professionId, level);
        var earned = bought.Count == 0 ? string.Empty : $" — {string.Join(", ", bought)}";
        return $"mastery {level}/{MasteryLeveling.MaxLevel}{earned}{next}";
    }

    /// <summary>
    /// What the rest of the player's progress is currently doing for this profession — the
    /// cross-profession and global bonuses, each with the level paying for it (Phase 10).
    ///
    /// <para>Shown because a bonus the player never sees is a bonus they do not believe in, which
    /// is the state every progression track in this project has had to be dug out of. Only
    /// synergies that are actually paying appear: a locked one is a promise, and the ladder is
    /// where promises belong.</para>
    /// </summary>
    public IReadOnlyList<string> SynergyReadout(string professionId)
    {
        var benefits = _professions.Benefits;
        var lines = new List<string>();

        foreach (var synergy in benefits.Synergies.Reaching(professionId))
        {
            var sourceLevel = benefits.SourceLevelOf(synergy);
            var value = synergy.ValueAt(sourceLevel);
            if (value <= 0)
                continue;

            var source = synergy.IsGlobalSource
                ? $"Everything you have learned (total {sourceLevel})"
                : $"{ProfessionName(synergy.SourceProfession!)} L{sourceLevel}";
            lines.Add($"{source} → {SynergyEffectLabel(synergy.Kind)} {value:P1}");
        }

        return lines;
    }

    private static string SynergyEffectLabel(ProfessionBenefitKind kind) => kind switch
    {
        ProfessionBenefitKind.IntervalReduction => "quicker by",
        ProfessionBenefitKind.InputPreservation => "saves materials",
        ProfessionBenefitKind.OutputDoubling => "doubles",
        ProfessionBenefitKind.BonusOutputChance => "luckier finds",
        ProfessionBenefitKind.OpportunityChance => "notices more",
        ProfessionBenefitKind.OpportunityRisk => "safer chances",
        _ => kind.ToString(),
    };

    /// <summary>The next rung and what it costs, so the ladder reads as a ladder rather than as
    /// a number. Empty once everything has switched on.</summary>
    private string NextMasteryUnlock(string professionId, int level)
    {
        var pending = new[]
        {
            (Kind: ProfessionBenefitKind.InputPreservation, Label: "saves materials"),
            (Kind: ProfessionBenefitKind.OutputDoubling, Label: "doubles"),
        }
        .Select(rung => (rung.Label, At: _professions.Benefits.Mastery.UnlockLevelOf(rung.Kind, professionId)))
        .Where(rung => rung.At is { } at && at > level)
        .OrderBy(rung => rung.At)
        .ToList();

        return pending.Count == 0 ? string.Empty : $"; at {pending[0].At} it {pending[0].Label}";
    }

    // --- Farming plots ------------------------------------------------------

    public IReadOnlyList<FarmingPlot> FarmingPlotsView => _farmingPlots.Plots;
    public int UnlockedFarmingPlots => _farmingPlots.UnlockedPlots;
    public IReadOnlyList<ProfessionActionDefinition> PlantableActions() => _farmingPlots.PlantableActions();

    public void PlantCrop(int plotIndex, string actionId)
    {
        var failure = _farmingPlots.Plant(plotIndex, actionId, _tick.CurrentTick);
        Emit(failure == PlotFailure.None
            ? $"[Farm] Planted {ActionName(actionId)} in plot {plotIndex + 1}."
            : $"[Farm] Cannot plant {ActionName(actionId)} ({failure}).");

        if (failure == PlotFailure.None)
            SetRunning(true); // crops only grow while the clock runs
    }

    public void HarvestPlot(int plotIndex)
    {
        var outcome = _farmingPlots.Harvest(plotIndex, _tick.CurrentTick, out var failure);
        if (outcome is null)
        {
            Emit($"[Farm] Cannot harvest plot {plotIndex + 1} ({failure}).");
            return;
        }

        Emit($"[Farm] Plot {plotIndex + 1} → {DescribeProduced(outcome)} (xp +{outcome.XpGained}).");
    }

    /// <summary>Growth in [0, 1] for a plot's UI bar.</summary>
    public double PlotProgress(int plotIndex)
    {
        if (plotIndex < 0 || plotIndex >= _farmingPlots.Plots.Count)
            return 0.0;
        var plot = _farmingPlots.Plots[plotIndex];
        if (plot.IsEmpty)
            return 0.0;
        return plot.Progress(_tick.CurrentTick, _professions.EffectiveIntervalTicks(plot.PlantedActionId!));
    }

    // --- Agility training course --------------------------------------------

    public IReadOnlyList<TrainingObstacleDefinition> ObstaclesFor(TrainingSlot slot) =>
        _trainingCourse.AvailableFor(slot);

    public string? FittedObstacle(TrainingSlot slot) =>
        _trainingCourse.Fitted.TryGetValue(slot, out var id) ? id : null;

    public void FitObstacle(TrainingSlot slot, string obstacleId)
    {
        var failure = _trainingCourse.Fit(slot, obstacleId);
        Emit(failure == CourseFitFailure.None
            ? $"[Course] Fitted {ObstacleName(obstacleId)} to the {slot} slot."
            : $"[Course] Cannot fit {ObstacleName(obstacleId)} ({failure}).");
    }

    public void ClearObstacle(TrainingSlot slot)
    {
        _trainingCourse.Clear(slot);
        Emit($"[Course] Cleared the {slot} slot.");
    }

    public void RunTrainingLap()
    {
        var xp = _trainingCourse.RunLap();
        Emit(xp > 0
            ? $"[Course] Ran a lap ({_trainingCourse.LapIntervalTicks() / (double)TicksPerSecond:0.#}s) — Agility xp +{xp}."
            : "[Course] Nothing is fitted; there is no course to run.");
    }

    /// <summary>The course's standing utility, in the player's language.</summary>
    public string TrainingCourseSummary()
    {
        var report = new StringBuilder();
        report.AppendLine($"Agility L{ProfessionLevel(TrainingCourse.AgilityProfessionId)} · " +
                          $"lap {_trainingCourse.LapIntervalTicks() / (double)TicksPerSecond:0.#}s for {_trainingCourse.LapExperience()} xp");

        var bonuses = _trainingCourse.ActiveBonuses();
        if (bonuses.Count == 0)
        {
            report.Append("No obstacles fitted — no standing bonuses.");
            return report.ToString();
        }

        foreach (var bonus in bonuses.OrderBy(b => b.Key, StringComparer.Ordinal))
            report.AppendLine($"  {CourseBonusLabel(bonus.Key)}  +{bonus.Value:P0}");

        return report.ToString().TrimEnd();
    }

    private static string CourseBonusLabel(string bonusKey) => bonusKey switch
    {
        CourseBonusKeys.RealmTravelSpeed => "Realm travel",
        CourseBonusKeys.GatheringSpeed => "Gathering speed",
        CourseBonusKeys.ExtractionSpeed => "Extraction speed",
        CourseBonusKeys.HazardAvoidance => "Hazard avoidance",
        CourseBonusKeys.OpportunitySafety => "Opportunity safety",
        _ => bonusKey,
    };

    private string ObstacleName(string obstacleId) =>
        _obstacleStore.TryGetById(obstacleId, out var obstacle) ? obstacle.Name : obstacleId;

    // --- Hideout stations ----------------------------------------------------
    //
    // Routing queries only. A station decides *where* the player stands; every gate still lives
    // where it always did, so a crafting action offered here is the same action with the same
    // profession requirement it would have anywhere else.

    /// <summary>The stations on one Hideout shelf, filed under the category of the profession
    /// each is named for. Mirrors <see cref="ProfessionsIn"/> so the two orders agree.</summary>
    public IReadOnlyList<StationDefinition> StationsIn(ProfessionCategory category) =>
        _stationStore.GetAll()
            .Where(station => ProfessionById(station.PrimaryProfessionId)?.Category == category)
            .OrderBy(station => station.Name, StringComparer.Ordinal)
            .ToList();

    // --- Assay ---------------------------------------------------------------

    /// <summary>How much of a material's reading the player has earned the right to see.</summary>
    public AssayDepth CurrentAssayDepth => AssayLens.DepthFor(ProfessionLevel("profession.assay"));

    /// <summary>The Assay inspector, redacted to the player's level. Assay never changes what
    /// a material is — only how much of it is legible (D30, rule 7). Re-aimed at the identity
    /// model in Phase 6 (D45/D48): latents, vessel, leanings, potential. Every material is
    /// migrated (D52), so an unreadable id is the only fallback left.</summary>
    public string MaterialSummaryAssayed(string materialId)
    {
        if (!_materials.TryGetById(materialId, out var material)
            || IdentityStateResolver.StateOf(material) is not { } identityState)
        {
            return materialId;
        }

        return AssayLens.IdentityMaterial(
            material.Name, identityState,
            RootDerivations.ProfileOf(identityState, _materials),
            _content, CurrentAssayDepth);
    }

    public string InventoryReport()
    {
        var report = new StringBuilder();
        report.AppendLine($"STASH (secured) — {_stash.Gold} gold:");
        report.AppendLine(FormatInventory(_stash));

        if (InRealm)
        {
            report.AppendLine();
            report.AppendLine($"UNSECURED — lost if you die{DescribeCoin(_run!.RunInventory.Gold)}:");
            report.AppendLine(FormatInventory(_run!.RunInventory));
        }

        return report.ToString().TrimEnd();
    }

    /// <summary>Coin, only when there is any — an extraction report that always says "and 0
    /// gold" trains the player to stop reading it.</summary>
    private static string DescribeCoin(long gold) => gold > 0 ? $" and {gold} gold" : string.Empty;

    private string FormatInventory(Inventory inventory)
    {
        var lines = inventory.Snapshot().OrderBy(s => s.ItemId)
            .Select(s => $"  {ItemName(s.ItemId),-16} x{s.Quantity}")
            .ToList();

        foreach (var instance in inventory.Instances.OrderBy(i => i.DisplayName))
            lines.Add("  " + ItemLabel(instance));

        return lines.Count == 0 ? "  (empty)" : string.Join("\n", lines);
    }

    // --- Emergent crafting ---------------------------------------------------
    //
    // These are thin forwards. Every rule lives in Core's MaterialTransformationEngine; GameRoot only turns
    // an outcome into log lines and change events, so the flagged Application-layer extraction
    // does not get any harder than it already is.

    /// <summary>
    /// Materials currently on hand, for the crafting pickers. Emergent archetypes appear here
    /// alongside authored ones with no special-casing — that is the whole point of registering
    /// them into the same store (DECISIONS D20).
    /// </summary>
    public IReadOnlyList<(string Id, string Name, int Quantity)> MaterialsOnHand =>
        ActiveInventory.Snapshot()
            .Where(s => _materials.Contains(s.ItemId))
            .Select(s => (s.ItemId, _materials.GetById(s.ItemId).Name, s.Quantity))
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();


    private string DescribeStacks(IReadOnlyList<ItemStack> stacks) =>
        stacks.Count == 0
            ? "nothing"
            : string.Join(", ", stacks.Select(s => $"{ItemName(s.ItemId)} ×{s.Quantity}"));

    // --- The identity bench (migration Phase 2c) -----------------------------

    /// <summary>A material's identity-model state, or null when it has not been migrated.</summary>
    public IdentityMaterialState? IdentityStateOf(string itemId) =>
        _materials.TryGetById(itemId, out var definition)
            ? IdentityStateResolver.StateOf(definition)
            : null;

    /// <summary>A material's tags — what the forge's slot gates filter by.</summary>
    public IReadOnlyList<string> MaterialTagsOf(string itemId) =>
        _materials.TryGetById(itemId, out var definition) ? definition.Tags : Array.Empty<string>();

    /// <summary>An identity's player-facing name.</summary>
    public string IdentityNameOf(string identityId) =>
        _content.Identities.TryGetById(identityId, out var identity) ? identity.Name : identityId;

    /// <summary>A picker row's identity summary: the stakes in rank words plus the overfill
    /// word — " · Dense, Vital (improved) · Unstable". Empty for unmigrated stock.</summary>
    public string MaterialStakeSummary(string itemId)
    {
        if (IdentityStateOf(itemId) is not { } state)
            return string.Empty;

        var stakes = state.Identities.Count > 0
            ? " · " + string.Join(", ", state.Identities.Select(stake => IdentityPhrases.Stake(stake, _content)))
            : string.Empty;
        return stakes + IdentityPhrases.StabilityMarker(state) + IdentityPhrases.ConditionMarker(state);
    }

    /// <summary>The bench inspector's material card: every §11.2 facet in a sentence.</summary>
    public string IdentityMaterialSummary(string itemId) =>
        IdentityStateOf(itemId) is { } state
            ? IdentityMaterialReadings.Summary(state, _content)
            : string.Empty;

    /// <summary>A verb refusal in words — deterministic, previewable, never an enum name.</summary>
    public string VerbRefusalText(VerbFailureReason reason) => VerbReadings.Refusal(reason);

    /// <summary>The bench preview in the player voice: what would change, then the odds.</summary>
    public IReadOnlyList<string> VerbProjectionReading(
        VerbActionDefinition action, string substrateItemId, VerbProjection projection)
    {
        if (IdentityStateOf(substrateItemId) is not { } before)
            return Array.Empty<string>();
        return VerbReadings.ProjectionLines(
            action.Verb, before, projection, VerbOutputName(action), _content);
    }

    private string? VerbOutputName(VerbActionDefinition action) =>
        action.Output is { } outputId && _materials.TryGetById(outputId, out var output)
            ? output.Name
            : null;

    /// <summary>The forge preview in the player voice (Phase 6, D53).</summary>
    public string IdentityMintPreviewText(
        IdentityComposition composition, ItemEffectProjection effects, bool firstOfItsKind) =>
        MintReadings.Preview(composition, effects, firstOfItsKind, _content);

    /// <summary>The forge preview's Advanced voice: exact scores and engine ids.</summary>
    public string IdentityMintAdvancedText(IdentityComposition composition, ItemEffectProjection effects) =>
        MintReadings.Advanced(composition, effects, _content);

    /// <summary>One picker line: fiction name, verb, and the action's own gate. The verb enum
    /// words are the design's own vocabulary (D47) — they are the player words.</summary>
    public string VerbActionLabel(VerbActionDefinition action)
    {
        var gate = string.IsNullOrEmpty(action.Profession)
            ? "any hands"
            : $"{(_content.Professions.TryGetById(action.Profession, out var profession) ? profession.Name : action.Profession)} L{action.RequiredLevel}";
        return $"{action.Name} — {action.Verb} · {gate}";
    }

    /// <summary>The identity-system actions this station's bench offers.</summary>
    public IReadOnlyList<VerbActionDefinition> VerbActionsAt(string stationId) =>
        _content.Stations.TryGetById(stationId, out var station)
            ? station.VerbActions
                .Where(id => _content.VerbActions.Contains(id))
                .Select(id => _content.VerbActions.GetById(id))
                .ToList()
            : Array.Empty<VerbActionDefinition>();

    /// <summary>What a verb action would do, before committing — gates included.</summary>
    public VerbActionPreview PreviewVerbAction(
        string actionId, string substrateItemId, IReadOnlyList<string> sourceItemIds,
        string? targetIdentityId = null, string? displacedIdentityId = null) =>
        _verbActionRunner.Preview(new VerbActionInvocation(
            actionId, substrateItemId, sourceItemIds, targetIdentityId, displacedIdentityId));

    /// <summary>Runs a verb action and reports it: gates → verb → consume → register → deposit.
    /// The log speaks the Phase 6 player voice — the before-state is captured ahead of the
    /// run so the outcome reading can diff what actually changed.</summary>
    public VerbActionResult RunVerbAction(
        string actionId, string substrateItemId, IReadOnlyList<string> sourceItemIds,
        string? targetIdentityId = null, string? displacedIdentityId = null)
    {
        var substrateBefore = IdentityStateOf(substrateItemId);
        var result = _verbActionRunner.Run(new VerbActionInvocation(
            actionId, substrateItemId, sourceItemIds, targetIdentityId, displacedIdentityId));

        if (result.GateFailure is not null)
        {
            Emit($"[Bench] {result.GateDetail}");
            return result;
        }
        if (result.Outcome is { Kind: VerbResultKind.Refused } refused)
        {
            Emit($"[Bench] {VerbReadings.Refusal(refused.Failure ?? VerbFailureReason.MissingTargetIdentity)}");
            return result;
        }

        if (substrateBefore is not null && _content.VerbActions.TryGetById(actionId, out var action))
        {
            foreach (var line in VerbReadings.OutcomeLines(
                action.Verb, substrateBefore, result.Outcome!, VerbOutputName(action), _content))
            {
                Emit("  " + line);
            }
        }
        foreach (var item in result.Deposited)
            Emit(item.FirstDiscovery
                ? $"[Bench] First discovery — {item.Name}!"
                : $"[Bench] {item.Name} added to the bag.");

        // Phase 5 — the bench trains: announce the pay the same way profession actions do.
        if (result.XpAwarded > 0)
            Emit($"[Bench] xp +{result.XpAwarded}.");
        if (result.LevelUp is { } levelUp)
            Emit($"[Level] {ProfessionName(levelUp.ProfessionId)} reached level {levelUp.NewLevel}!");

        InventoryChanged?.Invoke();
        if (result.AnyFirstDiscovery)
            DiscoveryChanged?.Invoke();
        return result;
    }

    // --- Identity fabrication (migration Phase 3, D50/D51) --------------------

    /// <summary>The identity forge's menu: every form migrated to the identity model.</summary>
    public IReadOnlyList<EquipmentBlueprintDefinition> IdentityForgeForms() =>
        _content.Forms.GetAll()
            .Where(form => form.IdentityCap is not null)
            .OrderBy(form => form.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>What a mint would produce, before committing: the composed item side and
    /// the full effect projection — guaranteed floor, scored candidate table, odds.</summary>
    public IdentityFabricationPreview PreviewIdentityFabrication(
        string formId, IReadOnlyDictionary<string, string> componentItemIdsBySlot) =>
        _identityFabrication.Preview(new IdentityFabricationInvocation(formId, componentItemIdsBySlot));

    /// <summary>Mints an item at the identity forge: compose → resolve → consume → deposit.</summary>
    public IdentityFabricationResult RunIdentityFabrication(
        string formId, IReadOnlyDictionary<string, string> componentItemIdsBySlot)
    {
        var result = _identityFabrication.Fabricate(
            new IdentityFabricationInvocation(formId, componentItemIdsBySlot));

        if (result.GateFailure is not null)
        {
            Emit($"[Forge] {result.GateDetail}");
            return result;
        }
        if (result.CompositionFailure != IdentityCompositionFailure.None)
        {
            Emit($"[Forge] The composition refuses: {result.CompositionFailure}.");
            return result;
        }

        Emit(result.FirstOfItsKind
            ? $"[Forge] First of its kind — {result.Item!.DisplayName}!"
            : $"[Forge] {result.Item!.DisplayName} forged.");

        InventoryChanged?.Invoke();
        if (result.FirstOfItsKind)
            DiscoveryChanged?.Invoke();
        return result;
    }

    // --- Legacy fixed-interaction crafting -----------------------------------
    //
    // Superseded by the reaction engine. Only the Healing Salve recipe remains, because
    // consumables are produced by fabrication (P5c) and there is no emergent path to one yet.
    // Delete this whole section when P5c lands.

    /// <summary>The interactions offered at a station — the ones gated on a profession it
    /// trains, so the Healing Salve is brewed at the Apothecary rather than anywhere.</summary>
    public IReadOnlyList<CraftingInteractionDefinition> InteractionsAt(StationDefinition station) =>
        _interactions.GetAll()
            .Where(interaction => interaction.ProfessionRequirements.Any(r => station.Hosts(r.ProfessionId)))
            .OrderBy(interaction => interaction.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>True once the player has discovered what this interaction makes.</summary>
    public bool IsDiscovered(string discoveryId) => _discoveries.IsDiscovered(discoveryId);

    /// <summary>Runs one known interaction by id, through the same experiment path a blind
    /// combination takes — so a listed interaction and a guessed one cannot diverge.</summary>
    public void MakeInteraction(string interactionId)
    {
        if (!_interactions.TryGetById(interactionId, out var interaction))
            return;

        Experiment(interaction.Inputs.Select(input => input.ItemId).ToArray());
    }

    public void Experiment(params string[] itemIds)
    {
        var outcome = _legacyInteractionCrafting.Experiment(itemIds);
        if (outcome.Success)
        {
            Emit($"[Craft] Made {outcome.ResultQuantity} {ItemName(outcome.ResultItemId!)}.");
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
        if (AutoCombatEnabled)
            EngagePilot();
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

    // --- Auto-combat (Phase 10, GDD §5.7) -----------------------------------
    //
    // The pilot only ever issues the commands directly above. Everything about the fight —
    // timing, telegraphs, costs, statuses, damage, defences, cooldowns, triggers — resolves in
    // the one encounter, on the one tick engine, exactly as it does for a hand on the keyboard.

    private AutoCombatPilot? _pilot;

    public IReadOnlyList<AutoCombatProfileDefinition> AutoCombatProfiles =>
        _autoCombatProfiles.GetAll().OrderBy(profile => profile.Id, StringComparer.Ordinal).ToList();

    /// <summary>Whether the player has handed the fight over. Persisted nowhere on purpose:
    /// letting a saved game resume in auto is a decision the player should make while looking
    /// at the fight, not one a save file makes for them.</summary>
    public bool AutoCombatEnabled { get; private set; }

    /// <summary>Which brain plays. Null until content loads; the first profile by id otherwise.</summary>
    public string? AutoCombatProfileId { get; private set; }

    public event Action? AutoCombatChanged;

    public void SetAutoCombatEnabled(bool enabled)
    {
        if (AutoCombatEnabled == enabled)
            return;

        AutoCombatEnabled = enabled;
        if (enabled)
            EngagePilot();
        else
            DisengagePilot();

        Emit(enabled
            ? $"[Auto] Auto-combat on ({AutoCombatProfileName()}). It reacts in {ActiveAutoCombatProfile()?.ReactionTicks ?? 0} ticks — too slow for a Perfect Block or a Parry."
            : "[Auto] Auto-combat off.");
        AutoCombatChanged?.Invoke();
    }

    public void SetAutoCombatProfile(string profileId)
    {
        if (!_autoCombatProfiles.Contains(profileId) || profileId == ActiveAutoCombatProfile()?.Id)
            return;

        AutoCombatProfileId = profileId;

        // Swapping brains mid-fight takes effect immediately: the pilot holds the profile, so it
        // is rebuilt rather than mutated.
        if (AutoCombatEnabled)
        {
            DisengagePilot();
            EngagePilot();
        }

        Emit($"[Auto] Brain set to {AutoCombatProfileName()}.");
        AutoCombatChanged?.Invoke();
    }

    public AutoCombatProfileDefinition? ActiveAutoCombatProfile() =>
        AutoCombatProfileId is { } id && _autoCombatProfiles.TryGetById(id, out var profile)
            ? profile
            : _autoCombatProfiles.GetAll().OrderBy(candidate => candidate.Id, StringComparer.Ordinal).FirstOrDefault();

    public string AutoCombatProfileName() => ActiveAutoCombatProfile()?.Name ?? "none";

    private void EngagePilot()
    {
        if (!_encounter.IsActive || ActiveAutoCombatProfile() is not { } profile)
            return;

        _pilot = new AutoCombatPilot(_encounter, _tick, profile, _combatRandom);
        _pilot.Decided += Emit;
        _pilot.Engage();
    }

    private void DisengagePilot()
    {
        if (_pilot is null)
            return;
        _pilot.Decided -= Emit;
        _pilot.Disengage();
        _pilot = null;
    }

    // --- Item cards (the §6 reveal, identity layer since Phase 6) ---------------------------

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
        _consumables.GetAll().Where(c => ActiveInventory.Contains(c.Id)).OrderBy(c => c.Name).ToList();

    public void CombatUseConsumable(string itemId)
    {
        if (!_encounter.IsActive)
            return;
        if (!_consumables.TryGetById(itemId, out var consumable))
            return;
        if (!ActiveInventory.Contains(itemId))
        {
            Emit($"[Combat] No {consumable.Name} to use.");
            return;
        }

        ActiveInventory.TryRemove(itemId, 1);
        _encounter.UseHealingItem(consumable.Name, consumable.HealAmount);
        InventoryChanged?.Invoke();
    }

    public string CombatReport()
    {
        if (!_everFought)
            return "(no combat yet — start a fight below)";

        var now = _tick.CurrentTick;
        var report = new StringBuilder();
        foreach (var combatant in _encounter.Combatants)
        {
            var stance = combatant.IsDodging(now) ? "  [DODGING]" : combatant.IsBlocking(now) ? "  [BLOCKING]" : string.Empty;
            report.AppendLine($"{combatant.Name,-16} HP {combatant.Health.Current,3}/{combatant.Health.Max}   STA {combatant.Stamina.Current,3}/{combatant.Stamina.Max}{stance}");
        }

        if (_encounter.IsActive)
        {
            report.AppendLine(_encounter.PlayerReady ? "You are READY to act." : "You are recovering…");
            foreach (var intent in _encounter.Intents)
            {
                var seconds = Math.Max(0, intent.ExecuteTick - now) / (double)TicksPerSecond;
                var type = intent.Move.Packets.Count > 0 ? intent.Move.Packets[0].Type.ToString() : intent.Move.Kind.ToString();
                report.AppendLine($"⚠ {intent.Attacker.Name}: {intent.Move.Name} — impact in {seconds:0.0}s ({type})");
            }
        }
        else
        {
            report.AppendLine("Combat over.");
        }

        return report.ToString().TrimEnd();
    }

    private void OnCombatEnded(CombatOutcome outcome)
    {
        DisengagePilot(); // the fight is over; nothing should still be scheduling decisions

        if (outcome.Result == CombatResult.Victory)
        {
            foreach (var enemy in outcome.DefeatedEnemies)
            {
                // The enemy's own identity tags join the circumstances, which is how an `elite`
                // or `boss` enemy reaches its spoils without combat knowing what a rank is.
                GrantLoot(enemy.LootTableIds, enemy.Name, LootCircumstances(enemy.Tags));
                AwardCharacterXp(CharacterLeveling.XpForDefeating(enemy.Health.Max, EnemyRanks.Of(enemy.Tags)));
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

    // --- Loot ---------------------------------------------------------------
    //
    // Application glue only. What a table contains and how it rolls lives in Core's
    // LootResolver; GameRoot supplies the circumstances, banks the result into whichever bag is
    // current, and turns it into a log line.

    /// <summary>
    /// Where the party is, as a loot table sees it: depth, tier, the Realm's own tags, plus
    /// whatever the specific source contributes. Depth 0 <em>is</em> the Hideout, which is why
    /// no caller ever has to ask "am I in a Realm?" — a <c>minDepth</c> gate answers it.
    /// </summary>
    private LootContext LootCircumstances(IEnumerable<string>? sourceTags = null)
    {
        var tags = new List<string>();
        if (_run is { Active: true } run)
        {
            tags.Add(LootContextTags.InRealm);
            tags.Add(run.Realm.Id);
            tags.AddRange(run.Realm.Tags);
        }

        if (sourceTags is not null)
            tags.AddRange(sourceTags);

        return new LootContext(
            depth: _run is { Active: true } ? _run.CurrentDepth : 0,
            tier: _run is { Active: true } ? _run.Tier : 1,
            tags);
    }

    /// <summary>Rolls tables, banks the haul, reports it. Every loot source routes through here
    /// so the log reads the same everywhere and nothing can forget which bag it is filling.</summary>
    private void GrantLoot(IReadOnlyList<string> tableIds, string sourceName, LootContext circumstances)
    {
        if (tableIds.Count == 0)
            return;

        var loot = _lootResolver.Roll(tableIds, circumstances);
        if (loot.IsEmpty)
        {
            Emit($"[Loot] {sourceName} — nothing worth carrying.");
            return;
        }

        loot.DepositInto(ActiveInventory);
        Emit($"[Loot] {sourceName}: {DescribeLoot(loot)}");
    }

    private string DescribeLoot(LootResult loot)
    {
        var parts = loot.Drops
            .Select(drop => $"{ItemName(drop.ItemId)} ×{drop.Quantity}{RarityMark(drop.Rarity)}")
            .ToList();
        if (loot.Gold > 0)
            parts.Add($"{loot.Gold} gold");
        return string.Join(", ", parts);
    }

    /// <summary>A rare find should read as one. Common and uncommon carry no mark at all, so
    /// the mark still means something the day it appears.</summary>
    private static string RarityMark(LootRarity rarity) => rarity switch
    {
        LootRarity.Rare => " ★",
        LootRarity.VeryRare => " ★★",
        LootRarity.Exceptional => " ★★★",
        _ => string.Empty,
    };

    // --- Equipment ----------------------------------------------------------

    /// <summary>Equipment blueprints that can be granted into the stash (excludes the starter kit).</summary>
    public IReadOnlyList<EquipmentDefinition> EquipmentCatalog =>
        _equipment.GetAll().Where(e => e.Id != StarterWeaponId && e.Id != StarterArmorId).OrderBy(e => e.Name).ToList();

    /// <summary>The instance equipped in a slot, or null when empty. The UI walks
    /// <see cref="EquipmentSlots.DisplayOrder"/> rather than naming slots one at a time, so a
    /// new slot appears on the character sheet without a UI change.</summary>
    public ItemInstance? Equipped(EquipmentSlot slot) => _playerEquipment.InSlot(slot);

    public ItemInstance? EquippedWeapon => _playerEquipment.InSlot(EquipmentSlot.Weapon);

    /// <summary>Unequipped weapons/armor sitting in the Stash, ready to equip.</summary>
    public IReadOnlyList<ItemInstance> StashEquipment =>
        _stash.Instances.Where(i => i.ItemType is ItemType.Weapon or ItemType.Armor)
            .OrderBy(i => i.DisplayName).ToList();

    /// <summary>Where a piece of gear would be worn, read from its base definition. Null when
    /// the definition no longer resolves — a fabricated archetype the save could not restore.</summary>
    public EquipmentSlot? SlotOf(ItemInstance instance) =>
        instance is not null && _equipment.TryGetById(instance.BaseDefinitionId, out var definition)
            ? definition.Slot
            : null;

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
        // A second ring goes on the other hand rather than displacing the first (Core decides which).
        var displaced = _playerEquipment.EquipInFirstFreePosition(def.Slot, instance);
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
        static string DescribeLaneResistance(string lane, double raw)
        {
            var capped = Dungeons.Combat.Combatant.CapResistance(raw);
            return raw > capped + Dungeons.Combat.CombatTuning.MultiplierEpsilon
                ? $"{lane} {capped:P0}/{raw:P0}"
                : $"{lane} {capped:P0}";
        }

        var resistanceText = armorProfile.Resistances.Count == 0
            ? string.Empty
            : "  resist " + string.Join(", ",
                armorProfile.Resistances.Select(r => DescribeLaneResistance(r.Key, r.Value)));

        return $"{armorProfile.Armor:0.#} armor{resistanceText}";
    }


    public string EquipmentReport()
    {
        var report = new StringBuilder();
        report.AppendLine($"Weapon: {(EquippedWeapon?.DisplayName ?? "— (unarmed)")}  →  {EquippedWeaponSummary()}");

        // Every worn piece, then the mitigation they add up to. The total is the honest number:
        // a loadout defends as a set, and reading it piece by piece hides that.
        foreach (var slot in EquipmentSlots.DisplayOrder.Where(s => s != EquipmentSlot.Weapon))
            report.AppendLine($"{EquipmentSlotNames.PositionOf(slot),-8}{Equipped(slot)?.DisplayName ?? "— (empty)"}");

        report.Append($"Worn:   {EquippedArmorSummary()}");
        return report.ToString();
    }

    private void EquipStarterLoadout()
    {
        if (_playerEquipment.InSlot(EquipmentSlot.Weapon) is null && _equipment.Contains(StarterWeaponId))
            _playerEquipment.Equip(EquipmentSlot.Weapon, InstantiateEquipment(_equipment.GetById(StarterWeaponId)));
        if (_playerEquipment.InSlot(EquipmentSlot.Body) is null && _equipment.Contains(StarterArmorId))
            _playerEquipment.Equip(EquipmentSlot.Body, InstantiateEquipment(_equipment.GetById(StarterArmorId)));
    }

    private ItemInstance InstantiateEquipment(EquipmentDefinition def) => new()
    {
        InstanceId = _instanceIds.Next(),
        BaseDefinitionId = def.Id,
        ItemType = def.ItemType,
        DisplayName = def.Name,
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

        // Resolved ONCE: these are per-instance definitions with the weapon's mass already
        // applied, and they are needed twice — as grants, and as overrides in the store the
        // builder reads from. Resolving twice would risk the two halves disagreeing.
        var weapon = _playerEquipment.InSlot(EquipmentSlot.Weapon);
        var weaponMoves = weapon is not null
            && _equipment.TryGetById(weapon.BaseDefinitionId, out var weaponDefinition)
                ? EquipmentResolver.ResolveWeaponMoves(weaponDefinition, weapon, _moves)
                : Array.Empty<MoveDefinition>();

        foreach (var move in weaponMoves)
            grants.Add(new MoveGrant(new MoveGrantSpec { Id = move.Id }, weapon!.DisplayName));

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

        // Phase 3: identity sentences. convert behavior grants standing move modifiers —
        // the third grantor, same builder path as equipment and character components.
        foreach (var (identityInstance, compiled) in EquippedIdentityGrants())
            foreach (var moveModifierId in compiled.MoveModifierIds)
                if (_moveModifierStore.TryGetById(moveModifierId, out var identityMoveModifier))
                    modifierGrants.Add(new MoveModifierGrant(identityMoveModifier, identityInstance.DisplayName));

        // The store the builder reads from: the weapon's own mass-adjusted definitions win over
        // the shared store's for those ids, and everything else comes through unchanged.
        var moveStoreWithWeaponAdjustments = new DataStore<MoveDefinition>();
        var weaponAdjustedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var move in weaponMoves)
        {
            moveStoreWithWeaponAdjustments.Add(move);
            weaponAdjustedIds.Add(move.Id);
        }
        foreach (var move in _moves.GetAll().Where(m => !weaponAdjustedIds.Contains(m.Id)))
            moveStoreWithWeaponAdjustments.Add(move);

        var conflicts = new MovesetBuilder(moveStoreWithWeaponAdjustments)
            .Build(grants, modifierGrants, out var moveset);
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

    /// <summary>Mitigation from the whole loadout, not one piece. Head, body, hands, feet and
    /// the offhand all contribute; the weapon and the trinket do not (they are not
    /// armour-bearing slots). Core does the summing — this only gathers the worn pieces.</summary>
    private ArmorProfile ResolvePlayerArmor()
    {
        var worn = new List<(EquipmentDefinition Definition, ItemInstance? Instance)>();
        foreach (var (slot, instance) in _playerEquipment.Slots)
        {
            if (!EquipmentSlots.GrantsArmor(slot))
                continue;
            if (_equipment.TryGetById(instance.BaseDefinitionId, out var definition))
                worn.Add((definition, instance));
        }

        return EquipmentResolver.ResolveWornArmor(worn);
    }

    // --- Realm preparation --------------------------------------------------

    /// <summary>
    /// Where a player who has never opened the preparation screen is pointed. The Dark Forest is
    /// the reference Realm — the only one with fights, hazards, a shrine, an elite and a boss
    /// wired — so it is the one destination where "enter" means something. Named here for the
    /// same reason <see cref="StarterWeaponId"/> is: a content id the client must know.
    /// </summary>
    private const string DefaultRealmId = "realm.dark_forest";

    public event Action? LoadoutChanged;

    /// <summary>The Realm the loadout is prepared for. Null only if content ships no realms.</summary>
    public RealmDefinition? SelectedRealm =>
        _loadout.RealmId is { } realmId && _realms.TryGetById(realmId, out var realm) ? realm : null;

    public void SelectRealm(string realmId)
    {
        if (!_realms.Contains(realmId))
            return;

        _loadout.SelectRealm(realmId);
        LoadoutChanged?.Invoke();
    }

    /// <summary>
    /// Points the loadout somewhere real. Called after content load and after a save load,
    /// because a save may name a Realm this build of the game no longer ships — and a
    /// preparation screen with no destination is a dead end the player cannot fix.
    /// </summary>
    private void EnsureRealmSelected()
    {
        if (SelectedRealm is not null)
            return;

        var fallback = _realms.Contains(DefaultRealmId)
            ? DefaultRealmId
            : _realms.GetAll().OrderBy(realm => realm.Name).FirstOrDefault()?.Id;

        if (fallback is not null)
            _loadout.SelectRealm(fallback);
    }

    /// <summary>What the player is allowed to know about the selected Realm before committing.</summary>
    public RealmBriefing? Briefing() =>
        SelectedRealm is { } realm ? RealmBriefing.Compile(_content, realm, Knowledge(realm.Id)) : null;

    /// <summary>Which trades the selected Realm asks for, measured against the party's levels.</summary>
    public IReadOnlyList<FieldworkRequirement> Fieldwork() =>
        SelectedRealm is { } realm
            ? RealmFieldwork.Survey(_content, realm, Knowledge(realm.Id), ProfessionLevel)
            : Array.Empty<FieldworkRequirement>();

    /// <summary>Filled and empty slots, what is wrong, and whether the game owes a starter kit.</summary>
    public LoadoutReport LoadoutStatus() =>
        LoadoutCheck.Inspect(_playerEquipment, _stash, _loadout, _equipment.Contains);

    /// <summary>Every consumable the player could take, with how many are banked and how many
    /// are packed. Reads the <b>Stash</b>, never the active bag: you prepare in the Hideout.</summary>
    public IReadOnlyList<(ConsumableDefinition Consumable, int InStash, int Packed)> ConsumableChoices =>
        _consumables.GetAll()
            .Where(consumable => _stash.GetQuantity(consumable.Id) > 0 || _loadout.PackedQuantity(consumable.Id) > 0)
            .OrderBy(consumable => consumable.Name)
            .Select(consumable => (consumable, _stash.GetQuantity(consumable.Id), _loadout.PackedQuantity(consumable.Id)))
            .ToList();

    public void PackConsumable(string itemId, int quantity = 1)
    {
        _loadout.Pack(itemId, quantity);
        LoadoutChanged?.Invoke();
    }

    public void UnpackConsumable(string itemId, int quantity = 1)
    {
        _loadout.Unpack(itemId, quantity);
        LoadoutChanged?.Invoke();
    }

    public void ClearPack()
    {
        _loadout.ClearPacked();
        LoadoutChanged?.Invoke();
    }

    /// <summary>
    /// The depth the next expedition starts at, and the deepest one Realm Knowledge allows.
    ///
    /// <para>Held on the client rather than in <see cref="RunLoadout"/> and the save: unlike the
    /// destination and the pack, a starting depth means nothing once the run begins, and a
    /// persisted one would quietly send a returning player straight to depth 3.</para>
    /// </summary>
    public int StartingDepth { get; private set; } = 1;

    public int DeepestStartingDepth =>
        SelectedRealm is { } realm ? RealmRun.DeepestReachableEntry(realm, Knowledge(realm.Id)) : 1;

    public void SetStartingDepth(int depth)
    {
        var chosen = Math.Clamp(depth, 1, DeepestStartingDepth);
        if (chosen == StartingDepth)
            return;

        StartingDepth = chosen;
        LoadoutChanged?.Invoke();
    }

    /// <summary>
    /// Hands out the starter weapon and armour, but only to a player with nothing to fight with.
    ///
    /// <para>This is GDD §13.1's guarantee made reachable: persistent progression survives death,
    /// and <b>a fresh or broke character can never be bricked</b>. The check is
    /// <see cref="LoadoutCheck"/>'s, so "broke" means no weapon worn <em>and</em> none in the
    /// Stash — a player who simply has not equipped their sword gets told to equip it rather
    /// than handed a rusty one.</para>
    /// </summary>
    public void IssueStarterKit()
    {
        if (!LoadoutStatus().NeedsStarterKit)
        {
            Emit("[Loadout] You already own something to fight with — equip it.");
            return;
        }

        EquipStarterLoadout();
        Emit("[Loadout] The Hideout turns out a rusty sword and some tattered armour. It is not much.");
        CharacterChanged?.Invoke();
        LoadoutChanged?.Invoke();
    }

    /// <summary>
    /// Enters the prepared Realm and moves the packed supplies into the run.
    ///
    /// <para><b>The pack is transferred, not copied.</b> The moment the party is inside, those
    /// salves sit in the unsecured run inventory — lost on death, carried home on extraction —
    /// which is the same rule every other thing they hold obeys. Leaving them in the Stash and
    /// letting combat reach back for them would have made supplies the one thing extraction
    /// cannot cost you.</para>
    ///
    /// <para>This also closes a real hole: before the preparation screen existed, a Healing Salve
    /// in the Stash was unreachable inside a Realm, because combat consumes from the active bag
    /// and the run started empty.</para>
    /// </summary>
    public void EnterPreparedRun()
    {
        if (Character is null || InRealm)
            return;

        if (SelectedRealm is not { } realm)
        {
            Emit("[Realm] Choose where you are going first.");
            return;
        }

        var manifest = LoadoutCheck.PackableFrom(_loadout, _stash);
        EnterRealm(realm.Id, StartingDepth);
        if (_run is null)
            return;

        foreach (var supply in manifest.Taking)
        {
            if (_stash.TryRemove(supply.ItemId, supply.Quantity))
                _run.RunInventory.Add(supply);
        }

        if (manifest.Taking.Count > 0)
            Emit($"[Loadout] Packed {DescribeStacks(manifest.Taking)} — unsecured from here on.");

        foreach (var missing in manifest.Short)
            Emit($"[Loadout] Short {missing.Quantity}× {ItemName(missing.ItemId)} — the Stash did not have it.");

        InventoryChanged?.Invoke();
    }

    // --- Realm --------------------------------------------------------------

    public IReadOnlyList<RealmDefinition> Realms => _realms.GetAll().OrderBy(r => r.Name).ToList();
    public bool InRealm => _run is { Active: true };
    public RealmRun? Run => _run;
    public bool RealmBusy => _encounter.IsActive;
    public bool RealmCanDescend => _run?.CanDescend ?? false;
    public bool RealmCanExtract => _run?.CanExtract ?? false;
    public int Knowledge(string realmId) => _realmKnowledge.TryGetValue(realmId, out var knowledge) ? knowledge : 0;

    public string ActorName(string actorId) => _actors.TryGetById(actorId, out var a) ? a.Name : actorId;

    /// <summary>Label for the current location's primary action, or null if it has none.</summary>
    public string? RealmActionLabel()
    {
        if (_run is null)
            return null;
        var location = _run.CurrentLocation;
        return location.Type switch
        {
            RealmLocationType.Combat => _run.IsCleared(location.Id) ? null : $"Fight {ActorName(location.ActorId ?? string.Empty)}",
            RealmLocationType.Gather => "Gather here",
            RealmLocationType.Event => _run.IsCleared(location.Id) ? null : "Investigate",
            RealmLocationType.Camp => _run.IsCleared(location.Id) ? null : "Rest here",
            RealmLocationType.Shrine => _run.IsCleared(location.Id) ? null : "Attend the shrine",
            RealmLocationType.Merchant => _run.IsCleared(location.Id) ? null : $"Trade ({location.Cost} coin)",
            RealmLocationType.Hazard => null,
            _ => null,
        };
    }

    public void EnterRealm(string realmId, int startingDepth = 1)
    {
        if (Character is null || InRealm)
            return;

        var realm = _realms.GetById(realmId);
        _passiveRunner.Stop();
        Character.RestoreAll(); // rested and prepared before the expedition
        _run = new RealmRun(realm, tier: 1, knowledge: Knowledge(realmId), startingDepth);
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

        CrossHazardIfAny();
        Emit($"[Realm] Travelled to {_run.CurrentLocation.Name}.");
        RealmChanged?.Invoke();
    }

    /// <summary>
    /// Crossing a Hazard costs health, once. The cost is paid on ARRIVAL rather than as an
    /// action, because a hazard is ground you are standing on — there is no "decline to be in
    /// the bog". What Realm Knowledge buys is seeing it on the map beforehand, which turns it
    /// from an ambush into a route choice.
    /// </summary>
    private void CrossHazardIfAny()
    {
        if (_run is null || Character is null)
            return;

        var here = _run.CurrentLocation;
        if (here.Type != RealmLocationType.Hazard || _run.IsCleared(here.Id))
            return;

        Emit($"[Hazard] {here.EventText}");
        var dealt = Character.TakeDamage(here.HazardDamage);
        Emit($"[Hazard] {here.Name} costs you {dealt} health.");
        _run.MarkCleared(here.Id);
        AddKnowledge(_run.Realm.Id, RealmTuning.KnowledgePerHazardCrossed);

        if (Character.Health.IsDepleted)
        {
            Emit("[Realm] The ground finishes what it started. Everything unsecured is lost.");
            EndRealmRun(died: true);
        }
    }

    /// <summary>Performs the current location's primary action (fight / gather / investigate).</summary>
    public void RealmAction()
    {
        if (_run is null || _encounter.IsActive)
            return;

        var location = _run.CurrentLocation;
        switch (location.Type)
        {
            case RealmLocationType.Combat:
                if (_run.IsCleared(location.Id))
                {
                    Emit("[Realm] This area is already cleared.");
                    return;
                }
                if (location.ActorId is null)
                    return;
                _realmCombatLocationId = location.Id;
                StartCombatInternal(location.ActorId);
                RealmChanged?.Invoke();
                break;

            case RealmLocationType.Gather:
                if (location.ProfessionActionId is null)
                    return;

                // Working a node in a Realm is active play by definition — the player is
                // standing there doing it, with unsecured pockets.
                var gathered = _professions.Execute(
                    location.ProfessionActionId, RealmTuning.RealmGatherPerformance, isActive: true);

                if (!gathered.Success)
                    Emit($"[Realm] You cannot work this here ({gathered.Failure}).");
                else if (location.LootTableId is { Length: > 0 } nodeTable && !gathered.AttemptMissed)
                    GrantLoot(new[] { nodeTable }, location.Name, LootCircumstances());

                RealmChanged?.Invoke();
                break;

            case RealmLocationType.Event:
                if (_run.IsCleared(location.Id))
                {
                    Emit("[Realm] Nothing else of interest here.");
                    return;
                }
                Emit($"[Event] {location.EventText}");
                if (location.LootTableId is { Length: > 0 } eventTable)
                    GrantLoot(new[] { eventTable }, location.Name, LootCircumstances());
                _run.MarkCleared(location.Id);
                AddKnowledge(_run.Realm.Id, RealmTuning.KnowledgePerEvent);
                RealmChanged?.Invoke();
                break;

            // A camp is the only way to spend safety instead of banking it: resting here costs
            // nothing but the fact that you cannot rest here twice.
            case RealmLocationType.Camp:
                if (_run.IsCleared(location.Id))
                {
                    Emit("[Realm] You have already taken what rest this place offers.");
                    return;
                }
                if (Character is null)
                    return;

                Emit($"[Camp] {location.EventText}");
                foreach (var pool in new[] { Character.Health, Character.Stamina, Character.Mana })
                {
                    var restored = pool.Restore((int)Math.Round(pool.Max * location.RestoreFraction));
                    if (restored > 0)
                        Emit($"[Camp] Recovered {restored} {pool.Type}.");
                }
                _run.MarkCleared(location.Id);
                RealmChanged?.Invoke();
                break;

            // A shrine pays in Realm Knowledge. It is the fastest way to learn a place, which is
            // why it sits deep enough that reaching it is already a decision.
            case RealmLocationType.Shrine:
                if (_run.IsCleared(location.Id))
                {
                    Emit("[Realm] The shrine has nothing more to tell you.");
                    return;
                }
                Emit($"[Shrine] {location.EventText}");
                if (location.LootTableId is { Length: > 0 } shrineTable)
                    GrantLoot(new[] { shrineTable }, location.Name, LootCircumstances());
                _run.MarkCleared(location.Id);
                AddKnowledge(_run.Realm.Id, RealmTuning.KnowledgePerShrine);
                RealmChanged?.Invoke();
                break;

            // The first gold sink. It spends UNSECURED coin — the coin you would lose by dying
            // on the way out — so buying here is the extraction decision in miniature.
            case RealmLocationType.Merchant:
                if (_run.IsCleared(location.Id))
                {
                    Emit("[Realm] She has nothing else you can afford to want.");
                    return;
                }
                if (_run.RunInventory.Gold < location.Cost)
                {
                    Emit($"[Trade] {location.Cost} coin, and you are carrying {_run.RunInventory.Gold}. She waits.");
                    return;
                }

                _run.RunInventory.TrySpendGold(location.Cost);
                Emit($"[Trade] {location.EventText}");
                if (location.LootTableId is { Length: > 0 } stock)
                    GrantLoot(new[] { stock }, location.Name, LootCircumstances());
                _run.MarkCleared(location.Id);
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

    /// <summary>
    /// What Realm Knowledge has actually bought, rendered for the player.
    ///
    /// <para>This is the whole payoff of the track and the reason it grants no damage: every
    /// line here is <b>information the realm was always hiding</b>, not a number going up. A
    /// party at 42 knowledge fights exactly as hard as a party at 0 and makes far better
    /// decisions, which is the difference the GDD asks for (§11.4).</para>
    /// </summary>
    private string KnowledgeIntel(RealmRun run)
    {
        var intel = new StringBuilder();

        if (run.Knows(RealmInsight.EnemyWeaknesses)
            && run.CurrentLocation.ActorId is { Length: > 0 } actorId
            && _actors.TryGetById(actorId, out var actor))
        {
            var resolved = ActorResolver.Resolve(actor, _enemyFamilies, _enemyRoles, _aiProfiles);
            var weakTo = resolved.Vulnerable.Where(v => v.Value > 1.0).Select(v => v.Key);
            var shrugsOff = resolved.Resistances.Where(r => r.Value > 0).Select(r => $"{r.Key} {r.Value:P0}");

            intel.AppendLine($"Known — {resolved.Name}: weak to {Join(weakTo, "nothing in particular")}; "
                + $"resists {Join(shrugsOff, "nothing")}.");
        }

        if (run.Knows(RealmInsight.Hazards))
        {
            var hazards = run.KnownAtCurrentDepth()
                .Where(l => l.Type == RealmLocationType.Hazard && !run.IsCleared(l.Id))
                .Select(l => $"{l.Name} ({l.HazardDamage})");
            if (hazards.Any())
                intel.AppendLine($"Known — dangerous ground at this depth: {string.Join(", ", hazards)}.");
        }

        if (run.Knows(RealmInsight.RichNodes))
        {
            var rich = run.KnownAtCurrentDepth()
                .Where(l => l.Type == RealmLocationType.Gather && !string.IsNullOrEmpty(l.LootTableId))
                .Select(l => l.Name);
            if (rich.Any())
                intel.AppendLine($"Known — worth working here: {string.Join(", ", rich)}.");
        }

        if (run.Knows(RealmInsight.HiddenRoutes))
        {
            var hidden = run.KnownAtCurrentDepth().Where(l => l.Hidden).Select(l => l.Name);
            if (hidden.Any())
                intel.AppendLine($"Known — ways nobody marked: {string.Join(", ", hidden)}.");
        }

        var exits = run.KnownExtractions();
        if (exits.Count > 0)
            intel.AppendLine($"Known — ways out at this depth: {string.Join(", ", exits.Select(l => l.Name))}.");

        if (RealmKnowledgeLevels.Next(run.Knowledge) is { } next)
            intel.AppendLine($"Next insight at {next.Required} knowledge: {PreparationText.DescribeInsight(next.Insight)}.");

        return intel.ToString();

        static string Join(IEnumerable<string> values, string whenEmpty)
        {
            var list = values.ToList();
            return list.Count == 0 ? whenEmpty : string.Join(", ", list);
        }
    }

    public string RealmReport()
    {
        if (_run is null)
        {
            var known = _realms.GetAll().Select(r => $"{r.Name}: Knowledge {Knowledge(r.Id)}");
            return "In the Hideout.\n" + string.Join("\n", known);
        }

        var run = _run;
        var location = run.CurrentLocation;
        var cleared = location.Type == RealmLocationType.Combat && run.IsCleared(location.Id) ? " (cleared)" : string.Empty;
        var healthText = Character is null ? "-" : $"{Character.Health.Current}/{Character.Health.Max}";

        var unsecured = run.RunInventory.Snapshot().Sum(s => s.Quantity);
        var report = new StringBuilder();
        report.AppendLine($"{run.Realm.Name} — Tier {run.Tier}   Depth {run.CurrentDepth}");
        report.AppendLine($"Location: {location.Name} [{location.Type}]{cleared}");
        report.AppendLine($"Party HP: {healthText}    Knowledge: {Knowledge(run.Realm.Id)}");
        report.AppendLine($"Unsecured loot at risk: {unsecured} item(s){DescribeCoin(run.RunInventory.Gold)}");
        report.Append(KnowledgeIntel(run));
        if (_encounter.IsActive)
            report.AppendLine("In combat — see the Combat panel.");
        return report.ToString().TrimEnd();
    }

    private void EndRealmRun(bool died)
    {
        if (_run is null)
            return;
        var realmId = _run.Realm.Id;

        if (died)
        {
            var lost = RealmExtraction.Forfeit(_run);
            Emit($"[Realm] You have died. {lost.TotalQuantity} unsecured item(s){DescribeCoin(lost.Gold)} lost. " +
                 "Your Stash and equipped gear are safe.");
        }
        else
        {
            var secured = RealmExtraction.Secure(_run, _stash);
            AddKnowledge(realmId, RealmTuning.KnowledgePerExtract);
            Emit($"[Extraction] Secured {secured.TotalQuantity} item(s){DescribeCoin(secured.Gold)} to your Stash. " +
                 "Returned to the Hideout.");

            // Paid on the way out only. Dying keeps the levels already earned — persistent
            // progression always survives (GDD §13.1) — but it does not pay for the trip.
            AwardCharacterXp(CharacterLeveling.XpForExtracting);
        }

        _run = null;
        _realmCombatLocationId = null;
        RealmChanged?.Invoke();
        InventoryChanged?.Invoke();
    }

    // --- Character progression (Phase 8) ------------------------------------

    public int CharacterLevel => _characterProgress.Level;
    public long CharacterXp => _characterProgress.Xp;
    public double CharacterLevelProgress => _characterProgress.ProgressToNextLevel;

    /// <summary>
    /// Banks character XP and, if it crossed a level, recomposes the character so the Base's
    /// growth weights land.
    ///
    /// <para><b>Realm work is the only caller.</b> Professions, crafting and discoveries award
    /// none by design — a fishing rod that raises combat attributes is the universal power level
    /// GDD §4 exists to prevent, and it would make every other track a rounding error.</para>
    /// </summary>
    private void AwardCharacterXp(long amount)
    {
        if (amount <= 0)
            return;

        var levelUp = _characterProgress.AddXp(amount);
        if (levelUp is null)
        {
            CharacterChanged?.Invoke();
            return;
        }

        Emit($"[Character] Level {levelUp.Value.NewLevel}. The work is starting to show.");
        RebuildCharacter(); // raises CharacterChanged, and carries the current pools across
    }

    private void AddKnowledge(string realmId, int amount)
    {
        var before = Knowledge(realmId);
        _realmKnowledge[realmId] = before + amount;

        // A run holds its own copy so hidden routes can open MID-RUN — the shortcut you earn on
        // the way down is one you can take on the way back.
        if (_run is not null && _run.Realm.Id == realmId)
            _run.Knowledge = _realmKnowledge[realmId];

        foreach (var insight in RealmKnowledgeLevels.Unlocked(_realmKnowledge[realmId]))
            if (!RealmKnowledgeLevels.Reveals(before, insight))
                Emit($"[Knowledge] You have learned this place well enough to {PreparationText.DescribeInsight(insight)}.");
    }

    // --- Save ---------------------------------------------------------------

    public void SaveGame()
    {
        var data = SaveMapper.Capture(_build, _stash, _professions, _discoveries, _realmKnowledge, _tick.CurrentTick, _playerEquipment, _instanceIds, _emergentRegistry, learnedMoves: _learnedMoves,
            emergentEquipment: _equipment.GetAll().Where(e => e.Id.StartsWith("equip.emergent.", StringComparison.Ordinal)),
            farmingPlots: _farmingPlots,
            trainingCourse: _trainingCourse,
            passiveActionId: _passiveRunner.SelectedActionId, // the standing selection, running or waiting
            savedAtUnixSeconds: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            loadout: _loadout,
            characterProgress: _characterProgress);
        _saveStore.Save(data);
        Emit($"[Save] Saved — {data.Professions.Count} profession(s), {data.Stash.Count} stash stack(s), " +
             $"{data.StashInstances.Count} instance(s), {data.Equipment.Count} equipped, {data.Discoveries.Count} discovery(ies).");
        if (data.PassiveActionId is not null)
            Emit($"[Save] {ActionName(data.PassiveActionId)} will keep running while you are away.");
    }

    /// <summary>
    /// Saves on the way out, so closing the window is a legitimate way to end a session.
    ///
    /// <para><b>Why this is needed at all.</b> Offline progress is measured from
    /// <see cref="SaveData.SavedAtUnixSeconds"/> — the clock only starts at a save. Without an
    /// autosave, "leave it running overnight" quietly means "remember to press Save first", and
    /// the one time the player forgets, the night pays nothing and looks like a bug.</para>
    ///
    /// <para><b>Why it is guarded.</b> A session that has never had a save file must not create
    /// one on the way out: a fresh game started for a look around would overwrite a real save.
    /// Writing over somebody's progress is a worse failure than losing an unsaved hour.</para>
    /// </summary>
    public override void _Notification(int what)
    {
        if (what is not ((int)NotificationWMCloseRequest or (int)NotificationExitTree))
            return;
        if (_autosaved || InRealm) // mid-run state is unsecured by design; saving it would bank it
            return;
        if (!_saveStore.Exists())
            return;

        _autosaved = true;
        SaveGame();
    }

    private bool _autosaved;

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

        SaveMapper.Apply(save, _stash, _professions, _discoveries, _realmKnowledge, _playerEquipment, _instanceIds, _emergentRegistry, learnedMoves: _learnedMoves, equipmentStore: _equipment,
            farmingPlots: _farmingPlots, trainingCourse: _trainingCourse, loadout: _loadout, characterProgress: _characterProgress);
        EnsureRealmSelected(); // a v9 save carries no loadout, and an old one may name a Realm we no longer ship
        if (save.Build is not null)
            _build = save.Build;

        // Always, not only when the build changed: the loaded character XP decides how much
        // attribute growth to apply, so a save with levels needs recomposing even if the four
        // build ids are the ones already in memory.
        RebuildCharacter(); // raises CharacterChanged
        Character?.RestoreAll(); // loading is a rest; pools carried across from the pre-load character mean nothing

        EquipStarterLoadout(); // fill any empty slots (fresh/old saves) so the player is never unarmed

        Emit($"[Load] Loaded save (schema v{save.SchemaVersion}, saved at tick {save.SavedAtTick}).");
        ApplyTimeAway(save);

        InventoryChanged?.Invoke();
        DiscoveryChanged?.Invoke();
        RealmChanged?.Invoke();
        LoadoutChanged?.Invoke();
    }

    /// <summary>
    /// Pays out the absence: the training selection the player left running, and any crop that
    /// finished growing while the game was closed. Wall-clock, not ticks — the simulation clock
    /// stops when the game does, so only <see cref="SaveData.SavedAtUnixSeconds"/> can measure
    /// how long they were gone.
    ///
    /// <para>The arithmetic all lives in <see cref="AwayProgress"/>, which runs the same
    /// <c>ProfessionSystem.Execute</c> live passive play does. This does the two things Core
    /// cannot: it converts wall-clock into an elapsed span, and it moves the crop clock onto
    /// this session's ticks first.</para>
    /// </summary>
    private void ApplyTimeAway(SaveData save)
    {
        if (save.SavedAtUnixSeconds <= 0)
            return; // a v6 save, or one written before the clock was stamped

        var secondsAway = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - save.SavedAtUnixSeconds;
        if (secondsAway <= 0)
            return;

        // Nothing may resolve twice: if a passive action was already running in this session
        // when the player hit Load, its scheduled completions would double-count against the
        // offline payout below.
        _passiveRunner.Stop();

        RebasePlantedCrops(save, ProfessionTuning.OfflineTicks(secondsAway));

        LastAwayReport = AwayProgress.Resolve(
            _professions, save.PassiveActionId, secondsAway, _farmingPlots, _tick.CurrentTick);

        Emit("[Away] " + AwayReadout.OneLine(LastAwayReport, ActionName, ItemName, OfflineCapHours));

        // The selection is picked back up whatever state it is in: auto-repeat means an action
        // that ran out of materials waits for more rather than being forgotten, which is the
        // whole point of a *standing* selection.
        if (save.PassiveActionId is { } actionId)
            _passiveRunner.Start(actionId);

        AwayReported?.Invoke(LastAwayReport);
    }

    /// <summary>What the last absence earned, for the summary panel. Null until one is paid.</summary>
    public AwayReport? LastAwayReport { get; private set; }

    /// <summary>Raised when an absence has been paid out and is ready to be shown.</summary>
    public event Action<AwayReport>? AwayReported;

    /// <summary>The offline cap in hours, as the summary states it.</summary>
    public static long OfflineCapHours => ProfessionTuning.MaxOfflineTicks / (TicksPerSecond * 3600);

    /// <summary>The away summary as headed lines — what the panel renders.</summary>
    public IReadOnlyList<AwayLine> AwaySummaryLines() =>
        LastAwayReport is null
            ? Array.Empty<AwayLine>()
            : AwayReadout.Lines(LastAwayReport, ActionName, ProfessionName, ItemName, OfflineCapHours);

    public string AwaySummaryHeadline() =>
        LastAwayReport is null ? string.Empty : AwayReadout.Headline(LastAwayReport);

    /// <summary>
    /// Moves saved crops onto this session's clock, minus the time the player was away.
    ///
    /// <para>A plot stores an absolute <c>ReadyAtTick</c>, but the simulation clock does not
    /// survive a restart — a fresh session starts at tick 0, so a crop saved as "ready at
    /// 2900" would otherwise wait out its whole growth again. What actually carries over is the
    /// <em>remaining</em> time, so that is what is recomputed: what was left at save, less the
    /// absence, starting from now.</para>
    /// </summary>
    private void RebasePlantedCrops(SaveData save, long ticksAway)
    {
        _farmingPlots.Restore(save.FarmingPlots.Select(planting =>
        {
            var remainingAtSave = Math.Max(0, planting.ReadyAtTick - save.SavedAtTick);
            var remainingNow = Math.Max(0, remainingAtSave - ticksAway);
            return (planting.Index, planting.ActionId, _tick.CurrentTick + remainingNow);
        }));
    }

    // TimeAwayPhrase/OfflineStopPhrase lived here and now live in Presentation's AwayReadout —
    // the console line and the summary panel say the same thing about the same absence, which
    // they could not while one of them owned its own wording (D30, rule 7).

    public void ReportStatus()
    {
        Emit($"[Content] {_materials.Count} materials, {_professionStore.Count} professions, {_actionStore.Count} actions.");
        if (Character is not null)
            Emit($"[Character] Active build: {Character.DisplayName}");
    }

    public string CharacterReport()
    {
        if (Character is null)
            return "No character.";

        var character = Character;
        var effectiveAttributes = character.EffectiveAttributes;
        var baseAttributes = character.BaseAttributes;

        var report = new StringBuilder();
        report.AppendLine(character.DisplayName);
        report.AppendLine(
            $"Level {_characterProgress.Level}    XP {_characterProgress.Xp}" +
            (_characterProgress.XpForNextLevel == 0
                ? "    (mastered)"
                : $"  ({_characterProgress.XpIntoCurrentLevel}/{_characterProgress.XpForNextLevel} to the next)") +
            "    — earned in Realms, never in the Hideout");
        report.AppendLine($"Primary resource: {character.Blueprint.PrimaryResource}");
        report.AppendLine(
            $"HP {character.Health.Current}/{character.Health.Max}" +
            $"    Mana {character.Mana.Current}/{character.Mana.Max}" +
            $"    Stamina {character.Stamina.Current}/{character.Stamina.Max}");
        report.AppendLine(EquipmentReport());
        report.AppendLine("Attributes  (effective / base):");
        foreach (var attribute in AttributeTypes.All)
            report.AppendLine($"  {attribute,-13} {effectiveAttributes[attribute],3} / {baseAttributes[attribute]}");
        report.AppendLine($"Tags: {string.Join(", ", character.Blueprint.Tags)}");
        if (character.Blueprint.Rules.Count > 0)
        {
            report.AppendLine("Rules:");
            foreach (var rule in character.Blueprint.Rules)
                report.AppendLine($"  • {rule.Description}");
        }

        return report.ToString().TrimEnd();
    }

    // --- Internals ----------------------------------------------------------

    /// <summary>
    /// Recomposes the character from its build and its <b>current level</b>.
    ///
    /// <para>The level is where the Base's growth weights finally land: <c>GrowthAt</c> spreads
    /// the same 4.0-point-per-level budget in the shape this Base declares, and that lands on top
    /// of the starting baseline. Bases are untouched — this is the call they were written for
    /// and never got.</para>
    ///
    /// <para><b>Pools carry across.</b> A fresh <see cref="Character"/> starts full, so composing
    /// a new one mid-run would silently heal the party every time they levelled — turning a
    /// level-up into a free potion at the worst possible moment for the extraction decision.
    /// Levelling raises the ceiling; it never refills what is under it.</para>
    /// </summary>
    private void RebuildCharacter()
    {
        var carried = Character;
        var growth = ResolveBuild(_build).GrowthAt(_characterProgress.Level);

        var grown = Baseline;
        foreach (var (attribute, points) in growth)
            grown = grown.Add(attribute, points);

        Character = new Character(_composer.Compose(_build, grown));
        if (carried is not null)
            CarryPoolsAcross(carried, Character);

        AttachBuildRules();
        Emit($"[Character] Built {Character.DisplayName} (level {_characterProgress.Level}).");
        CharacterChanged?.Invoke();
    }

    /// <summary>Moves current pool values onto a freshly composed character, clamped to its new
    /// maxima — a rebuild that shrinks a pool must not leave the party over-full.</summary>
    private static void CarryPoolsAcross(Character previous, Character rebuilt)
    {
        foreach (var type in new[] { ResourceType.Health, ResourceType.Mana, ResourceType.Stamina })
        {
            var pool = rebuilt.Resource(type);
            pool.Reduce(pool.Max - Math.Min(previous.Resource(type).Current, pool.Max));
        }
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

        // Phase 3: identity sentences on worn items attach beside the build.s own hooks and
        // swap with the gear — an unequipped item.s rules must stop firing exactly like a
        // retired Prefix.s would: rules beside the
        // build's, gauges beside the build's (a store sentence's meter swaps with the gear
        // exactly like a Prefix's meter swaps with the Prefix).
        var identityGrants = EquippedIdentityGrants().ToList();
        foreach (var (instance, compiled) in identityGrants)
        {
            foreach (var rule in compiled.Rules)
                _ruleEngine.Attach(rule, instance.DisplayName);
        }

        // The gauge set is part of the build, so it swaps with it — otherwise a retired Prefix's
        // meter would keep filling from feeds that no longer exist.
        _gauges.Reconfigure(
            resolved.Gauges.Concat(identityGrants.SelectMany(grant => grant.Compiled.Gauges)).ToList(),
            _tick.CurrentTick);
    }

    /// <summary>Every worn identity-minted item with its sentences recompiled to grants —
    /// Compiled fresh per rebuild; the
    /// compile is deterministic from the persisted sentences (D50), so nothing can drift.</summary>
    private IEnumerable<(ItemInstance Instance, CompiledSentence Compiled)> EquippedIdentityGrants()
    {
        foreach (var instance in _playerEquipment.Slots.Values)
        {
            if (instance.IdentitySentences.Count == 0)
                continue;
            yield return (instance, _itemEffectResolver.CompileAll(instance.IdentitySentences));
        }
    }

    /// <summary>Standing stat grants from worn identity items' sentences, with per-item
    /// provenance — the sustain floors and their kin.</summary>
    private IEnumerable<ModifierContribution> EquippedIdentityContributions()
    {
        foreach (var (instance, compiled) in EquippedIdentityGrants())
        {
            foreach (var (modifierKey, value, scopeText) in compiled.StatGrants)
            {
                ModifierScope? scope = null;
                if (scopeText is { Length: > 0 })
                {
                    var dimensionEnd = scopeText.IndexOf(':');
                    if (dimensionEnd > 0)
                        scope = new ModifierScope(scopeText[..dimensionEnd], scopeText[(dimensionEnd + 1)..]);
                }

                yield return new ModifierContribution(modifierKey, value, instance.DisplayName, scope);
            }
        }
    }

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
        Emit($"[Passive] {ActionName(outcome.ActionId)} is waiting — {outcome.Failure}. It resumes on its own.");

    private void OnPassiveResumed(string actionId) =>
        Emit($"[Passive] {ActionName(actionId)} picked back up.");

    private void OnDiscovered(string discoveryId)
    {
        var interaction = _interactions.GetAll().FirstOrDefault(i => i.DiscoveryId == discoveryId);
        var name = interaction?.Name ?? discoveryId;
        Emit($"[Discovery] You discovered {name}!");
        DiscoveryChanged?.Invoke();
    }

    private string DescribeProduced(ActionOutcome outcome)
    {
        var produced = outcome.Produced.Count == 0
            ? "nothing"
            : string.Join(", ", outcome.Produced.Select(s => $"+{s.Quantity} {ItemName(s.ItemId)}"));

        // Mastery says so out loud. A benefit the player never sees fire is a benefit they do
        // not believe in — which is how mastery spent this whole project feeling like a number
        // that goes up and does nothing.
        return produced + DescribeMasteryLuck(outcome);
    }

    private static string DescribeMasteryLuck(ActionOutcome outcome) => (outcome.InputsPreserved, outcome.OutputsDoubled) switch
    {
        (true, > 0) => "  [mastery: materials saved, and doubled]",
        (true, _) => "  [mastery: materials saved]",
        (_, > 0) => "  [mastery: doubled]",
        _ => string.Empty,
    };

    private string ProfessionOf(string actionId) =>
        _actionStore.TryGetById(actionId, out var a) ? ProfessionName(a.ProfessionId) : "?";

    /// <summary>An action's display name. Public because the UI names the running passive
    /// action and the crop in each plot, and neither should render a raw content id.</summary>
    public string ActionName(string actionId) =>
        _actionStore.TryGetById(actionId, out var a) ? a.Name : actionId;

    /// <summary>An item's display name, whatever store it lives in. Public because the UI lists
    /// interaction inputs and outputs, and neither should render a raw content id.</summary>
    public string ItemName(string itemId)
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
