using System;
using System.Linq;
using Dungeons.Professions;
using Godot;
using static Dungeons.Game.Ui.ConsoleTheme;

namespace Dungeons.Game.Ui;

/// <summary>
/// The Agility course: five slots, one obstacle each. What is fitted <em>is</em> the player's
/// standing travel/hazard/extraction loadout, so this is a configuration screen that happens to
/// grant XP rather than another action ladder. Appears at whichever station hosts
/// <c>profession.agility</c>.
/// </summary>
public partial class TrainingCoursePanel : VBoxContainer
{
    private readonly GameRoot _game;
    private Label _summaryLabel = null!;
    private VBoxContainer _slotRows = null!;

    public TrainingCoursePanel(GameRoot game)
    {
        _game = game;
        AddThemeConstantOverride("separation", 4);
        Build();
        Refresh();
    }

    private void Build()
    {
        AddChild(SectionTitle("Course"));
        _summaryLabel = new Label();
        AddChild(Card(_summaryLabel));
        _slotRows = new VBoxContainer();
        AddChild(_slotRows);
        AddChild(MakeButton("Run a lap", () => _game.RunTrainingLap(), Accent));
    }

    public void Refresh()
    {
        ClearChildren(_slotRows);
        _summaryLabel.Text = _game.TrainingCourseSummary();

        foreach (var slot in Enum.GetValues<TrainingSlot>())
        {
            var available = _game.ObstaclesFor(slot);
            var row = Row();
            _slotRows.AddChild(row);
            row.AddChild(new Label { Text = slot.ToString(), CustomMinimumSize = new Vector2(90, 0) });

            if (available.Count == 0)
            {
                var none = new Label { Text = "nothing unlocked yet" };
                none.AddThemeColorOverride("font_color", Muted);
                row.AddChild(none);
                continue;
            }

            var picker = new OptionButton { CustomMinimumSize = new Vector2(220, 0) };
            foreach (var obstacle in available)
                picker.AddItem(obstacle.Name);

            var fitted = _game.FittedObstacle(slot);
            if (fitted is not null)
            {
                var fittedIndex = available.ToList().FindIndex(o => o.Id == fitted);
                if (fittedIndex >= 0)
                    picker.Selected = fittedIndex;
            }

            row.AddChild(picker);
            var slotValue = slot;
            row.AddChild(MakeButton("Fit", () =>
            {
                _game.FitObstacle(slotValue, available[Mathf.Clamp(picker.Selected, 0, available.Count - 1)].Id);
                Refresh();
            }, Accent));
            row.AddChild(MakeButton("Clear", () =>
            {
                _game.ClearObstacle(slotValue);
                Refresh();
            }));
        }
    }
}
