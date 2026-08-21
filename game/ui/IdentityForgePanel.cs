using System;
using System.Collections.Generic;
using System.Linq;
using Dungeons.Crafting;
using Dungeons.Crafting.Identity;
using Godot;
using static Dungeons.Game.Ui.ConsoleTheme;

namespace Dungeons.Game.Ui;

/// <summary>
/// The identity forge (migration Phase 3, D50/D51) — mints equipment from identity-model
/// materials through the item-effect pipeline, beside the old assembly panel until the
/// surfaces swap.
///
/// <para>The preview is the projection itself: the guaranteed floor, the scored candidate
/// table the draws come from, and the odds — "I am engineering the odds," on screen before
/// the click. Wording is engine vocabulary until the Phase 6 semantic pass.</para>
/// </summary>
public partial class IdentityForgePanel : VBoxContainer
{
    private readonly GameRoot _game;
    private readonly IReadOnlyList<EquipmentBlueprintDefinition> _forms;

    private OptionButton _formPicker = null!;
    private VBoxContainer _slotRows = null!;
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
    /// menu never offers a refusal.</summary>
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
            {
                var state = _game.IdentityStateOf(id);
                var carried = state is { Identities.Count: > 0 }
                    ? " · " + string.Join(", ", state.Identities.Select(s => _game.IdentityNameOf(s.Id)))
                    : string.Empty;
                picker.AddItem($"{name} ×{quantity}{carried}");
            }
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
            _previewLabel.Text = $"The composition refuses: {preview.Composition?.Failure}.";
            _forgeButton.Disabled = true;
            return;
        }

        _previewLabel.Text = DescribeProjection(composition, preview.Effects!, preview.WouldBeFirstOfItsKind);
        _forgeButton.Disabled = false;
    }

    private string DescribeProjection(
        IdentityComposition composition, ItemEffectProjection effects, bool firstOfItsKind)
    {
        var lines = new List<string>
        {
            firstOfItsKind ? $"{composition.Name} — first of its kind" : composition.Name,
            DescribeDelivery(composition.BaseDelivery),
        };

        if (composition.Dormant.Count > 0)
            lines.Add("Dormant: " + string.Join(", ", composition.Dormant.Select(s => _game.IdentityNameOf(s.Id))));

        foreach (var sentence in effects.Floor)
            lines.Add($"Guaranteed: {DescribeSentence(sentence)}");

        if (effects.GeneratedSentenceCount > 0 && effects.Candidates.Count > 0)
        {
            lines.Add($"Will draw {effects.GeneratedSentenceCount} from:");
            foreach (var candidate in effects.Candidates)
            {
                var breach = candidate.FromProfileBreach ? " ◇" : string.Empty;
                lines.Add($"  {candidate.Score,6:0.#}  {_game.TriggerNameOf(candidate.TriggerId)} → " +
                    $"{_game.BehaviorNameOf(candidate.BehaviorId)} → {_game.PayloadNameOf(candidate.PayloadId)}{breach}");
            }
        }

        if (effects.SignatureChance > 0)
            lines.Add($"Signature odds: {effects.SignatureChance:P0}");
        if (effects.DrawbackChance > 0)
            lines.Add($"Drawback odds: {effects.DrawbackChance:P0} — the price of Volatile stock");

        return string.Join("\n", lines);
    }

    private static string DescribeDelivery(ItemBaseDelivery delivery)
    {
        var parts = new List<string>();
        if (delivery.DamageBonus != 0)
            parts.Add($"+{delivery.DamageBonus:0.#} damage");
        if (delivery.WindupTicks != 0)
            parts.Add($"+{delivery.WindupTicks} windup");
        if (delivery.Armor != 0)
            parts.Add($"+{delivery.Armor:0.#} armor");
        return parts.Count == 0 ? "No physical delivery — an identity vessel." : string.Join(" · ", parts);
    }

    private string DescribeSentence(ItemEffectSentence sentence)
    {
        var chance = sentence.Chance < 1.0 ? $" @ {sentence.Chance:P0}" : string.Empty;
        return $"{_game.TriggerNameOf(sentence.TriggerId)} → {_game.BehaviorNameOf(sentence.BehaviorId)} → " +
            $"{_game.PayloadNameOf(sentence.PayloadId)} {sentence.Magnitude:0.##}{chance}";
    }

    private void Forge()
    {
        if (SelectedForm is not { } form || CurrentComponents() is not { } components)
            return;

        _game.RunIdentityFabrication(form.Id, components);
        Refresh();
    }
}
