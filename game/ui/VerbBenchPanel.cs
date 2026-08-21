using System;
using System.Collections.Generic;
using System.Linq;
using Dungeons.Content;
using Dungeons.Crafting.Identity;
using Godot;
using static Dungeons.Game.Ui.ConsoleTheme;

namespace Dungeons.Game.Ui;

/// <summary>
/// The identity bench (migration Phase 2c, D47) — the verb actions one Hideout station
/// offers, run against the materials on hand.
///
/// <para>The preview is <b>required scope</b>, exactly as it was on the old bench: risk only
/// ever lives where the crafter chose it (an overfilled material, Fragile work), and that is
/// only fair if the odds are on screen before the click.</para>
///
/// <para>Wording here is the engine's own step text for now — the semantic-layer pass over
/// the identity system is migration Phase 6, and this panel deliberately does not invent a
/// second vocabulary in the meantime (D30: translate, never recompute).</para>
/// </summary>
public partial class VerbBenchPanel : VBoxContainer
{
    private readonly GameRoot _game;
    private readonly IReadOnlyList<VerbActionDefinition> _actions;

    private OptionButton _actionPicker = null!;
    private Label _actionDescription = null!;
    private OptionButton _substratePicker = null!;
    private HBoxContainer _sourceRow = null!;
    private OptionButton _sourcePicker = null!;
    private SpinBox _sourceCount = null!;
    private HBoxContainer _targetRow = null!;
    private OptionButton _targetPicker = null!;
    private HBoxContainer _displacedRow = null!;
    private OptionButton _displacedPicker = null!;
    private Label _previewLabel = null!;
    private Button _runButton = null!;

    /// <summary>Snapshot backing the material pickers, so a picker index maps to an id.
    /// Only migrated materials appear — the identity bench cannot work the rest yet.</summary>
    private IReadOnlyList<(string Id, string Name, int Quantity)> _onHand =
        Array.Empty<(string, string, int)>();

    /// <summary>Identity ids behind the target/displaced pickers, index-aligned.</summary>
    private IReadOnlyList<string> _targetIds = Array.Empty<string>();
    private IReadOnlyList<string> _displacedIds = Array.Empty<string>();

    public VerbBenchPanel(GameRoot game, IReadOnlyList<VerbActionDefinition> actions)
    {
        _game = game;
        _actions = actions;
        AddThemeConstantOverride("separation", 8);
        Build();
        Refresh();
    }

    private void Build()
    {
        AddChild(SectionTitle("Identity Bench"));

        var actionRow = Row();
        AddChild(actionRow);
        actionRow.AddChild(new Label { Text = "Action:", CustomMinimumSize = new Vector2(70, 0) });
        _actionPicker = new OptionButton { CustomMinimumSize = new Vector2(420, 0) };
        foreach (var action in _actions)
            _actionPicker.AddItem(_game.VerbActionLabel(action));
        _actionPicker.ItemSelected += _ => OnShapeChanged();
        actionRow.AddChild(_actionPicker);

        _actionDescription = Wrapping(Muted);
        AddChild(_actionDescription);

        var substrateRow = Row();
        AddChild(substrateRow);
        substrateRow.AddChild(new Label { Text = "Material:", CustomMinimumSize = new Vector2(70, 0) });
        _substratePicker = new OptionButton { CustomMinimumSize = new Vector2(280, 0) };
        _substratePicker.ItemSelected += _ => OnShapeChanged();
        substrateRow.AddChild(_substratePicker);

        _sourceRow = Row();
        AddChild(_sourceRow);
        _sourceRow.AddChild(new Label { Text = "Source:", CustomMinimumSize = new Vector2(70, 0) });
        _sourcePicker = new OptionButton { CustomMinimumSize = new Vector2(280, 0) };
        _sourcePicker.ItemSelected += _ => RefreshPreview();
        _sourceRow.AddChild(_sourcePicker);
        _sourceCount = new SpinBox { MinValue = 1, MaxValue = 5, Value = 1 };
        _sourceCount.ValueChanged += _ => RefreshPreview();
        _sourceRow.AddChild(_sourceCount);

        _targetRow = Row();
        AddChild(_targetRow);
        _targetRow.AddChild(new Label { Text = "Identity:", CustomMinimumSize = new Vector2(70, 0) });
        _targetPicker = new OptionButton { CustomMinimumSize = new Vector2(280, 0) };
        _targetPicker.ItemSelected += _ => RefreshPreview();
        _targetRow.AddChild(_targetPicker);

        _displacedRow = Row();
        AddChild(_displacedRow);
        _displacedRow.AddChild(new Label { Text = "Eject:", CustomMinimumSize = new Vector2(70, 0) });
        _displacedPicker = new OptionButton { CustomMinimumSize = new Vector2(280, 0) };
        _displacedPicker.ItemSelected += _ => RefreshPreview();
        _displacedRow.AddChild(_displacedPicker);

        _previewLabel = Wrapping(Muted);
        AddChild(Card(_previewLabel));

        _runButton = MakeButton("Work the material", Run, Accent);
        AddChild(_runButton);
    }

    public void Refresh()
    {
        _onHand = _game.MaterialsOnHand
            .Where(material => _game.IdentityStateOf(material.Id) is not null)
            .ToList();

        RefillMaterialPicker(_substratePicker);
        RefillMaterialPicker(_sourcePicker);
        OnShapeChanged();
    }

    private void RefillMaterialPicker(OptionButton picker)
    {
        var previous = picker.Selected;
        picker.Clear();
        foreach (var (id, name, quantity) in _onHand)
        {
            var state = _game.IdentityStateOf(id);
            var carried = state is { Identities.Count: > 0 }
                ? " · " + string.Join(", ", state.Identities.Select(s => _game.IdentityNameOf(s.Id)))
                : string.Empty;
            picker.AddItem($"{name} ×{quantity}{carried}");
        }
        if (previous >= 0 && previous < picker.ItemCount)
            picker.Selected = previous;
    }

    private VerbActionDefinition? SelectedAction =>
        _actionPicker.Selected >= 0 && _actionPicker.Selected < _actions.Count
            ? _actions[_actionPicker.Selected]
            : null;

    private string? SelectedId(OptionButton picker) =>
        picker.Selected >= 0 && picker.Selected < _onHand.Count ? _onHand[picker.Selected].Id : null;

    /// <summary>The verb decides which rows exist: sources feed Transfer/Displace/Develop/Fuse,
    /// an identity is picked for Reveal/Extract/Develop, Displace also picks what to eject.</summary>
    private void OnShapeChanged()
    {
        var action = SelectedAction;
        _actionDescription.Text = action?.Description ?? string.Empty;

        var verb = action?.Verb;
        var wantsSources = verb is CraftVerb.Transfer or CraftVerb.Displace or CraftVerb.Develop or CraftVerb.Fuse;
        _sourceRow.Visible = wantsSources;
        _sourceCount.Visible = verb is CraftVerb.Develop or CraftVerb.Fuse;
        if (verb is CraftVerb.Transfer or CraftVerb.Displace)
            _sourceCount.Value = 1;

        var wantsTarget = verb is CraftVerb.Reveal or CraftVerb.Extract or CraftVerb.Develop;
        _targetRow.Visible = wantsTarget;
        if (wantsTarget)
            RefillTargetPicker(verb!.Value);

        _displacedRow.Visible = verb is CraftVerb.Displace;
        if (verb is CraftVerb.Displace)
            RefillDisplacedPicker();

        RefreshPreview();
    }

    private void RefillTargetPicker(CraftVerb verb)
    {
        var state = SelectedId(_substratePicker) is { } id ? _game.IdentityStateOf(id) : null;
        _targetIds = verb == CraftVerb.Reveal
            ? state?.Latent ?? Array.Empty<string>()
            : state?.Identities.Select(s => s.Id).ToList() ?? (IReadOnlyList<string>)Array.Empty<string>();

        _targetPicker.Clear();
        foreach (var identityId in _targetIds)
            _targetPicker.AddItem(_game.IdentityNameOf(identityId));
    }

    private void RefillDisplacedPicker()
    {
        var state = SelectedId(_substratePicker) is { } id ? _game.IdentityStateOf(id) : null;
        _displacedIds = state?.Identities.Select(s => s.Id).ToList()
            ?? (IReadOnlyList<string>)Array.Empty<string>();

        _displacedPicker.Clear();
        foreach (var identityId in _displacedIds)
            _displacedPicker.AddItem(_game.IdentityNameOf(identityId));
    }

    private (string ActionId, string SubstrateId, IReadOnlyList<string> Sources, string? Target, string? Displaced)?
        CurrentInvocation()
    {
        if (SelectedAction is not { } action || SelectedId(_substratePicker) is not { } substrateId)
            return null;

        var sources = _sourceRow.Visible && SelectedId(_sourcePicker) is { } sourceId
            ? Enumerable.Repeat(sourceId, (int)_sourceCount.Value).ToList()
            : (IReadOnlyList<string>)Array.Empty<string>();
        var target = _targetRow.Visible && _targetPicker.Selected >= 0 && _targetPicker.Selected < _targetIds.Count
            ? _targetIds[_targetPicker.Selected]
            : null;
        var displaced = _displacedRow.Visible && _displacedPicker.Selected >= 0 && _displacedPicker.Selected < _displacedIds.Count
            ? _displacedIds[_displacedPicker.Selected]
            : null;
        return (action.Id, substrateId, sources, target, displaced);
    }

    private void RefreshPreview()
    {
        if (CurrentInvocation() is not { } invocation)
        {
            _previewLabel.Text = _onHand.Count == 0
                ? "Nothing on hand the identity bench can work yet."
                : "Pick an action and a material.";
            _runButton.Disabled = true;
            return;
        }

        var preview = _game.PreviewVerbAction(
            invocation.ActionId, invocation.SubstrateId, invocation.Sources,
            invocation.Target, invocation.Displaced);

        if (preview.GateFailure is not null)
        {
            _previewLabel.Text = preview.GateDetail;
            _runButton.Disabled = true;
            return;
        }

        var projection = preview.Projection!;
        if (projection.Failure is { } refusal)
        {
            _previewLabel.Text = $"The material refuses: {refusal}.";
            _runButton.Disabled = true;
            return;
        }

        _previewLabel.Text = string.Join("\n", projection.Steps.Select(step => step.Detail));
        _runButton.Disabled = false;
    }

    private void Run()
    {
        if (CurrentInvocation() is not { } invocation)
            return;

        _game.RunVerbAction(
            invocation.ActionId, invocation.SubstrateId, invocation.Sources,
            invocation.Target, invocation.Displaced);
        Refresh();
    }
}
