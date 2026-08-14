using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dungeons.Characters;
using Dungeons.Characters.Composition;
using Dungeons.Characters.Rules;
using Dungeons.Content;
using Dungeons.Game.Infrastructure;
using Dungeons.Persistence;
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

    private DataStore<MaterialDefinition> _materials = new();
    private CharacterComposer _composer = null!;
    private CharacterBuild _build = new("species.fey_touched", "class.hexslinger", "prefix.frenzied", SuffixCycle[0]);
    private int _suffixIndex;

    /// <summary>Human-readable events for the developer event log.</summary>
    public event Action<string>? LogEmitted;

    /// <summary>Raised whenever the active character is rebuilt or its state changes.</summary>
    public event Action? CharacterChanged;

    public long CurrentTick => _tick.CurrentTick;
    public int MaterialCount => _materials.Count;
    public Character? Character { get; private set; }

    public override void _Ready()
    {
        _tick.TickAdvanced += OnTickAdvanced;
        _materials = ContentLoader.LoadMaterials("res://data/materials");

        var species = ContentLoader.LoadDefinitions<SpeciesDefinition>("res://data/species");
        var classes = ContentLoader.LoadDefinitions<BaseClassDefinition>("res://data/classes");
        var prefixes = ContentLoader.LoadDefinitions<PrefixDefinition>("res://data/prefixes");
        var suffixes = ContentLoader.LoadDefinitions<SuffixDefinition>("res://data/suffixes");

        var rules = new RuleRegistry(new ICharacterRule[]
        {
            new UnreasonableConfidenceRule(),
            new InappropriateOptimismRule(),
        });

        _composer = new CharacterComposer(species, classes, prefixes, suffixes, rules);
        RebuildCharacter();

        GD.Print($"[GameRoot] Ready. {_materials.Count} materials, " +
                 $"{species.Count} species, {classes.Count} classes, {prefixes.Count} prefixes, {suffixes.Count} suffixes.");
    }

    public void AdvanceTick(long ticks = 1) => _tick.Advance(ticks);

    public void SaveAndReload()
    {
        _saveStore.Save(new SaveData { SavedAtTick = _tick.CurrentTick });
        var loaded = _saveStore.Load();
        Emit(loaded is null
            ? "[Save] Failed to reload save."
            : $"[Save] Round-tripped save (schema v{loaded.SchemaVersion}, savedAtTick {loaded.SavedAtTick}).");
    }

    /// <summary>Rebuilds the active character with the next suffix in the demo cycle.</summary>
    public void CycleSuffix()
    {
        _suffixIndex = (_suffixIndex + 1) % SuffixCycle.Length;
        _build = _build with { SuffixId = SuffixCycle[_suffixIndex] };
        RebuildCharacter();
    }

    /// <summary>Applies damage as a fraction of max Health, to demonstrate stateful rules.</summary>
    public void DamageCharacterPercent(double fraction)
    {
        if (Character is null)
            return;
        var amount = (int)Math.Round(Character.Health.Max * fraction);
        var dealt = Character.TakeDamage(amount);
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

    public void ReportStatus()
    {
        Emit($"[Content] {_materials.Count} material definition(s) loaded.");
        if (Character is not null)
            Emit($"[Character] Active build: {Character.DisplayName}");
    }

    /// <summary>Multi-line summary of the active character for the debug panel.</summary>
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
        sb.AppendLine($"HP {c.Health.Current}/{c.Health.Max}    " +
                      $"Mana {c.Mana.Current}/{c.Mana.Max}    " +
                      $"Stamina {c.Stamina.Current}/{c.Stamina.Max}");
        sb.AppendLine("Attributes  (effective / base):");
        foreach (var attribute in AttributeTypes.All)
            sb.AppendLine($"  {attribute,-13} {effective[attribute],3} / {baseAttributes[attribute]}");

        sb.AppendLine($"Tags: {string.Join(", ", c.Blueprint.Tags)}");
        if (c.Blueprint.AbilityIds.Count > 0)
            sb.AppendLine($"Abilities: {string.Join(", ", c.Blueprint.AbilityIds)}");

        if (c.Blueprint.Rules.Count > 0)
        {
            sb.AppendLine("Rules:");
            foreach (var rule in c.Blueprint.Rules)
                sb.AppendLine($"  • {rule.Description}");
        }

        return sb.ToString().TrimEnd();
    }

    private void RebuildCharacter()
    {
        var blueprint = _composer.Compose(_build, Baseline);
        Character = new Character(blueprint);
        Emit($"[Character] Built {Character.DisplayName}.");
        CharacterChanged?.Invoke();
    }

    private void OnTickAdvanced(long tick) => Emit($"[Tick] Advanced to {tick}.");

    private void Emit(string message)
    {
        GD.Print(message);
        LogEmitted?.Invoke(message);
    }
}
