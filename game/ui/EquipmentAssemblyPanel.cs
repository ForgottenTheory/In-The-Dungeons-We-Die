using System.Collections.Generic;
using System.Linq;
using Dungeons.Crafting;
using Godot;
using static Dungeons.Game.Ui.ConsoleTheme;

namespace Dungeons.Game.Ui;

/// <summary>
/// Fabrication (docs/code-map.md §10.5), scoped to the blueprints one Hideout station can
/// assemble — the Forge builds a Longsword, the Loom and the Tannery both build a Vest.
///
/// <para>Pick a blueprint, then a material per named slot; each slot's picker lists only what
/// its tag gate accepts, crafted and authored materials alike. The pre-commit card is not
/// polish: fabrication is terminal and the components are consumed forever, so the preview
/// runs through the same <c>Compose</c> the real item will.</para>
///
/// <para>"Latest work" is the §6 reveal — the payoff screen for the experiment, in gameplay
/// language rather than a log line.</para>
/// </summary>
public partial class EquipmentAssemblyPanel : VBoxContainer
{
    private readonly GameRoot _game;
    private readonly IReadOnlyList<EquipmentBlueprintDefinition> _blueprints;

    private OptionButton _blueprintPicker = null!;
    private VBoxContainer _slotRows = null!;
    private Label _preview = null!;
    private Label _latestWorkLabel = null!;
    private long _latestWorkInstanceId = -1;

    private readonly Dictionary<string, OptionButton> _slotPickers = new();
    private readonly Dictionary<string, Label> _slotFitLabels = new();
    private readonly Dictionary<string, IReadOnlyList<(string Id, string Name, int Quantity)>> _slotEligible = new();

    public EquipmentAssemblyPanel(GameRoot game, IReadOnlyList<EquipmentBlueprintDefinition> blueprints)
    {
        _game = game;
        _blueprints = blueprints;
        AddThemeConstantOverride("separation", 8);
        Build();
        Refresh();
    }

    private void Build()
    {
        AddChild(SectionTitle("Fabrication"));

        var blueprintRow = Row();
        AddChild(blueprintRow);
        blueprintRow.AddChild(new Label { Text = "Make:", CustomMinimumSize = new Vector2(70, 0) });
        _blueprintPicker = new OptionButton { CustomMinimumSize = new Vector2(180, 0) };
        foreach (var blueprint in _blueprints)
            _blueprintPicker.AddItem(blueprint.Name);
        _blueprintPicker.ItemSelected += _ => RebuildSlotRows();
        blueprintRow.AddChild(_blueprintPicker);
        blueprintRow.AddChild(MakeButton("Fabricate", CommitFabrication, Positive));

        _slotRows = new VBoxContainer();
        _slotRows.AddThemeConstantOverride("separation", 4);
        AddChild(_slotRows);

        _preview = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _preview.AddThemeColorOverride("font_color", Muted);
        AddChild(Card(_preview));

        var latestHeadRow = Row();
        AddChild(latestHeadRow);
        var latestHead = new Label { Text = "Latest work:" };
        latestHead.AddThemeColorOverride("font_color", Muted);
        latestHead.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        latestHeadRow.AddChild(latestHead);
        latestHeadRow.AddChild(MakeButton("Reroll (debug)", DebugRerollLatest));

        _latestWorkLabel = new Label { Text = "(nothing fabricated here yet)", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _latestWorkLabel.AddThemeColorOverride("font_color", Muted);
        AddChild(Card(_latestWorkLabel));
    }

    public void Refresh() => RebuildSlotRows();

    private EquipmentBlueprintDefinition? SelectedBlueprint() =>
        _blueprintPicker.Selected >= 0 && _blueprintPicker.Selected < _blueprints.Count
            ? _blueprints[_blueprintPicker.Selected]
            : null;

    /// <summary>One row per slot of the chosen blueprint, each picker filtered by the slot's tag
    /// gate. Selections survive rebuilds where the material is still eligible.</summary>
    private void RebuildSlotRows()
    {
        var previous = _slotPickers.ToDictionary(
            p => p.Key,
            p => _slotEligible.TryGetValue(p.Key, out var list) && p.Value.Selected >= 0 && p.Value.Selected < list.Count
                ? list[p.Value.Selected].Id
                : null);

        ClearChildren(_slotRows);
        _slotPickers.Clear();
        _slotFitLabels.Clear();
        _slotEligible.Clear();

        if (SelectedBlueprint() is not { } blueprint)
            return;

        foreach (var (slotName, _) in blueprint.Slots)
        {
            var row = Row();
            _slotRows.AddChild(row);
            row.AddChild(new Label { Text = $"  {slotName}:", CustomMinimumSize = new Vector2(90, 0) });

            var eligible = _game.EligibleForSlot(blueprint.Id, slotName);
            var picker = new OptionButton { CustomMinimumSize = new Vector2(220, 0) };
            foreach (var material in eligible)
            {
                var strip = _game.MaterialStrip(material.Id);
                picker.AddItem($"{material.Name}  ×{material.Quantity}{(strip.Length > 0 ? "   " + strip : "")}");
            }

            var restored = eligible.ToList().FindIndex(m => m.Id == previous.GetValueOrDefault(slotName));
            picker.Selected = restored >= 0 ? restored : (picker.ItemCount > 0 ? 0 : -1);

            var slot = slotName;
            picker.ItemSelected += _ => { RefreshSlotFit(slot); RefreshPreview(); };

            _slotPickers[slotName] = picker;
            _slotEligible[slotName] = eligible;
            row.AddChild(picker);

            // §2E: why this material appears suitable here — derived from the blueprint's own data.
            var fit = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            fit.AddThemeColorOverride("font_color", Muted);
            _slotFitLabels[slotName] = fit;
            _slotRows.AddChild(Indent(fit));

            RefreshSlotFit(slotName);
        }

        RefreshPreview();
    }

    /// <summary>The chosen material id per slot, or null while any slot is empty.</summary>
    private Dictionary<string, string>? ChosenSlotMaterials()
    {
        var chosen = new Dictionary<string, string>();
        foreach (var (slotName, picker) in _slotPickers)
        {
            var eligible = _slotEligible[slotName];
            if (picker.Selected < 0 || picker.Selected >= eligible.Count)
                return null;
            chosen[slotName] = eligible[picker.Selected].Id;
        }

        return chosen;
    }

    private void RefreshSlotFit(string slotName)
    {
        if (!_slotFitLabels.TryGetValue(slotName, out var label) || SelectedBlueprint() is not { } blueprint)
            return;

        var eligible = _slotEligible[slotName];
        var picker = _slotPickers[slotName];
        label.Text = picker.Selected >= 0 && picker.Selected < eligible.Count
            ? _game.SlotFitText(blueprint.Id, slotName, eligible[picker.Selected].Id)
            : "(nothing eligible on hand)";
    }

    private void RefreshPreview()
    {
        if (SelectedBlueprint() is not { } blueprint)
            return;

        var chosen = ChosenSlotMaterials();
        _preview.Text = chosen is null
            ? "(choose a material for every slot)"
            : _game.FabricationPreviewText(blueprint.Id, chosen);
        _preview.AddThemeColorOverride("font_color", chosen is null ? Muted : TextColor);
    }

    private void CommitFabrication()
    {
        if (SelectedBlueprint() is not { } blueprint || ChosenSlotMaterials() is not { } chosen)
            return; // a slot has nothing eligible — nothing sensible to commit

        var outcome = _game.FabricateItem(blueprint.Id, chosen);

        // The §6 reveal: the experiment pays off in gameplay language, not a log line.
        if (outcome.Success && outcome.Item is { } item)
        {
            _latestWorkInstanceId = item.InstanceId;
            _latestWorkLabel.Text = _game.ItemCardText(item);
            _latestWorkLabel.AddThemeColorOverride("font_color", outcome.IsFirstOfItsKind ? Accent : TextColor);
        }

        Refresh();
    }

    private void DebugRerollLatest()
    {
        if (_latestWorkInstanceId < 0)
            return;

        if (_game.DebugRerollAffixes(_latestWorkInstanceId) is { } rerolled)
            _latestWorkLabel.Text = _game.ItemCardText(rerolled);
    }
}
