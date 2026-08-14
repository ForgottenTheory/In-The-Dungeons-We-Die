using System;
using Godot;

namespace Dungeons.Game.Ui;

/// <summary>
/// Milestone 1–3 "wiring-proof" developer shell. Intentionally ugly: its job is to
/// prove the Godot → GameRoot → Core path and make the simulation observable — the
/// tick clock, character composition, and profession passive/active gathering. Real
/// screens arrive in later milestones. Controls are built in code to keep the .tscn
/// trivial. Only animating values are polled in _Process; the rest is event-driven.
/// </summary>
public partial class MainMvpUI : Control
{
    private GameRoot _game = null!;
    private RichTextLabel _log = null!;
    private Label _statusLabel = null!;
    private Label _characterLabel = null!;
    private Label _professionSummaryLabel = null!;
    private Label _passiveStatusLabel = null!;
    private Label _inventoryLabel = null!;
    private Button _runButton = null!;
    private ProgressBar _timingBar = null!;
    private ProgressBar _passiveBar = null!;

    private double _timingPhase;

    public override void _Ready()
    {
        _game = GetNode<GameRoot>("/root/GameRoot");

        BuildLayout();

        _game.LogEmitted += AppendLog;
        _game.CharacterChanged += RefreshCharacter;
        _game.InventoryChanged += RefreshProfessionsAndInventory;
        _game.RunningChanged += RefreshRunButton;

        _game.ReportStatus();
        RefreshCharacter();
        RefreshProfessionsAndInventory();
        RefreshRunButton();
    }

    public override void _ExitTree()
    {
        if (_game is null)
            return;
        _game.LogEmitted -= AppendLog;
        _game.CharacterChanged -= RefreshCharacter;
        _game.InventoryChanged -= RefreshProfessionsAndInventory;
        _game.RunningChanged -= RefreshRunButton;
    }

    public override void _Process(double delta)
    {
        // Sweep the active-timing indicator 0 → 100 → 0.
        _timingPhase = (_timingPhase + (delta * 0.6)) % 1.0;
        var t = _timingPhase < 0.5 ? _timingPhase * 2.0 : 2.0 - (_timingPhase * 2.0);
        _timingBar.Value = t * 100.0;

        _statusLabel.Text = $"Tick {_game.CurrentTick}   |   Sim {(_game.IsRunning ? "RUNNING" : "paused")} @ {GameRoot.TicksPerSecond}/s";

        _passiveBar.Value = _game.PassiveProgress * 100.0;
        _passiveStatusLabel.Text = _game.IsPassiveRunning
            ? $"Passive: {_game.CurrentPassiveActionId}"
            : "Passive: (idle)";
    }

    private void BuildLayout()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

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

        var title = new Label { Text = "In The Dungeons We Die — Debug Shell (M1–M3)" };
        title.AddThemeFontSizeOverride("font_size", 20);
        root.AddChild(title);

        _statusLabel = new Label();
        root.AddChild(_statusLabel);

        var simButtons = new HBoxContainer();
        simButtons.AddThemeConstantOverride("separation", 8);
        root.AddChild(simButtons);
        _runButton = MakeButton("Play", ToggleRun);
        simButtons.AddChild(_runButton);
        simButtons.AddChild(MakeButton("Advance 50 Ticks", () => _game.AdvanceTick(50)));
        simButtons.AddChild(MakeButton("Save + Reload", () => _game.SaveAndReload()));

        BuildCharacterSection(root);
        BuildProfessionSection(root);
        BuildInventorySection(root);

        root.AddChild(new HSeparator());
        _log = new RichTextLabel
        {
            ScrollFollowing = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 200),
        };
        root.AddChild(_log);
    }

    private void BuildCharacterSection(VBoxContainer root)
    {
        root.AddChild(new HSeparator());
        root.AddChild(Header("CHARACTER"));

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        root.AddChild(buttons);
        buttons.AddChild(MakeButton("Damage 40%", () => _game.DamageCharacterPercent(0.4)));
        buttons.AddChild(MakeButton("Heal Full", () => _game.HealCharacterFull()));
        buttons.AddChild(MakeButton("Cycle Suffix", () => _game.CycleSuffix()));

        _characterLabel = new Label { Text = "…" };
        root.AddChild(_characterLabel);
    }

    private void BuildProfessionSection(VBoxContainer root)
    {
        root.AddChild(new HSeparator());
        root.AddChild(Header("PROFESSIONS"));

        _professionSummaryLabel = new Label();
        root.AddChild(_professionSummaryLabel);

        var timingRow = new HBoxContainer();
        timingRow.AddThemeConstantOverride("separation", 8);
        root.AddChild(timingRow);
        timingRow.AddChild(new Label { Text = "Active timing (aim for the middle):" });
        _timingBar = new ProgressBar { MinValue = 0, MaxValue = 100, CustomMinimumSize = new Vector2(200, 0), ShowPercentage = false };
        timingRow.AddChild(_timingBar);

        foreach (var action in _game.Actions)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            root.AddChild(row);

            var actionId = action.Id;
            row.AddChild(new Label
            {
                Text = $"{action.Name} ({_game.ProfessionName(action.ProfessionId)})",
                CustomMinimumSize = new Vector2(240, 0),
            });
            row.AddChild(MakeButton("Passive", () => _game.StartPassive(actionId)));
            row.AddChild(MakeButton("Active", () => _game.ActiveAttempt(actionId, CurrentTimingPerformance())));
        }

        var passiveRow = new HBoxContainer();
        passiveRow.AddThemeConstantOverride("separation", 8);
        root.AddChild(passiveRow);
        _passiveStatusLabel = new Label { Text = "Passive: (idle)", CustomMinimumSize = new Vector2(240, 0) };
        passiveRow.AddChild(_passiveStatusLabel);
        _passiveBar = new ProgressBar { MinValue = 0, MaxValue = 100, CustomMinimumSize = new Vector2(200, 0), ShowPercentage = false };
        passiveRow.AddChild(_passiveBar);
        passiveRow.AddChild(MakeButton("Stop Passive", () => _game.StopPassive()));
    }

    private void BuildInventorySection(VBoxContainer root)
    {
        root.AddChild(new HSeparator());
        root.AddChild(Header("INVENTORY"));
        _inventoryLabel = new Label { Text = "…" };
        root.AddChild(_inventoryLabel);
    }

    private double CurrentTimingPerformance()
    {
        // 1.0 when the sweep is dead-centre, falling to 0 at the edges.
        var position = _timingBar.Value / 100.0;
        return Math.Clamp(1.0 - (Math.Abs(position - 0.5) * 2.0), 0.0, 1.0);
    }

    private void ToggleRun() => _game.SetRunning(!_game.IsRunning);

    private void RefreshRunButton() => _runButton.Text = _game.IsRunning ? "Pause" : "Play";

    private void RefreshCharacter() => _characterLabel.Text = _game.CharacterReport();

    private void RefreshProfessionsAndInventory()
    {
        _professionSummaryLabel.Text = _game.ProfessionSummary();
        _inventoryLabel.Text = _game.InventoryReport();
    }

    private void AppendLog(string message) => _log.AppendText(message + "\n");

    private static Label Header(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 16);
        return label;
    }

    private static Button MakeButton(string text, Action onPressed)
    {
        var button = new Button { Text = text };
        button.Pressed += onPressed;
        return button;
    }
}
