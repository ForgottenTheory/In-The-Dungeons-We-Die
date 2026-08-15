using System;
using Dungeons.Items;
using Dungeons.Realms;
using Godot;

namespace Dungeons.Game.Ui;

/// <summary>
/// Developer test console. Its job is to make every system observable and drivable
/// while there is no art — a themed, tabbed shell over <see cref="GameRoot"/> with a
/// persistent header (tick/sim + save) and an always-visible event log. Purely
/// presentation: it only calls <see cref="GameRoot"/> commands and reads its queries.
/// All controls are built in code so the .tscn stays trivial.
/// </summary>
public partial class MainMvpUI : Control
{
    // --- Palette (code-only theming, no assets) -----------------------------
    private static readonly Color Bg = new(0.11f, 0.12f, 0.15f);
    private static readonly Color PanelCol = new(0.16f, 0.18f, 0.22f);
    private static readonly Color CardCol = new(0.20f, 0.23f, 0.28f);
    private static readonly Color Accent = new(0.29f, 0.62f, 1.0f);
    private static readonly Color Positive = new(0.30f, 0.72f, 0.36f);
    private static readonly Color Danger = new(0.90f, 0.36f, 0.32f);
    private static readonly Color TextCol = new(0.84f, 0.86f, 0.90f);
    private static readonly Color Muted = new(0.55f, 0.58f, 0.64f);
    private static readonly Color Border = new(0.28f, 0.32f, 0.38f);

    private GameRoot _game = null!;
    private TabContainer _tabs = null!;
    private RichTextLabel _log = null!;
    private Label _statusLabel = null!;
    private Label _characterLabel = null!;
    private Label _professionSummaryLabel = null!;
    private Label _passiveStatusLabel = null!;
    private Label _inventoryLabel = null!;
    private Label _craftingLabel = null!;
    private Label _craftingStashLabel = null!;
    private Label _combatLabel = null!;
    private Label _realmLabel = null!;
    private VBoxContainer _realmControls = null!;
    private VBoxContainer _equipmentControls = null!;
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
        _game.DiscoveryChanged += RefreshCrafting;
        _game.CombatChanged += RefreshCombat;
        _game.RealmChanged += RefreshRealm;

        _game.ReportStatus();
        RefreshCharacter();
        RefreshProfessionsAndInventory();
        RefreshRunButton();
        RefreshCombat();
        RefreshRealm();
    }

    public override void _ExitTree()
    {
        if (_game is null)
            return;
        _game.LogEmitted -= AppendLog;
        _game.CharacterChanged -= RefreshCharacter;
        _game.InventoryChanged -= RefreshProfessionsAndInventory;
        _game.RunningChanged -= RefreshRunButton;
        _game.DiscoveryChanged -= RefreshCrafting;
        _game.CombatChanged -= RefreshCombat;
        _game.RealmChanged -= RefreshRealm;
    }

    public override void _Process(double delta)
    {
        // Sweep the active-timing indicator 0 → 100 → 0.
        _timingPhase = (_timingPhase + (delta * 0.6)) % 1.0;
        var t = _timingPhase < 0.5 ? _timingPhase * 2.0 : 2.0 - (_timingPhase * 2.0);
        _timingBar.Value = t * 100.0;

        _statusLabel.Text = $"Tick {_game.CurrentTick}    Sim {(_game.IsRunning ? "▶ RUNNING" : "❚❚ paused")} @ {GameRoot.TicksPerSecond}/s";

        _passiveBar.Value = _game.PassiveProgress * 100.0;
        _passiveStatusLabel.Text = _game.IsPassiveRunning
            ? $"Passive: {_game.CurrentPassiveActionId}"
            : "Passive: (idle)";

        if (_game.IsCombatActive)
            RefreshCombat(); // telegraph countdowns tick down each frame
    }

    // --- Layout -------------------------------------------------------------

    private void BuildLayout()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = BuildTheme();

        var bg = new ColorRect { Color = Bg, MouseFilter = MouseFilterEnum.Ignore };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 10);
        AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        root.AddChild(BuildHeader());

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 10);
        root.AddChild(body);

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddChild(_tabs);
        body.AddChild(BuildLogPanel());

        BuildCharacterSection(MakeTab("Character"));
        BuildEquipmentSection(MakeTab("Equipment"));
        BuildProfessionSection(MakeTab("Professions"));
        BuildCraftingSection(MakeTab("Crafting"));
        BuildRealmSection(MakeTab("Realm"));
        BuildCombatSection(MakeTab("Combat"));
        BuildInventorySection(MakeTab("Inventory"));
    }

    private Control BuildHeader()
    {
        var panel = new PanelContainer();
        var row = Row(8);
        panel.AddChild(row);

        var title = new Label { Text = "IN THE DUNGEONS WE DIE" };
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", Accent);
        row.AddChild(title);

        _statusLabel = new Label
        {
            Text = "…",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        _statusLabel.AddThemeColorOverride("font_color", Muted);
        row.AddChild(_statusLabel);

        _runButton = MakeButton("Play", ToggleRun, Accent);
        row.AddChild(_runButton);
        row.AddChild(MakeButton("Advance 50", () => _game.AdvanceTick(50)));
        row.AddChild(MakeButton("Save", () => _game.SaveGame(), Positive));
        row.AddChild(MakeButton("Load", () => _game.LoadGame()));
        return panel;
    }

    private Control BuildLogPanel()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(340, 0) };
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        panel.AddChild(col);

        var head = Row(8);
        col.AddChild(head);
        var logTitle = SectionTitle("Event Log");
        logTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(logTitle);
        head.AddChild(MakeButton("Clear", () => _log.Clear()));

        _log = new RichTextLabel
        {
            ScrollFollowing = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        col.AddChild(_log);
        return panel;
    }

    /// <summary>Adds a scrollable tab page and returns the VBox its content goes into.</summary>
    private VBoxContainer MakeTab(string name)
    {
        var page = new MarginContainer { Name = name };
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            page.AddThemeConstantOverride(side, 10);

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(col);
        page.AddChild(scroll);
        _tabs.AddChild(page);
        return col;
    }

    // --- Sections -----------------------------------------------------------

    private void BuildCharacterSection(VBoxContainer root)
    {
        root.AddChild(SectionTitle("Character"));
        _characterLabel = new Label { Text = "…" };
        root.AddChild(Card(_characterLabel));

        var buttons = Row();
        root.AddChild(buttons);
        buttons.AddChild(MakeButton("Damage 40%", () => _game.DamageCharacterPercent(0.4), Danger));
        buttons.AddChild(MakeButton("Heal Full", () => _game.HealCharacterFull(), Positive));
        buttons.AddChild(MakeButton("Cycle Suffix", () => _game.CycleSuffix()));
    }

    private void BuildEquipmentSection(VBoxContainer root)
    {
        root.AddChild(SectionTitle("Equipment"));

        _equipmentControls = new VBoxContainer();
        _equipmentControls.AddThemeConstantOverride("separation", 4);
        root.AddChild(Card(_equipmentControls));

        var grant = Row();
        root.AddChild(grant);
        grant.AddChild(new Label { Text = "Grant to stash:" });
        foreach (var gear in _game.EquipmentCatalog)
        {
            var id = gear.Id;
            grant.AddChild(MakeButton(gear.Name, () => _game.GrantToStash(id)));
        }
    }

    private void BuildProfessionSection(VBoxContainer root)
    {
        root.AddChild(SectionTitle("Professions"));
        _professionSummaryLabel = new Label();
        root.AddChild(Card(_professionSummaryLabel));

        var timingRow = Row();
        root.AddChild(timingRow);
        timingRow.AddChild(new Label { Text = "Active timing (aim for the middle):" });
        _timingBar = new ProgressBar { MinValue = 0, MaxValue = 100, CustomMinimumSize = new Vector2(200, 0), ShowPercentage = false };
        timingRow.AddChild(_timingBar);

        foreach (var action in _game.Actions)
        {
            var row = Row();
            root.AddChild(row);
            var actionId = action.Id;
            row.AddChild(new Label
            {
                Text = $"{action.Name} ({_game.ProfessionName(action.ProfessionId)})",
                CustomMinimumSize = new Vector2(240, 0),
            });
            row.AddChild(MakeButton("Passive", () => _game.StartPassive(actionId)));
            row.AddChild(MakeButton("Active", () => _game.ActiveAttempt(actionId, CurrentTimingPerformance()), Accent));
        }

        var passiveRow = Row();
        root.AddChild(passiveRow);
        _passiveStatusLabel = new Label { Text = "Passive: (idle)", CustomMinimumSize = new Vector2(240, 0) };
        passiveRow.AddChild(_passiveStatusLabel);
        _passiveBar = new ProgressBar { MinValue = 0, MaxValue = 100, CustomMinimumSize = new Vector2(200, 0), ShowPercentage = false };
        passiveRow.AddChild(_passiveBar);
        passiveRow.AddChild(MakeButton("Stop", () => _game.StopPassive(), Danger));
    }

    private void BuildCraftingSection(VBoxContainer root)
    {
        root.AddChild(SectionTitle("Crafting"));
        _craftingLabel = new Label();
        root.AddChild(Card(_craftingLabel));

        var buttons = new VBoxContainer();
        buttons.AddThemeConstantOverride("separation", 4);
        root.AddChild(buttons);
        buttons.AddChild(MakeButton("Experiment: Iron Ingot + Oak Bark", () => _game.ExperimentBarkbound(), Accent));
        buttons.AddChild(MakeButton("Brew Healing Salve (2 Sageleaf)", () => _game.BrewHealingSalve()));
        buttons.AddChild(MakeButton("Grant Craft Test Mats", () => _game.GrantCraftTestMaterials(), Positive));

        root.AddChild(new HSeparator());
        var stashHead = new Label { Text = "Materials on hand (experiment inputs):" };
        stashHead.AddThemeColorOverride("font_color", Muted);
        root.AddChild(stashHead);
        _craftingStashLabel = new Label();
        root.AddChild(Card(_craftingStashLabel));
    }

    private void BuildRealmSection(VBoxContainer root)
    {
        root.AddChild(SectionTitle("Realm"));
        _realmLabel = new Label();
        root.AddChild(Card(_realmLabel));

        _realmControls = new VBoxContainer();
        _realmControls.AddThemeConstantOverride("separation", 4);
        root.AddChild(_realmControls);
    }

    private void BuildCombatSection(VBoxContainer root)
    {
        root.AddChild(SectionTitle("Combat"));
        _combatLabel = new Label();
        root.AddChild(Card(_combatLabel));

        var startRow = Row();
        root.AddChild(startRow);
        startRow.AddChild(new Label { Text = "Start fight:" });
        foreach (var actor in _game.EnemyActors)
        {
            var actorId = actor.Id;
            startRow.AddChild(MakeButton(actor.Name, () => _game.StartCombat(actorId), Danger));
        }

        var actionRow = Row();
        root.AddChild(actionRow);
        actionRow.AddChild(MakeButton("Attack", () => _game.CombatAttack(), Accent));
        actionRow.AddChild(MakeButton("Block", () => _game.CombatBlock()));
        actionRow.AddChild(MakeButton("Dodge", () => _game.CombatDodge()));
        actionRow.AddChild(MakeButton("Use Salve", () => _game.CombatUseConsumable("consumable.healing_salve"), Positive));
        actionRow.AddChild(MakeButton("Wait", () => _game.CombatWait()));
    }

    private void BuildInventorySection(VBoxContainer root)
    {
        root.AddChild(SectionTitle("Inventory"));
        _inventoryLabel = new Label { Text = "…" };
        root.AddChild(Card(_inventoryLabel));
    }

    // --- Dynamic control groups --------------------------------------------

    private void RebuildEquipmentControls()
    {
        foreach (var child in _equipmentControls.GetChildren())
        {
            _equipmentControls.RemoveChild(child);
            child.QueueFree();
        }

        AddSlotRow(EquipmentSlot.Weapon, _game.EquippedWeapon, _game.EquippedWeaponSummary());
        AddSlotRow(EquipmentSlot.Armor, _game.EquippedArmor, _game.EquippedArmorSummary());

        var stashHead = new Label { Text = "In stash:" };
        stashHead.AddThemeColorOverride("font_color", Muted);
        _equipmentControls.AddChild(stashHead);

        var stash = _game.StashEquipment;
        if (stash.Count == 0)
        {
            _equipmentControls.AddChild(new Label { Text = "  (no unequipped gear)" });
            return;
        }

        foreach (var instance in stash)
        {
            var row = Row();
            _equipmentControls.AddChild(row);
            row.AddChild(new Label { Text = _game.InstanceLabel(instance), CustomMinimumSize = new Vector2(340, 0) });
            var id = instance.InstanceId;
            row.AddChild(MakeButton("Equip", () => _game.EquipFromStash(id), Accent));
        }
    }

    private void AddSlotRow(EquipmentSlot slot, ItemInstance? equipped, string summary)
    {
        var row = Row();
        _equipmentControls.AddChild(row);
        var name = equipped?.DisplayName ?? "— (empty)";
        row.AddChild(new Label { Text = $"{slot}: {name}  →  {summary}", CustomMinimumSize = new Vector2(420, 0) });
        if (equipped is not null)
            row.AddChild(MakeButton("Unequip", () => _game.UnequipToStash(slot), Danger));
    }

    private void RebuildRealmControls()
    {
        foreach (var child in _realmControls.GetChildren())
        {
            _realmControls.RemoveChild(child);
            child.QueueFree();
        }

        if (!_game.InRealm)
        {
            foreach (var realm in _game.Realms)
            {
                var id = realm.Id;
                _realmControls.AddChild(MakeButton($"Enter {realm.Name}", () => _game.EnterRealm(id), Accent));
            }

            return;
        }

        if (_game.RealmBusy)
        {
            _realmControls.AddChild(new Label { Text = "In combat — act here (telegraphs show in the Combat tab):" });
            var fightRow = Row();
            _realmControls.AddChild(fightRow);
            fightRow.AddChild(MakeButton("Attack", () => _game.CombatAttack(), Accent));
            fightRow.AddChild(MakeButton("Block", () => _game.CombatBlock()));
            fightRow.AddChild(MakeButton("Dodge", () => _game.CombatDodge()));
            fightRow.AddChild(MakeButton("Use Salve", () => _game.CombatUseConsumable("consumable.healing_salve"), Positive));
            return;
        }

        var actionLabel = _game.RealmActionLabel();
        if (actionLabel is not null)
            _realmControls.AddChild(MakeButton(actionLabel, () => _game.RealmAction(), Accent));
        if (_game.RealmCanDescend)
            _realmControls.AddChild(MakeButton("▼ Go Deeper", () => _game.RealmGoDeeper()));
        if (_game.RealmCanExtract)
            _realmControls.AddChild(MakeButton("Extract", () => _game.RealmExtract(), Positive));

        var run = _game.Run;
        if (run is null)
            return;
        foreach (var destination in run.Destinations())
        {
            var id = destination.Id;
            _realmControls.AddChild(MakeButton($"→ {destination.Name}", () => _game.RealmTravel(id)));
        }
    }

    // --- Refresh ------------------------------------------------------------

    private void ToggleRun() => _game.SetRunning(!_game.IsRunning);

    private void RefreshRunButton() => _runButton.Text = _game.IsRunning ? "Pause" : "Play";

    private void RefreshCharacter()
    {
        _characterLabel.Text = _game.CharacterReport();
        RebuildEquipmentControls(); // equipped slots changed
    }

    private void RefreshProfessionsAndInventory()
    {
        _professionSummaryLabel.Text = _game.ProfessionSummary();
        _inventoryLabel.Text = _game.InventoryReport();
        _craftingStashLabel.Text = _game.InventoryReport();
        RebuildEquipmentControls(); // stash equipment may have changed
        RefreshCrafting(); // herblore level shown in the crafting requirement can change
    }

    private void RefreshCrafting() => _craftingLabel.Text = _game.CraftingReport();

    private void RefreshCombat()
    {
        _combatLabel.Text = _game.CombatReport();
        if (!_game.InRealm)
            return;

        _realmLabel.Text = _game.RealmReport(); // keep party HP live during a realm fight
        if (!_game.RealmBusy)
            RebuildRealmControls(); // combat just ended → surface travel/extract options again
    }

    private void RefreshRealm()
    {
        _realmLabel.Text = _game.RealmReport();
        RebuildRealmControls();
    }

    private void AppendLog(string message) => _log.AddText(message + "\n");

    // --- Theming helpers ----------------------------------------------------

    /// <summary>1.0 when the active-timing sweep is dead-centre, falling to 0 at the edges.</summary>
    private double CurrentTimingPerformance() =>
        Dungeons.Professions.ProfessionTuning.TimingPerformance(_timingBar.Value / 100.0);

    private static Label SectionTitle(string text)
    {
        var label = new Label { Text = text.ToUpperInvariant() };
        label.AddThemeFontSizeOverride("font_size", 15);
        label.AddThemeColorOverride("font_color", Accent);
        return label;
    }

    private static HBoxContainer Row(int separation = 8)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", separation);
        return row;
    }

    private static PanelContainer Card(Control inner)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Flat(CardCol, 6, 10, Border, 1));
        panel.AddChild(inner);
        return panel;
    }

    private static Button MakeButton(string text, Action onPressed, Color? accentText = null)
    {
        var button = new Button { Text = text };
        button.Pressed += onPressed;
        if (accentText.HasValue)
            button.AddThemeColorOverride("font_color", accentText.Value);
        return button;
    }

    private static StyleBoxFlat Flat(Color bg, int radius, int pad, Color? border = null, int borderWidth = 0)
    {
        var box = new StyleBoxFlat { BgColor = bg };
        box.SetCornerRadiusAll(radius);
        box.SetContentMarginAll(pad);
        if (border.HasValue && borderWidth > 0)
        {
            box.BorderColor = border.Value;
            box.SetBorderWidthAll(borderWidth);
        }

        return box;
    }

    private static Theme BuildTheme()
    {
        var theme = new Theme();

        theme.SetColor("font_color", "Label", TextCol);
        theme.SetFontSize("font_size", "Label", 13);

        theme.SetStylebox("normal", "Button", Flat(PanelCol, 5, 8, Border, 1));
        theme.SetStylebox("hover", "Button", Flat(CardCol, 5, 8, Accent, 1));
        theme.SetStylebox("pressed", "Button", Flat(Accent, 5, 8));
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        theme.SetColor("font_color", "Button", TextCol);
        theme.SetColor("font_hover_color", "Button", new Color(1, 1, 1));
        theme.SetColor("font_pressed_color", "Button", new Color(1, 1, 1));
        theme.SetFontSize("font_size", "Button", 13);

        theme.SetStylebox("panel", "PanelContainer", Flat(PanelCol, 8, 10, Border, 1));

        theme.SetStylebox("panel", "TabContainer", Flat(PanelCol, 8, 10, Border, 1));
        theme.SetStylebox("tab_selected", "TabContainer", Flat(Accent, 5, 8));
        theme.SetStylebox("tab_unselected", "TabContainer", Flat(CardCol, 5, 8));
        theme.SetStylebox("tab_hovered", "TabContainer", Flat(CardCol, 5, 8, Accent, 1));
        theme.SetStylebox("tabbar_background", "TabContainer", new StyleBoxEmpty());
        theme.SetColor("font_selected_color", "TabContainer", new Color(1, 1, 1));
        theme.SetColor("font_unselected_color", "TabContainer", Muted);
        theme.SetColor("font_hovered_color", "TabContainer", TextCol);

        theme.SetStylebox("background", "ProgressBar", Flat(Bg, 4, 0));
        theme.SetStylebox("fill", "ProgressBar", Flat(Accent, 4, 0));
        theme.SetColor("font_color", "ProgressBar", TextCol);

        theme.SetColor("default_color", "RichTextLabel", TextCol);
        theme.SetFontSize("normal_font_size", "RichTextLabel", 12);
        return theme;
    }
}
