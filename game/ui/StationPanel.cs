using System;
using System.Collections.Generic;
using Dungeons.Hideout;
using Godot;
using static Dungeons.Game.Ui.ConsoleTheme;

namespace Dungeons.Game.Ui;

/// <summary>
/// One Hideout station's page: what you can train here, what the bench here can transform, and
/// what can be assembled here.
///
/// <para>The page is <b>composed from what the station's definition routes to</b>, not from a
/// per-station layout — a station with no crafting actions simply has no bench, and the three
/// bespoke panels appear because of which profession is hosted, not because of a flag. That is
/// what keeps twenty destinations to one class.</para>
/// </summary>
public partial class StationPanel : VBoxContainer
{
    private readonly List<ProfessionLadderPanel> _ladders = new();
    private VerbBenchPanel? _verbBench;
    private IdentityForgePanel? _identityForge;
    private CraftingInteractionsPanel? _interactions;
    private FarmingPlotsPanel? _plots;
    private TrainingCoursePanel? _course;
    private AssayBenchPanel? _assay;

    /// <summary>Professions whose station gets a panel of its own on top of the ladder,
    /// because the profession is a system rather than a list of actions.</summary>
    private const string FarmingProfessionId = "profession.farming";
    private const string AgilityProfessionId = "profession.agility";
    private const string AssayProfessionId = "profession.assay";

    public StationPanel(GameRoot game, StationDefinition station, Func<double> activeTimingPerformance)
    {
        AddThemeConstantOverride("separation", 10);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var title = new Label { Text = station.Name };
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", Accent);
        AddChild(title);

        var description = Wrapping(Muted);
        description.Text = station.Description;
        AddChild(description);

        foreach (var professionId in station.Professions)
        {
            if (game.ProfessionById(professionId) is not { } profession)
                continue;

            AddChild(new HSeparator());
            var ladder = new ProfessionLadderPanel(game, profession, activeTimingPerformance);
            _ladders.Add(ladder);
            AddChild(ladder);

            if (profession.Id == FarmingProfessionId)
                AddChild(_plots = new FarmingPlotsPanel(game));
            if (profession.Id == AgilityProfessionId)
                AddChild(_course = new TrainingCoursePanel(game));
            if (profession.Id == AssayProfessionId)
                AddChild(_assay = new AssayBenchPanel(game));
        }

        if (game.InteractionsAt(station) is { Count: > 0 } interactions)
        {
            AddChild(new HSeparator());
            AddChild(_interactions = new CraftingInteractionsPanel(game, interactions));
        }

        if (station.VerbActions.Count > 0)
        {
            AddChild(new HSeparator());
            AddChild(_verbBench = new VerbBenchPanel(game, game.VerbActionsAt(station.Id)));
        }

        if (station.HasAssembly)
        {
            AddChild(new HSeparator());
            AddChild(_identityForge = new IdentityForgePanel(game));
        }
    }

    /// <summary>Re-reads everything on the page. Called when the page is shown and whenever the
    /// inventory changes — a level-up unlocks rungs, a harvest empties a plot, and every picker
    /// is a snapshot of what was on hand when it was filled.</summary>
    public void Refresh()
    {
        foreach (var ladder in _ladders)
            ladder.Refresh();

        _plots?.Refresh();
        _course?.Refresh();
        _assay?.Refresh();
        _interactions?.Refresh();
        _verbBench?.Refresh();
        _identityForge?.Refresh();
    }
}
