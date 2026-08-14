using Godot;

namespace Dungeons.Game.Ui;

/// <summary>
/// Milestone 1–2 "wiring-proof" developer shell. It is intentionally ugly: its job
/// is to prove the Godot → GameRoot → Core path end-to-end and to make character
/// composition observable. Real screens arrive in later milestones. Controls are
/// built in code to keep the .tscn trivial and avoid brittle node paths.
/// </summary>
public partial class MainMvpUI : Control
{
    private GameRoot _game = null!;
    private RichTextLabel _log = null!;
    private Label _statusLabel = null!;
    private Label _characterLabel = null!;

    public override void _Ready()
    {
        _game = GetNode<GameRoot>("/root/GameRoot");

        BuildLayout();

        _game.LogEmitted += AppendLog;
        _game.CharacterChanged += RefreshCharacter;
        _game.ReportStatus();
        RefreshStatus();
        RefreshCharacter();
    }

    public override void _ExitTree()
    {
        if (_game is null)
            return;
        _game.LogEmitted -= AppendLog;
        _game.CharacterChanged -= RefreshCharacter;
    }

    private void BuildLayout()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // A ScrollContainer keeps every control reachable even when the window is
        // shorter than the content. Horizontal scrolling is disabled so children
        // are stretched to the window width instead.
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(scroll);

        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 12);
        scroll.AddChild(margin);

        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var title = new Label { Text = "In The Dungeons We Die — Debug Shell (M1–M2)" };
        title.AddThemeFontSizeOverride("font_size", 20);
        root.AddChild(title);

        _statusLabel = new Label();
        root.AddChild(_statusLabel);

        var simButtons = new HBoxContainer();
        simButtons.AddThemeConstantOverride("separation", 8);
        root.AddChild(simButtons);
        simButtons.AddChild(MakeButton("Advance Tick", () => Advance(1)));
        simButtons.AddChild(MakeButton("Advance 10 Ticks", () => Advance(10)));
        simButtons.AddChild(MakeButton("Save + Reload", () =>
        {
            _game.SaveAndReload();
            RefreshStatus();
        }));

        root.AddChild(new HSeparator());

        var characterHeader = new Label { Text = "CHARACTER" };
        characterHeader.AddThemeFontSizeOverride("font_size", 16);
        root.AddChild(characterHeader);

        var characterButtons = new HBoxContainer();
        characterButtons.AddThemeConstantOverride("separation", 8);
        root.AddChild(characterButtons);
        characterButtons.AddChild(MakeButton("Damage 40%", () => _game.DamageCharacterPercent(0.4)));
        characterButtons.AddChild(MakeButton("Heal Full", () => _game.HealCharacterFull()));
        characterButtons.AddChild(MakeButton("Cycle Suffix", () => _game.CycleSuffix()));

        _characterLabel = new Label { Text = "…" };
        root.AddChild(_characterLabel);

        root.AddChild(new HSeparator());

        _log = new RichTextLabel
        {
            ScrollFollowing = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 220),
        };
        root.AddChild(_log);
    }

    private void Advance(long ticks)
    {
        _game.AdvanceTick(ticks);
        RefreshStatus();
    }

    private void RefreshStatus() =>
        _statusLabel.Text = $"Current tick: {_game.CurrentTick}    |    Materials loaded: {_game.MaterialCount}";

    private void RefreshCharacter() => _characterLabel.Text = _game.CharacterReport();

    private void AppendLog(string message) => _log.AppendText(message + "\n");

    private static Button MakeButton(string text, System.Action onPressed)
    {
        var button = new Button { Text = text };
        button.Pressed += onPressed;
        return button;
    }
}
