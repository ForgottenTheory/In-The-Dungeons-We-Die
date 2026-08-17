using System;
using System.Collections.Generic;
using System.Linq;
using Dungeons.Presentation;
using Godot;
using static Dungeons.Game.Ui.ConsoleTheme;

namespace Dungeons.Game.Ui;

/// <summary>
/// The Assay Table: read a material as deeply as the profession has earned.
///
/// <para>Assay pays in comprehension, never in power — the reading is computed identically at
/// every level, and levelling only replaces <c>???</c> with what the material was always doing.
/// So this panel is the redaction made visible: the reveal ladder with its thresholds, and one
/// material read through <see cref="AssayLens"/> at the current depth.</para>
/// </summary>
public partial class AssayBenchPanel : VBoxContainer
{
    private readonly GameRoot _game;

    private Label _depthLabel = null!;
    private VBoxContainer _revealLadder = null!;
    private OptionButton _materialPicker = null!;
    private Label _reading = null!;

    private IReadOnlyList<(string Id, string Name, int Quantity)> _onHand = Array.Empty<(string, string, int)>();

    public AssayBenchPanel(GameRoot game)
    {
        _game = game;
        AddThemeConstantOverride("separation", 8);
        Build();
        Refresh();
    }

    private void Build()
    {
        AddChild(SectionTitle("Assay"));

        _depthLabel = Wrapping(Muted);
        AddChild(_depthLabel);

        _revealLadder = new VBoxContainer();
        _revealLadder.AddThemeConstantOverride("separation", 2);
        AddChild(Card(_revealLadder));

        var pickerRow = Row();
        AddChild(pickerRow);
        pickerRow.AddChild(new Label { Text = "Read:", CustomMinimumSize = new Vector2(70, 0) });
        _materialPicker = new OptionButton { CustomMinimumSize = new Vector2(280, 0) };
        _materialPicker.ItemSelected += _ => RefreshReading();
        pickerRow.AddChild(_materialPicker);

        _reading = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(Card(_reading));
    }

    public void Refresh()
    {
        var level = _game.ProfessionLevel("profession.assay");
        var depth = _game.CurrentAssayDepth;
        _depthLabel.Text = $"Assay L{level} reads to {depth}. Every level removes a ??? — never adds a point of anything.";

        ClearChildren(_revealLadder);
        foreach (var facet in Enum.GetValues<AssayFacet>())
        {
            var requiredLevel = AssayLens.LevelFor(facet);
            var revealed = AssayLens.Reveals(depth, facet);
            var row = new Label
            {
                Text = revealed
                    ? $"✓ {AssayLens.FacetLabel(facet)}"
                    : $"{AssayTuning.Redacted} {AssayLens.FacetLabel(facet)}  (Assay L{requiredLevel})",
            };
            row.AddThemeColorOverride("font_color", revealed ? TextColor : Muted);
            _revealLadder.AddChild(row);
        }

        var previouslySelected = SelectedMaterialId();
        _onHand = _game.MaterialsOnHand;
        _materialPicker.Clear();
        foreach (var material in _onHand)
            _materialPicker.AddItem($"{material.Name}  ×{material.Quantity}");

        var restoredIndex = _onHand.ToList().FindIndex(m => m.Id == previouslySelected);
        if (restoredIndex >= 0)
            _materialPicker.Selected = restoredIndex;
        else if (_materialPicker.ItemCount > 0)
            _materialPicker.Selected = 0;

        RefreshReading();
    }

    private string? SelectedMaterialId() =>
        _materialPicker.Selected >= 0 && _materialPicker.Selected < _onHand.Count
            ? _onHand[_materialPicker.Selected].Id
            : null;

    private void RefreshReading() =>
        _reading.Text = SelectedMaterialId() is { } materialId
            ? _game.MaterialSummaryAssayed(materialId)
            : "(nothing on hand to read)";
}
