using System;
using Dungeons.Professions;
using Godot;
using static Dungeons.Game.Ui.ConsoleTheme;

namespace Dungeons.Game.Ui;

/// <summary>
/// One profession's action ladder, at the station that trains it.
///
/// <para>Rungs are level-gated and shown locked rather than hidden, so the ladder doubles as
/// the profession's roadmap. Each unlocked rung offers the two modes the design keeps
/// structurally apart: <b>Passive</b> repeats automatically and never rolls for an opportunity,
/// <b>Active</b> scores the timing sweep and can surface a Discover → Pursue offer. The offer
/// itself lands on the Hideout's activity strip, which is why the timing performance is passed
/// in rather than owned here.</para>
/// </summary>
public partial class ProfessionLadderPanel : VBoxContainer
{
    private readonly GameRoot _game;
    private readonly ProfessionDefinition _profession;
    private readonly Func<double> _activeTimingPerformance;

    private Label _levelLabel = null!;
    private VBoxContainer _rungs = null!;

    public ProfessionLadderPanel(GameRoot game, ProfessionDefinition profession, Func<double> activeTimingPerformance)
    {
        _game = game;
        _profession = profession;
        _activeTimingPerformance = activeTimingPerformance;
        AddThemeConstantOverride("separation", 4);
        Build();
        Refresh();
    }

    private void Build()
    {
        AddChild(SectionTitle(_profession.Name));

        _levelLabel = Wrapping(Muted);
        AddChild(_levelLabel);

        _rungs = new VBoxContainer();
        AddChild(_rungs);
    }

    public void Refresh()
    {
        var level = _game.ProfessionLevel(_profession.Id);
        _levelLabel.Text = $"{_profession.Description}   ({_profession.Category}, L{level})";

        ClearChildren(_rungs);

        foreach (var action in _game.ActionsFor(_profession.Id))
        {
            var row = Row();
            _rungs.AddChild(row);

            var actionId = action.Id;
            var locked = level < action.RequiredLevel;
            var label = new Label
            {
                Text = $"L{action.RequiredLevel,-3} {action.Name}",
                CustomMinimumSize = new Vector2(260, 0),
            };
            if (locked)
                label.AddThemeColorOverride("font_color", Muted);
            row.AddChild(label);

            if (locked)
            {
                row.AddChild(new Label { Text = "locked", CustomMinimumSize = new Vector2(140, 0) });
                continue;
            }

            row.AddChild(MakeButton("Passive", () => _game.StartPassive(actionId)));
            row.AddChild(MakeButton("Active", () => _game.ActiveAttempt(actionId, _activeTimingPerformance()), Accent));

            // Mastery, and what it has bought. Until Phase 8 this number went up and did
            // nothing; showing it beside what it is currently worth is what makes repeating
            // one action legible as progression rather than as grinding.
            var mastery = new Label
            {
                Text = _game.MasteryReadout(actionId),
                CustomMinimumSize = new Vector2(300, 0),
            };
            mastery.AddThemeColorOverride("font_color", Muted);
            row.AddChild(mastery);

            if (action.Opportunities.Count > 0)
            {
                var marker = new Label { Text = "◆ has an opportunity" };
                marker.AddThemeColorOverride("font_color", Positive);
                row.AddChild(marker);
            }

            if (action.SuccessChance < 1.0)
            {
                var chance = new Label { Text = $"{action.SuccessChance:P0} to land" };
                chance.AddThemeColorOverride("font_color", Muted);
                row.AddChild(chance);
            }
        }
    }
}
