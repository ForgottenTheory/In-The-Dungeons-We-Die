using System;
using System.Collections.Generic;
using System.Linq;
using Dungeons.Content;
using Dungeons.Presentation;
using Godot;
using static Dungeons.Game.Ui.ConsoleTheme;

namespace Dungeons.Game.Ui;

/// <summary>
/// The emergent crafting bench (docs/emergent-item-system.md §7.1, §6.2c), scoped to the
/// crafting actions one Hideout station offers.
///
/// <para>The layout follows the spec's insistence that order be <b>legible</b>: the player
/// literally sees "Base: Iron Ingot → Step 1: Ember Sap → Step 2: Stormglass" and can reorder
/// the steps, rather than dragging abstract properties around. Permuting the reagents permutes
/// the outcome, and the UI makes that visible.</para>
///
/// <para>The projection panel is <b>required scope</b>, not polish: workability 0 destroys the
/// material, and that rule is only fair if destruction is never a surprise.</para>
///
/// <para>Scoping is presentation only. The Forge shows four thermal actions and the Alchemy Lab
/// shows Distill, but both call the same <c>MaterialTransformationEngine</c> under the same
/// gates — a station decides where you stand, never whether you may. Distill offered at the
/// Alchemy Lab still says "Herblore L12" on its own picker line.</para>
/// </summary>
public partial class CraftingBenchPanel : VBoxContainer
{
    private readonly GameRoot _game;
    private readonly IReadOnlyList<CraftingActionDefinition> _craftingActions;

    private OptionButton _craftingActionPicker = null!;
    private OptionButton _substratePicker = null!;
    private OptionButton _reagentPicker = null!;
    private OptionButton _catalystPicker = null!;
    private Button _addReagentButton = null!;
    private VBoxContainer _reagentChain = null!;
    private Label _affectedQualitiesLabel = null!;
    private VBoxContainer _projectionPanel = null!;
    private Label _advancedProjectionLabel = null!;
    private CheckButton _advancedToggle = null!;
    private Label _substrateInspector = null!;
    private Label _substrateInspectorAdvanced = null!;
    private Button _craftButton = null!;

    /// <summary>The ordered reagent chain being assembled. Order is the mechanic (§0 D2).</summary>
    private readonly List<string> _reagents = new();

    /// <summary>Snapshot backing the material pickers, so a picker index maps to an id.</summary>
    private IReadOnlyList<(string Id, string Name, int Quantity)> _onHand = Array.Empty<(string, string, int)>();

    /// <summary>1–3 ordered reagents (§7.1). Beyond three the chain stops being legible, which
    /// is the whole reason order lives in numbered steps rather than a permutation control.</summary>
    private const int MaxReagentSteps = 3;

    public CraftingBenchPanel(GameRoot game, IReadOnlyList<CraftingActionDefinition> craftingActions)
    {
        _game = game;
        _craftingActions = craftingActions;
        AddThemeConstantOverride("separation", 8);
        Build();
        Refresh();
    }

    private void Build()
    {
        AddChild(SectionTitle("Crafting Bench"));

        // --- Crafting action -------------------------------------------------
        var craftingActionRow = Row();
        AddChild(craftingActionRow);
        craftingActionRow.AddChild(new Label { Text = "Action:", CustomMinimumSize = new Vector2(70, 0) });
        _craftingActionPicker = new OptionButton { CustomMinimumSize = new Vector2(420, 0) };
        foreach (var craftingAction in _craftingActions)
            _craftingActionPicker.AddItem(_game.CraftingActionLabel(craftingAction));
        _craftingActionPicker.ItemSelected += _ => RefreshProjection();
        craftingActionRow.AddChild(_craftingActionPicker);

        _affectedQualitiesLabel = new Label();
        _affectedQualitiesLabel.AddThemeColorOverride("font_color", Muted);
        AddChild(_affectedQualitiesLabel);

        // --- Substrate -------------------------------------------------------
        var substrateRow = Row();
        AddChild(substrateRow);
        substrateRow.AddChild(new Label { Text = "Base:", CustomMinimumSize = new Vector2(70, 0) });
        _substratePicker = new OptionButton { CustomMinimumSize = new Vector2(280, 0) };
        _substratePicker.ItemSelected += _ => RefreshProjection();
        substrateRow.AddChild(_substratePicker);

        // --- Reagent chain ---------------------------------------------------
        var reagentRow = Row();
        AddChild(reagentRow);
        reagentRow.AddChild(new Label { Text = "Add step:", CustomMinimumSize = new Vector2(70, 0) });
        _reagentPicker = new OptionButton { CustomMinimumSize = new Vector2(280, 0) };
        reagentRow.AddChild(_reagentPicker);
        _addReagentButton = MakeButton("Add →", AddReagent, Accent);
        reagentRow.AddChild(_addReagentButton);
        reagentRow.AddChild(MakeButton("Clear", () => { _reagents.Clear(); RebuildReagentChain(); }, Danger));
        var stepCap = new Label { Text = $"max {MaxReagentSteps} steps" };
        stepCap.AddThemeColorOverride("font_color", Muted);
        reagentRow.AddChild(stepCap);

        _reagentChain = new VBoxContainer();
        _reagentChain.AddThemeConstantOverride("separation", 2);
        AddChild(Card(_reagentChain));

        // --- Catalyst --------------------------------------------------------
        var catalystRow = Row();
        AddChild(catalystRow);
        catalystRow.AddChild(new Label { Text = "Catalyst:", CustomMinimumSize = new Vector2(70, 0) });
        _catalystPicker = new OptionButton { CustomMinimumSize = new Vector2(280, 0) };
        _catalystPicker.ItemSelected += _ => RefreshProjection();
        catalystRow.AddChild(_catalystPicker);
        var catalystHint = new Label { Text = "not consumed; lends its affinity" };
        catalystHint.AddThemeColorOverride("font_color", Muted);
        catalystRow.AddChild(catalystHint);

        // --- Projection + commit ---------------------------------------------
        var projectionHeadRow = Row();
        AddChild(projectionHeadRow);
        var projectionHead = new Label { Text = "Before you commit:" };
        projectionHead.AddThemeColorOverride("font_color", Muted);
        projectionHead.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        projectionHeadRow.AddChild(projectionHead);

        // §2F: the numeric voice is one toggle away, never the default.
        _advancedToggle = new CheckButton { Text = "Advanced" };
        _advancedToggle.AddThemeColorOverride("font_color", Muted);
        _advancedToggle.Toggled += _ => RefreshProjection();
        projectionHeadRow.AddChild(_advancedToggle);

        // The pre-commit panel: one row per ProjectionLine, coloured by kind (D30 §3).
        var projectionBox = new VBoxContainer();
        projectionBox.AddThemeConstantOverride("separation", 2);
        _projectionPanel = new VBoxContainer();
        _projectionPanel.AddThemeConstantOverride("separation", 2);
        projectionBox.AddChild(_projectionPanel);
        _advancedProjectionLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
        _advancedProjectionLabel.AddThemeColorOverride("font_color", Muted);
        projectionBox.AddChild(_advancedProjectionLabel);
        AddChild(Card(projectionBox));

        _craftButton = MakeButton("Craft", CommitCraft, Positive);
        _craftButton.CustomMinimumSize = new Vector2(160, 0);
        var commitRow = Row();
        AddChild(commitRow);
        commitRow.AddChild(_craftButton);

        // --- Base-material inspector -----------------------------------------
        var inspectorHead = new Label { Text = "Base material:" };
        inspectorHead.AddThemeColorOverride("font_color", Muted);
        AddChild(inspectorHead);
        var inspectorBox = new VBoxContainer();
        inspectorBox.AddThemeConstantOverride("separation", 2);
        _substrateInspector = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        inspectorBox.AddChild(_substrateInspector);
        _substrateInspectorAdvanced = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
        _substrateInspectorAdvanced.AddThemeColorOverride("font_color", Muted);
        inspectorBox.AddChild(_substrateInspectorAdvanced);
        AddChild(Card(inspectorBox));
    }

    /// <summary>Repopulates the material pickers from what is actually on hand, preserving the
    /// current selection where it still exists.</summary>
    public void Refresh()
    {
        // Read the selections against the *old* list before replacing it — a picker index only
        // means anything relative to the snapshot it was populated from.
        var previousSubstrate = SelectedMaterialId(_substratePicker);
        var previousReagent = SelectedMaterialId(_reagentPicker);
        var previousCatalyst = SelectedMaterialId(_catalystPicker);

        _onHand = _game.MaterialsOnHand;

        RepopulateMaterialPicker(_substratePicker, previousSubstrate, includeNone: false);
        RepopulateMaterialPicker(_reagentPicker, previousReagent, includeNone: false);
        RepopulateMaterialPicker(_catalystPicker, previousCatalyst, includeNone: true);

        // Steps referring to materials no longer on hand would fail the gate confusingly.
        _reagents.RemoveAll(id => _onHand.All(m => m.Id != id));
        RebuildReagentChain();
    }

    /// <summary>
    /// Refills a material picker from what is currently on hand, restoring
    /// <paramref name="previouslySelectedId"/> if that material is still available.
    /// </summary>
    private void RepopulateMaterialPicker(OptionButton picker, string? previouslySelectedId, bool includeNone)
    {
        picker.Clear();

        if (includeNone)
            picker.AddItem("(none)");

        foreach (var material in _onHand)
        {
            var glyphStrip = _game.MaterialStrip(material.Id);
            picker.AddItem($"{material.Name}  ×{material.Quantity}{(glyphStrip.Length > 0 ? "   " + glyphStrip : "")}");
        }

        var restoredIndex = _onHand.ToList().FindIndex(m => m.Id == previouslySelectedId);
        if (restoredIndex >= 0)
            picker.Selected = restoredIndex + (includeNone ? 1 : 0);
        else if (picker.ItemCount > 0)
            picker.Selected = 0;
    }

    /// <summary>The material id a picker is on, or null for "(none)" / an empty picker.</summary>
    private string? SelectedMaterialId(OptionButton picker)
    {
        var offset = picker == _catalystPicker ? 1 : 0;
        var index = picker.Selected - offset;
        return index >= 0 && index < _onHand.Count ? _onHand[index].Id : null;
    }

    private CraftingActionDefinition? SelectedCraftingAction() =>
        _craftingActionPicker.Selected >= 0 && _craftingActionPicker.Selected < _craftingActions.Count
            ? _craftingActions[_craftingActionPicker.Selected]
            : null;

    private void AddReagent()
    {
        if (_reagents.Count >= MaxReagentSteps || SelectedMaterialId(_reagentPicker) is not { } id)
            return;

        _reagents.Add(id);
        RebuildReagentChain();
    }

    /// <summary>Renders the ordered chain, with the reordering controls that make §0 Decision 2
    /// something the player can actually play with.</summary>
    private void RebuildReagentChain()
    {
        ClearChildren(_reagentChain);
        _addReagentButton.Disabled = _reagents.Count >= MaxReagentSteps;

        if (_reagents.Count == 0)
        {
            var empty = new Label { Text = "(no steps — add a reagent above)" };
            empty.AddThemeColorOverride("font_color", Muted);
            _reagentChain.AddChild(empty);
            RefreshProjection();
            return;
        }

        for (var i = 0; i < _reagents.Count; i++)
        {
            var index = i;
            var row = Row(4);
            _reagentChain.AddChild(row);

            var stepStrip = _game.MaterialStrip(_reagents[i]);
            row.AddChild(new Label
            {
                Text = $"Step {i + 1}:  {NameOf(_reagents[i])}{(stepStrip.Length > 0 ? "   " + stepStrip : "")}",
                CustomMinimumSize = new Vector2(260, 0),
            });

            if (index > 0)
                row.AddChild(MakeButton("↑", () => SwapReagents(index, index - 1)));
            if (index < _reagents.Count - 1)
                row.AddChild(MakeButton("↓", () => SwapReagents(index, index + 1)));

            row.AddChild(MakeButton("✕", () => { _reagents.RemoveAt(index); RebuildReagentChain(); }, Danger));
        }

        RefreshProjection();
    }

    private void SwapReagents(int firstIndex, int secondIndex)
    {
        (_reagents[firstIndex], _reagents[secondIndex]) = (_reagents[secondIndex], _reagents[firstIndex]);
        RebuildReagentChain();
    }

    /// <summary>
    /// Recomputes the pre-commit projection (§6.2c). Runs on every selection change, because a
    /// destruction warning the player has to ask for is not a warning.
    /// </summary>
    private void RefreshProjection()
    {
        var advanced = _advancedToggle.ButtonPressed;
        var craftingAction = SelectedCraftingAction();

        _affectedQualitiesLabel.Text = craftingAction is null ? string.Empty
            : advanced ? AdvancedFormat.AffectedQualities(craftingAction)
            : _game.AffectedQualitiesLabel(craftingAction);

        var substrate = SelectedMaterialId(_substratePicker);
        _substrateInspector.Text = substrate is null ? "(nothing selected)" : _game.MaterialSummary(substrate);
        _substrateInspectorAdvanced.Visible = advanced && substrate is not null;
        _substrateInspectorAdvanced.Text = advanced && substrate is not null
            ? _game.MaterialSummaryAdvanced(substrate)
            : string.Empty;

        ClearChildren(_projectionPanel);
        _advancedProjectionLabel.Visible = false;

        if (craftingAction is null || substrate is null || _reagents.Count == 0)
        {
            AddProjectionRow("Choose an action, a base material, and at least one step.", Muted);
            _craftButton.Disabled = true;
            _craftButton.Text = "Craft";
            return;
        }

        var projection = _game.ProjectCraft(craftingAction.Id, substrate, _reagents, SelectedMaterialId(_catalystPicker));
        var reading = _game.ProjectionReading(projection, substrate);

        foreach (var line in _game.ProjectionLines(reading))
            AddProjectionRow(line.Text, LineColor(line.Kind, reading));

        if (advanced)
        {
            _advancedProjectionLabel.Visible = true;
            _advancedProjectionLabel.Text = _game.ProjectionTextAdvanced(projection, substrate);
        }

        // Destruction stays available — pushing a deep material is a legible gamble the player
        // is allowed to take (§6.2c) — but the button says plainly what it will do.
        _craftButton.Disabled = !projection.CanCraft;
        _craftButton.Text = projection.WarnsOfDestruction ? "Craft (destroys!)"
            : projection.WarnsOfRisk ? "Craft (risky)"
            : "Craft";
        _craftButton.AddThemeColorOverride(
            "font_color", projection.WarnsOfDestruction || projection.WarnsOfRisk ? Danger : Positive);
    }

    private void AddProjectionRow(string text, Color color)
    {
        var row = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        row.AddThemeColorOverride("font_color", color);
        _projectionPanel.AddChild(row);
    }

    /// <summary>Colour by line kind (D30 §3) — the client decides colour and layout, never words.</summary>
    private static Color LineColor(ProjectionLineKind kind, CraftReading reading) => kind switch
    {
        ProjectionLineKind.Aim => reading.FirstDiscovery ? Accent : TextColor,
        ProjectionLineKind.Strengthening => Positive,
        ProjectionLineKind.TraitBirth => Positive,
        ProjectionLineKind.Opposition => Accent,
        ProjectionLineKind.Nearby => Accent,
        ProjectionLineKind.Essence => Accent,
        ProjectionLineKind.StressWarning => Danger,
        ProjectionLineKind.Risk => reading.Risk switch
        {
            RiskBand.Perilous or RiskBand.Destroys => Danger,
            RiskBand.Strained => Accent,
            _ => Muted,
        },
        ProjectionLineKind.Failure => Muted,
        _ => Muted,
    };

    private void CommitCraft()
    {
        if (SelectedCraftingAction() is not { } craftingAction)
            return;
        if (SelectedMaterialId(_substratePicker) is not { } substrate)
            return;

        var outcome = _game.Craft(
            craftingAction.Id, substrate, _reagents.ToList(), SelectedMaterialId(_catalystPicker));

        // A successful craft consumed its steps; keep the base pointed at the result so the
        // recursion the system exists for is one click away.
        if (outcome.Success)
        {
            _reagents.Clear();
            Refresh();

            if (outcome.ResultItemId is { } produced)
            {
                var index = _onHand.ToList().FindIndex(m => m.Id == produced);
                if (index >= 0)
                    _substratePicker.Selected = index;
            }
        }

        RefreshProjection();
    }

    private string NameOf(string materialId) =>
        _onHand.FirstOrDefault(m => m.Id == materialId).Name ?? materialId;
}
