using Dungeons.Professions;
using Dungeons.Presentation;
using Godot;
using static Dungeons.Game.Ui.ConsoleTheme;

namespace Dungeons.Game.Ui;

/// <summary>
/// "While you were away" — what the last absence earned, at the top of the Hideout.
///
/// <para><b>Why it is a panel and not three log lines.</b> The payout has worked since P4; what
/// the player got back was a few sentences scrolling past in a console shared with combat traces
/// and crafting output. For an idle game the return <em>is</em> the session's first beat, and it
/// has to be readable in one glance — otherwise the whole offline half is something the player
/// takes on trust rather than something they can see working.</para>
///
/// <para>Every word here comes from <see cref="AwayReadout"/>, which the console line also uses,
/// so the two can never describe the same absence differently (D30, CLAUDE.md rule 7). This
/// class decides layout and nothing else.</para>
/// </summary>
public partial class AwaySummaryPanel : PanelContainer
{
    private readonly GameRoot _game;
    private readonly VBoxContainer _body = new();
    private Label _headline = null!;

    public AwaySummaryPanel(GameRoot game)
    {
        _game = game;
        Visible = false;
        AddThemeStyleboxOverride("panel", Flat(CardColor, radius: 6, pad: 10, Border, borderWidth: 1));

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 4);
        AddChild(root);

        var head = Row();
        root.AddChild(head);

        _headline = new Label { Text = string.Empty, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _headline.AddThemeColorOverride("font_color", Accent);
        head.AddChild(_headline);
        head.AddChild(MakeButton("Dismiss", () => Visible = false));

        _body.AddThemeConstantOverride("separation", 2);
        root.AddChild(_body);

        _game.AwayReported += OnAwayReported;
    }

    /// <summary>GameRoot is an autoload and outlives this scene, so a subscription left behind
    /// would call Refresh on a freed node.</summary>
    public override void _ExitTree() => _game.AwayReported -= OnAwayReported;

    private void OnAwayReported(AwayReport report) => Refresh();

    /// <summary>Rebuilds from the last absence, and shows itself only if there is something to
    /// say. An empty "you were away" card is worse than none — it reads as a bug.</summary>
    public void Refresh()
    {
        var lines = _game.AwaySummaryLines();
        ClearChildren(_body);

        if (lines.Count == 0)
        {
            Visible = false;
            return;
        }

        _headline.Text = _game.AwaySummaryHeadline();

        foreach (var line in lines)
        {
            var row = Row();
            _body.AddChild(row);

            var heading = new Label { Text = line.Heading, CustomMinimumSize = new Vector2(220, 0) };
            heading.AddThemeColorOverride("font_color", Muted);
            row.AddChild(heading);

            var detail = Wrapping();
            detail.Text = line.Detail;
            detail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(detail);
        }

        Visible = true;
    }
}
