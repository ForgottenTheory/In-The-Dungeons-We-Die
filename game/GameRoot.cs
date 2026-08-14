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
using Dungeons.Game.Infrastructure;
using Dungeons.Items;
using Dungeons.Persistence;
using Dungeons.Professions;
using Dungeons.Randomness;
using Dungeons.Realms;
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

    private static readonly string[] SuffixCycle =
    {
        "suffix.unreasonable_confidence",
        "suffix.inappropriate_optimism",
        "suffix.exploding_kneecaps",
        "suffix.the_bigger_hammer",
        "suffix.the_last_laugh",
    };

    private readonly TickEngine _tick = new();
    private readonly SaveStore _saveStore = new();
    private readonly Inventory _stash = new();

    private DataStore<MaterialDefinition> _materials = new();
    private DataStore<ProfessionDefinition> _professionDefs = new();
    private DataStore<ProfessionActionDefinition> _actionDefs = new();

    private DataStore<CraftingInteractionDefinition> _interactions = new();
    private DataStore<AbilityDefinition> _abilities = new();
    private DataStore<ActorDefinition> _actors = new();
    private DataStore<RealmDefinition> _realms = new();

    private CharacterComposer _composer = null!;
    private ProfessionSystem _professions = null!;
    private PassiveProfessionRunner _passiveRunner = null!;
    private DiscoverySystem _discoveries = null!;
    private CraftingExperimentSystem _crafting = null!;
    private CombatEncounter _encounter = null!;
    private bool _everFought;

    private RealmRun? _run;
    private string? _realmCombatLocationId;
    private readonly Dictionary<string, int> _realmKnowledge = new();

    /// <summary>Where loot currently flows: the run inventory in a Realm, else the Stash.</summary>
    private Inventory CurrentBag => _run is { Active: true } ? _run.RunInventory : _stash;

    private CharacterBuild _build = new("species.fey_touched", "class.hexslinger", "prefix.frenzied", SuffixCycle[0]);
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
        _materials = ContentLoader.LoadMaterials("res://data/materials");

        var species = ContentLoader.LoadDefinitions<SpeciesDefinition>("res://data/species");
        var classes = ContentLoader.LoadDefinitions<BaseClassDefinition>("res://data/classes");
        var prefixes = ContentLoader.LoadDefinitions<PrefixDefinition>("res://data/prefixes");
        var suffixes = ContentLoader.LoadDefinitions<SuffixDefinition>("res://data/suffixes");
        _professionDefs = ContentLoader.LoadDefinitions<ProfessionDefinition>("res://data/professions");
        _actionDefs = ContentLoader.LoadDefinitions<ProfessionActionDefinition>("res://data/profession_actions");
        _interactions = ContentLoader.LoadDefinitions<CraftingInteractionDefinition>("res://data/crafting_interactions");
        _abilities = ContentLoader.LoadDefinitions<AbilityDefinition>("res://data/abilities");
        _actors = ContentLoader.LoadDefinitions<ActorDefinition>("res://data/actors");
        _realms = ContentLoader.LoadDefinitions<RealmDefinition>("res://data/realms");

        var rules = new RuleRegistry(new ICharacterRule[]
        {
            new UnreasonableConfidenceRule(),
            new InappropriateOptimismRule(),
        });
        _composer = new CharacterComposer(species, classes, prefixes, suffixes, rules);
        RebuildCharacter();

        // Gathering deposits into the current bag: the Stash in the Hideout, the run
        // inventory while in a Realm (unsecured until extraction).
        _professions = new ProfessionSystem(_actionDefs, () => CurrentBag, new SeededRandom(20260814));
        _passiveRunner = new PassiveProfessionRunner(_tick, _professions);
        _professions.ActionCompleted += OnActionCompleted;
        _professions.LeveledUp += OnLeveledUp;
        _passiveRunner.Stalled += OnPassiveStalled;
        _stash.Changed += () => InventoryChanged?.Invoke();

        // Crafting happens in the Hideout, against the Stash (field crafting deferred).
        _discoveries = new DiscoverySystem();
        _crafting = new CraftingExperimentSystem(
            _interactions, _materials, _stash, _discoveries,
            professionLevel: id => _professions.GetProgress(id).Level);
        _discoveries.Discovered += OnDiscovered;

        var combatRng = new SeededRandom(0x0C0FFEE);
        _encounter = new CombatEncounter(_tick, new CombatCalculator(combatRng), _abilities, combatRng, "ability.strike");
        _encounter.Logged += Emit;
        _encounter.StateChanged += () => CombatChanged?.Invoke();
        _encounter.Ended += OnCombatEnded;

        // Seed some ore so Smithing is demonstrable (Mining is deferred).
        _stash.Add("material.iron_ore", 10);

        GD.Print($"[GameRoot] Ready. {_materials.Count} materials, {_professionDefs.Count} professions, {_actionDefs.Count} actions, {_interactions.Count} interactions.");
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

    // --- Character ----------------------------------------------------------

    public void CycleSuffix()
    {
        _suffixIndex = (_suffixIndex + 1) % SuffixCycle.Length;
        _build = _build with { SuffixId = SuffixCycle[_suffixIndex] };
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
        var stacks = bag.Snapshot().OrderBy(s => s.ItemId).ToList();
        if (stacks.Count == 0)
            return "  (empty)";
        return string.Join("\n", stacks.Select(s => $"  {ItemName(s.ItemId),-14} x{s.Quantity}"));
    }

    // --- Crafting -----------------------------------------------------------

    /// <summary>Attempts the flagship cross-profession experiment: Iron Ingot + Oak Bark.</summary>
    public void ExperimentBarkbound() => Experiment("material.iron_ingot", "material.oak_bark");

    public void Experiment(params string[] itemIds)
    {
        var outcome = _crafting.Experiment(itemIds);
        if (outcome.Success)
        {
            var props = outcome.ResultProperties.Count > 0
                ? " (" + string.Join(", ", outcome.ResultProperties.Select(p => $"{p.Property} {p.Value}")) + ")"
                : string.Empty;
            Emit($"[Craft] Made {outcome.ResultQuantity} {ItemName(outcome.ResultItemId!)}{props}.");
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

    /// <summary>Debug helper: grants the materials and Herblore knowledge needed to try Barkbound Iron.</summary>
    public void GrantCraftTestMaterials()
    {
        _stash.Add("material.iron_ingot", 1);
        _stash.Add("material.oak_bark", 1);
        _professions.GetProgress("profession.herblore").AddXp(ProfessionLeveling.XpForLevel(2));
        Emit("[Debug] Granted 1 Iron Ingot, 1 Oak Bark, and Herblore knowledge.");
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
        _encounter.Start(Combatant.FromCharacter(Character), new[] { Combatant.FromActor(actor) });
        SetRunning(true); // telegraphs advance in real time
        CombatChanged?.Invoke();
    }

    public void CombatAttack() => _encounter.Attack();
    public void CombatBlock() => _encounter.Block();
    public void CombatDodge() => _encounter.Dodge();
    public void CombatWait() => _encounter.Wait();

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
                sb.AppendLine($"⚠ {intent.Attacker.Name}: {intent.Ability.Name} — impact in {seconds:0.0}s ({intent.Ability.DamageType})");
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
            AddKnowledge(_run.Realm.Id, 2);
            Emit($"[Realm] Cleared {_run.Realm.GetLocation(locationId).Name}.");
            RealmChanged?.Invoke();
        }
        else
        {
            EndRealmRun(died: true);
        }
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
        AddKnowledge(realmId, 1);
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
            AddKnowledge(_run.Realm.Id, 1);
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
                AddKnowledge(_run.Realm.Id, 1);
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

        AddKnowledge(_run.Realm.Id, 2);
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
            Emit($"[Realm] You have died. {lost.TotalQuantity} unsecured item(s) lost. Your Stash is safe. " +
                 "(Equipped-gear loss and a starter loadout arrive with the equipment system.)");
        }
        else
        {
            var secured = RealmExtraction.Secure(_run, _stash);
            AddKnowledge(realmId, 3);
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

    public void SaveAndReload()
    {
        _saveStore.Save(new SaveData { SavedAtTick = _tick.CurrentTick });
        var loaded = _saveStore.Load();
        Emit(loaded is null
            ? "[Save] Failed to reload save."
            : $"[Save] Round-tripped save (schema v{loaded.SchemaVersion}, savedAtTick {loaded.SavedAtTick}).");
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
        Emit($"[Character] Built {Character.DisplayName}.");
        CharacterChanged?.Invoke();
    }

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

    private string ItemName(string itemId) =>
        _materials.TryGetById(itemId, out var m) ? m.Name : itemId;

    private void Emit(string message)
    {
        GD.Print(message);
        LogEmitted?.Invoke(message);
    }
}
