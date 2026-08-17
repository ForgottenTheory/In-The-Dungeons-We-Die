using Godot;
using static Dungeons.Game.Ui.ConsoleTheme;

namespace Dungeons.Game.Ui;

/// <summary>
/// The Garden Plots' parallel beds — Farming's own system rather than a rung on the ladder,
/// because plots run several at once and keep growing on the world clock while the game is
/// closed. Appears at whichever station hosts <c>profession.farming</c>.
/// </summary>
public partial class FarmingPlotsPanel : VBoxContainer
{
    private readonly GameRoot _game;
    private Label _header = null!;
    private VBoxContainer _plotRows = null!;

    public FarmingPlotsPanel(GameRoot game)
    {
        _game = game;
        AddThemeConstantOverride("separation", 4);
        Build();
        Refresh();
    }

    private void Build()
    {
        AddChild(SectionTitle("Plots"));
        _header = Wrapping(Muted);
        AddChild(_header);
        _plotRows = new VBoxContainer();
        AddChild(_plotRows);
    }

    public void Refresh()
    {
        ClearChildren(_plotRows);

        var plantable = _game.PlantableActions();
        var unlocked = _game.UnlockedFarmingPlots;

        _header.Text = $"{unlocked} plot(s) open at Farming L{_game.ProfessionLevel("profession.farming")}. "
                     + "Crops grow on the world clock, so they keep growing while the game is closed.";

        for (var index = 0; index < unlocked; index++)
        {
            var plot = _game.FarmingPlotsView[index];
            var plotIndex = index;
            var row = Row();
            _plotRows.AddChild(row);
            row.AddChild(new Label { Text = $"Plot {index + 1}", CustomMinimumSize = new Vector2(70, 0) });

            if (plot.IsEmpty)
            {
                var picker = new OptionButton { CustomMinimumSize = new Vector2(220, 0) };
                foreach (var action in plantable)
                    picker.AddItem(action.Name);
                row.AddChild(picker);
                row.AddChild(MakeButton("Plant", () =>
                {
                    if (plantable.Count == 0)
                        return;
                    _game.PlantCrop(plotIndex, plantable[Mathf.Clamp(picker.Selected, 0, plantable.Count - 1)].Id);
                    Refresh();
                }, Accent));
                continue;
            }

            row.AddChild(new Label
            {
                Text = _game.ActionName(plot.PlantedActionId!),
                CustomMinimumSize = new Vector2(220, 0),
            });
            var bar = new ProgressBar { MinValue = 0, MaxValue = 100, CustomMinimumSize = new Vector2(140, 0), ShowPercentage = false };
            bar.Value = _game.PlotProgress(plotIndex) * 100.0;
            row.AddChild(bar);
            row.AddChild(MakeButton("Harvest", () =>
            {
                _game.HarvestPlot(plotIndex);
                Refresh();
            }, Positive));
        }
    }
}
