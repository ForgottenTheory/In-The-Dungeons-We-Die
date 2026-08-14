using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dungeons.Characters;
using Dungeons.Characters.Composition;
using Dungeons.Characters.Rules;
using Dungeons.Content;
using Dungeons.Game.Infrastructure;
using Dungeons.Items;
using Dungeons.Persistence;
using Dungeons.Professions;
using Dungeons.Randomness;
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
    private readonly Inventory _inventory = new();

    private DataStore<MaterialDefinition> _materials = new();
    private DataStore<ProfessionDefinition> _professionDefs = new();
    private DataStore<ProfessionActionDefinition> _actionDefs = new();

    private CharacterComposer _composer = null!;
    private ProfessionSystem _professions = null!;
    private PassiveProfessionRunner _passiveRunner = null!;

    private CharacterBuild _build = new("species.fey_touched", "class.hexslinger", "prefix.frenzied", SuffixCycle[0]);
    private int _suffixIndex;

    private bool _running;
    private double _tickAccumulator;

    public event Action<string>? LogEmitted;
    public event Action? CharacterChanged;
    public event Action? InventoryChanged;
    public event Action? RunningChanged;

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

        var rules = new RuleRegistry(new ICharacterRule[]
        {
            new UnreasonableConfidenceRule(),
            new InappropriateOptimismRule(),
        });
        _composer = new CharacterComposer(species, classes, prefixes, suffixes, rules);
        RebuildCharacter();

        _professions = new ProfessionSystem(_actionDefs, _inventory, new SeededRandom(20260814));
        _passiveRunner = new PassiveProfessionRunner(_tick, _professions);
        _professions.ActionCompleted += OnActionCompleted;
        _professions.LeveledUp += OnLeveledUp;
        _passiveRunner.Stalled += OnPassiveStalled;
        _inventory.Changed += () => InventoryChanged?.Invoke();

        // Seed some ore so Smithing is demonstrable (Mining is deferred).
        _inventory.Add("material.iron_ore", 10);

        GD.Print($"[GameRoot] Ready. {_materials.Count} materials, {_professionDefs.Count} professions, {_actionDefs.Count} actions.");
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
        var stacks = _inventory.Snapshot().OrderBy(s => s.ItemId).ToList();
        if (stacks.Count == 0)
            return "(empty)";
        return string.Join("\n", stacks.Select(s => $"  {ItemName(s.ItemId),-14} x{s.Quantity}"));
    }

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
