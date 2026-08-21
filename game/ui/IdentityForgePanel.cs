using System;
using System.Collections.Generic;
using System.Linq;
using Dungeons.Crafting;
using Dungeons.Crafting.Identity;
using Dungeons.Presentation;
using Godot;
using static Dungeons.Game.Ui.ConsoleTheme;

namespace Dungeons.Game.Ui;

/// <summary>
/// The identity forge (migration Phase 3, D50/D51) — mints equipment from identity-model
/// materials through the item-effect pipeline, beside the old assembly panel until the
/// surfaces swap.
///
/// <para>The preview is the projection itself: the guaranteed floor, the draw table and the
/// odds — "I am engineering the odds," on screen before the click. Since the Phase 6
/// semantic pass it speaks through <see cref="MintReadings"/>: likelihood words for the
/// table (D53), sentences in player language, and the exact scores one Advanced toggle away.</para>
/// </summary>
public partial class IdentityForgePanel : VBoxContainer
{
    private readonly GameRoot _game;
    private readonly IReadOnlyList<EquipmentBlueprintDefinition> _forms;

    private OptionButton _formPicker = null!;
    private VBoxContainer _slotRows = null!;
    private CheckButton _advancedToggle = null!;
    private Label _previewLabel = null!;
    private Button _forgeButton = null!;

    /// <summary>Per-slot pickers with their index-aligned material ids, rebuilt whenever the
    /// form or the inventory changes.</summary>
    private readonly Dictionary<string, (OptionButton Picker, IReadOnlyList<string> ItemIds)> _slotPickers =
        new(StringComparer.Ordinal);

    public IdentityForgePanel(GameRoot game)
    {
        _game = game;
        _forms = game.IdentityForgeForms();
        AddThemeConstantOverride("separation", 8);
        Build();
        Refresh();
    }

    private void Build()
    {
        AddChild(SectionTitle("Identity Forge"));

        var formRow = Row();
        AddChild(formRow);
        formRow.AddChild(new Label { Text = "Form:", CustomMinimumSize = new Vector2(70, 0) });
        _formPicker = new OptionButton { CustomMinimumSize = new Vector2(280, 0) };
        foreach (var form in _forms)
            _formPicker.AddItem(form.Name);
        _formPicker.ItemSelected += _ => RebuildSlotRows();
        formRow.AddChild(_formPicker);

        _advancedToggle = new CheckButton { Text = "Advanced" };
        _advancedToggle.Toggled += _ => RefreshPreview();
        formRow.AddChild(_advancedToggle);

        _slotRows = new VBoxContainer();
        _slotRows.AddThemeConstantOverride("separation", 4);
        AddChild(_slotRows);

        _previewLabel = Wrapping(Muted);
        AddChild(Card(_previewLabel));

        _forgeButton = MakeButton("Forge", Forge, Accent);
        AddChild(_forgeButton);
    }

    public void Refresh() => RebuildSlotRows();

    private EquipmentBlueprintDefinition? SelectedForm =>
        _formPicker.Selected >= 0 && _formPicker.Selected < _forms.Count
            ? _forms[_formPicker.Selected]
            : null;

    /// <summary>One picker per form slot, offering only migrated materials on hand that pass
    /// the slot's tag gate — the same filter the composer enforces, applied early so the
    /// menu never offers a refusal. Rows carry the material's stakes and its overfill word,
    /// because an Unstable component is a choice the crafter makes at the menu.</summary>
    private void RebuildSlotRows()
    {
        foreach (var child in _slotRows.GetChildren())
            child.QueueFree();
        _slotPickers.Clear();

        if (SelectedForm is not { } form)
        {
            RefreshPreview();
            return;
        }

        var onHand = _game.MaterialsOnHand
            .Where(material => _game.IdentityStateOf(material.Id) is not null)
            .ToList();

        foreach (var (slotName, slot) in form.Slots.OrderByDescending(s => s.Value.MassShare))
        {
            var row = Row();
            _slotRows.AddChild(row);
            row.AddChild(new Label { Text = $"{slotName}:", CustomMinimumSize = new Vector2(70, 0) });

            var eligible = onHand
                .Where(material => slot.RequiresTags.Count == 0
                    || slot.RequiresTags.Any(tag =>
                        _game.MaterialTagsOf(material.Id).Contains(tag, StringComparer.OrdinalIgnoreCase)))
                .ToList();

            var picker = new OptionButton { CustomMinimumSize = new Vector2(280, 0) };
            foreach (var (id, name, quantity) in eligible)
                picker.AddItem($"{name} ×{quantity}{_game.MaterialStakeSummary(id)}");
            picker.ItemSelected += _ => RefreshPreview();
            row.AddChild(picker);

            _slotPickers[slotName] = (picker, eligible.Select(material => material.Id).ToList());
        }

        RefreshPreview();
    }

    private IReadOnlyDictionary<string, string>? CurrentComponents()
    {
        if (SelectedForm is null || _slotPickers.Count == 0)
            return null;

        var components = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (slotName, (picker, itemIds)) in _slotPickers)
        {
            if (picker.Selected < 0 || picker.Selected >= itemIds.Count)
                return null;
            components[slotName] = itemIds[picker.Selected];
        }

        return components;
    }

    private void RefreshPreview()
    {
        if (SelectedForm is not { } form || CurrentComponents() is not { } components)
        {
            _previewLabel.Text = "Fill every slot with a migrated material.";
            _forgeButton.Disabled = true;
            return;
        }

        var preview = _game.PreviewIdentityFabrication(form.Id, components);
        if (preview.GateFailure is not null)
        {
            _previewLabel.Text = preview.GateDetail;
            _forgeButton.Disabled = true;
            return;
        }
        if (preview.Composition is not { Failure: IdentityCompositionFailure.None } composition)
        {
            _previewLabel.Text = MintReadings.CompositionRefusal(
                preview.Composition?.Failure ?? IdentityCompositionFailure.MissingComponent);
            _forgeButton.Disabled = true;
            return;
        }

        _previewLabel.Text = _advancedToggle.ButtonPressed
            ? _game.IdentityMintAdvancedText(composition, preview.Effects!)
            : _game.IdentityMintPreviewText(composition, preview.Effects!, preview.WouldBeFirstOfItsKind);
        _forgeButton.Disabled = false;
    }

    private void Forge()
    {
        if (SelectedForm is not { } form || CurrentComponents() is not { } components)
            return;

        _game.RunIdentityFabrication(form.Id, components);
        Refresh();
    }
}
