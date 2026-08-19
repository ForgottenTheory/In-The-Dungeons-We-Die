using System;
using System.Collections.Generic;
using System.Linq;
using Dungeons.Combat;
using Dungeons.Hideout;
using Dungeons.Items;
using Dungeons.Professions;
using Dungeons.Presentation;
using Godot;
using static Dungeons.Game.Ui.ConsoleTheme;

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
    private GameRoot _game = null!;
    private TabContainer _tabs = null!;
    private RichTextLabel _log = null!;
    private Label _statusLabel = null!;
    private Label _characterLabel = null!;
    private Label _inventoryLabel = null!;
    private Label _combatLabel = null!;
    private HBoxContainer _moveButtonsRow = null!;
    private Label _hitLogLabel = null!;
    private PanelContainer _hitLogCard = null!;
    private string _moveButtonsKey = string.Empty;
    private Label _realmLabel = null!;
    private VBoxContainer _realmControls = null!;
    private VBoxContainer _realmRunView = null!;
    private RealmPreparationPanel _preparation = null!;
    private int _realmTabIndex;
    private VBoxContainer _equipmentControls = null!;
    private VBoxContainer _techniqueControls = null!;
    private Button _runButton = null!;
    private ProgressBar _timingBar = null!;
    private ProgressBar _passiveBar = null!;

    // --- Hideout ------------------------------------------------------------
    private Label _professionSummaryLabel = null!;
    private Label _passiveStatusLabel = null!;
    private PanelContainer _opportunityCard = null!;
    private Label _opportunityTitle = null!;
    private Label _opportunityPrompt = null!;
    private Label _opportunityCost = null!;
    private VBoxContainer _stationIndex = null!;
    private VBoxContainer _stationPage = null!;

    /// <summary>Station pages are built on first visit and kept, so walking away from a
    /// half-assembled reagent chain and coming back does not throw the work away.</summary>
    private readonly Dictionary<string, StationPanel> _visitedStations = new();
    private StationPanel? _openStation;

    // --- Character Lab ------------------------------------------------------
    private OptionButton _basePicker = null!;
    private OptionButton _prefixPicker = null!;
    private OptionButton _suffixPicker = null!;
    private Label _labNameLabel = null!;
    private Label _labEngineLabel = null!;
    private Label _labMechanicLabel = null!;
    private Label _labDiffLabel = null!;
    private Label _labReportLabel = null!;

    private double _timingPhase;

    public override void _Ready()
    {
        _game = GetNode<GameRoot>("/root/GameRoot");

        BuildLayout();

        _game.LogEmitted += AppendLog;
        _game.CharacterChanged += RefreshCharacter;
        _game.InventoryChanged += RefreshHideoutAndInventory;
        _game.RunningChanged += RefreshRunButton;
        _game.DiscoveryChanged += RefreshOpenStation;
        _game.CombatChanged += RefreshCombat;
        _game.RealmChanged += RefreshRealm;
        _game.OpportunityOffered += ShowOpportunity;
        _game.LoadoutChanged += RefreshPreparation;

        _game.ReportStatus();
        RefreshCharacter();
        RefreshHideoutAndInventory();
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
        _game.InventoryChanged -= RefreshHideoutAndInventory;
        _game.RunningChanged -= RefreshRunButton;
        _game.DiscoveryChanged -= RefreshOpenStation;
        _game.CombatChanged -= RefreshCombat;
        _game.RealmChanged -= RefreshRealm;
        _game.OpportunityOffered -= ShowOpportunity;
        _game.LoadoutChanged -= RefreshPreparation;
        _tabs.TabChanged -= OnTabChanged;
    }

    public override void _Process(double delta)
    {
        // Sweep the active-timing indicator 0 → 100 → 0.
        _timingPhase = (_timingPhase + (delta * 0.6)) % 1.0;
        var sweep = _timingPhase < 0.5 ? _timingPhase * 2.0 : 2.0 - (_timingPhase * 2.0);
        _timingBar.Value = sweep * 100.0;

        _statusLabel.Text = $"Tick {_game.CurrentTick}    Sim {(_game.IsRunning ? "▶ RUNNING" : "❚❚ paused")} @ {GameRoot.TicksPerSecond}/s";

        // The pursuit bar borrows the passive bar: only one of the two can be in flight, and a
        // second bar in the same row would just be furniture.
        if (_game.IsPursuingOpportunity)
        {
            _passiveBar.Value = _game.PursuitProgress() * 100.0;
            _passiveStatusLabel.Text = "Pursuing an opportunity…";
        }
        else
        {
            _passiveBar.Value = _game.PassiveProgress * 100.0;
            _passiveStatusLabel.Text = _game.IsPassiveRunning
                ? $"Passive: {_game.ActionName(_game.CurrentPassiveActionId!)}"
                : "Passive: (idle)";
        }

        if (_game.IsCombatActive)
            RefreshCombat(); // telegraph countdowns tick down each frame
    }

    // --- Layout -------------------------------------------------------------

    private void BuildLayout()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = ConsoleTheme.Build();

        var background = new ColorRect { Color = BackgroundColor, MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

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
        BuildCharacterLabSection(MakeTab("Char Lab"));
        BuildEquipmentSection(MakeTab("Equipment"));
        BuildHideoutSection(MakeTab("Hideout"));

        _realmTabIndex = _tabs.GetChildCount();
        BuildRealmSection(MakeTab("Realm"));

        BuildCombatSection(MakeTab("Combat"));
        BuildInventorySection(MakeTab("Inventory"));

        // The preparation screen is left stale while it is off screen, so opening the tab is
        // what re-reads it. See RefreshPreparation for why it is not simply refreshed on every
        // inventory change.
        _tabs.TabChanged += OnTabChanged;
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

    /// <summary>
    /// The Hideout: everything you do between runs, reached the way the fiction describes it —
    /// <b>choose a station, then use what that station is for</b>.
    ///
    /// <para>The tab has exactly two layers. The <b>activity strip</b> is fixed furniture,
    /// because only one thing can be running at a time and the Discover → Pursue offer must
    /// appear on the same page as the Active button that raised it. Below it, the <b>station
    /// index</b> and one <b>station page</b> swap places.</para>
    ///
    /// <para>This replaced a monolithic Crafting tab that put eight crafting actions and every
    /// blueprint on one screen regardless of where any of it belonged.</para>
    /// </summary>
    private void BuildHideoutSection(VBoxContainer root)
    {
        root.AddChild(SectionTitle("Hideout"));

        // --- The activity strip: what is running, and what is on offer ----------
        var passiveRow = Row();
        root.AddChild(passiveRow);
        _passiveStatusLabel = new Label { Text = "Passive: (idle)", CustomMinimumSize = new Vector2(260, 0) };
        passiveRow.AddChild(_passiveStatusLabel);
        _passiveBar = new ProgressBar { MinValue = 0, MaxValue = 100, CustomMinimumSize = new Vector2(180, 0), ShowPercentage = false };
        passiveRow.AddChild(_passiveBar);
        passiveRow.AddChild(MakeButton("Stop", () => _game.StopPassive(), Danger));

        passiveRow.AddChild(new Label { Text = "   Active timing:" });
        _timingBar = new ProgressBar { MinValue = 0, MaxValue = 100, CustomMinimumSize = new Vector2(180, 0), ShowPercentage = false };
        passiveRow.AddChild(_timingBar);

        var offlineNote = Wrapping(Muted);
        offlineNote.Text = "Whatever is running when you save keeps going while the game is closed — same rate, "
                          + "no opportunities. Levelling never needs you at the keyboard. Active play aims for the "
                          + "middle of the sweep.";
        root.AddChild(offlineNote);

        _opportunityCard = Card(BuildOpportunityPanel());
        _opportunityCard.Visible = false;
        root.AddChild(_opportunityCard);

        root.AddChild(new HSeparator());

        // --- Index ⟷ station page ----------------------------------------------
        _stationIndex = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _stationIndex.AddThemeConstantOverride("separation", 8);
        root.AddChild(_stationIndex);
        BuildStationIndex(_stationIndex);

        _stationPage = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Visible = false };
        _stationPage.AddThemeConstantOverride("separation", 8);
        var leaveRow = Row();
        leaveRow.AddChild(MakeButton("← Hideout", LeaveStation));
        _stationPage.AddChild(leaveRow);
        root.AddChild(_stationPage);
    }

    private void BuildStationIndex(VBoxContainer root)
    {
        var intro = Wrapping(Muted);
        intro.Text = "Choose a station. Each one trains its profession and offers whatever it is for — "
                    + "the Forge smelts and forges blades, the Apothecary steeps, the Assay Table reads.";
        root.AddChild(intro);

        foreach (var category in Enum.GetValues<ProfessionCategory>())
        {
            var stations = _game.StationsIn(category);
            if (stations.Count == 0)
                continue;

            var shelf = new Label { Text = category.ToString() };
            shelf.AddThemeColorOverride("font_color", Muted);
            root.AddChild(shelf);

            var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            grid.AddThemeConstantOverride("h_separation", 6);
            grid.AddThemeConstantOverride("v_separation", 6);
            root.AddChild(grid);

            foreach (var station in stations)
            {
                var button = MakeButton(station.Name, () => OpenStation(station), Accent);
                button.CustomMinimumSize = new Vector2(170, 0);
                button.TooltipText = station.Description;
                grid.AddChild(button);
            }
        }

        root.AddChild(new HSeparator());
        _professionSummaryLabel = new Label();
        root.AddChild(Card(_professionSummaryLabel));

        var debugRow = Row();
        root.AddChild(debugRow);
        debugRow.AddChild(MakeButton("Grant Test Mats (debug)", () => _game.GrantCraftTestMaterials(), Accent));
    }

    private void OpenStation(StationDefinition station)
    {
        if (!_visitedStations.TryGetValue(station.Id, out var page))
        {
            page = new StationPanel(_game, station, CurrentTimingPerformance);
            _visitedStations[station.Id] = page;
        }

        if (_openStation is { } previouslyOpen)
            previouslyOpen.Visible = false;

        if (page.GetParent() is null)
            _stationPage.AddChild(page);

        page.Visible = true;
        page.Refresh(); // it may have been sitting behind several level-ups
        _openStation = page;

        _stationIndex.Visible = false;
        _stationPage.Visible = true;
    }

    private void LeaveStation()
    {
        if (_openStation is { } page)
            page.Visible = false;
        _openStation = null;

        _stationPage.Visible = false;
        _stationIndex.Visible = true;
    }

    private VBoxContainer BuildOpportunityPanel()
    {
        var panel = new VBoxContainer();
        _opportunityTitle = new Label();
        _opportunityTitle.AddThemeColorOverride("font_color", Accent);
        _opportunityTitle.AddThemeFontSizeOverride("font_size", 15);
        panel.AddChild(_opportunityTitle);

        _opportunityPrompt = Wrapping();
        panel.AddChild(_opportunityPrompt);

        _opportunityCost = Wrapping(Muted);
        panel.AddChild(_opportunityCost);

        var buttons = Row();
        panel.AddChild(buttons);
        buttons.AddChild(MakeButton("Pursue", () => _game.PursuePendingOpportunity(), Positive));
        buttons.AddChild(MakeButton("Leave it", () => _game.DeclinePendingOpportunity()));
        return panel;
    }

    /// <summary>Shows or hides the pursue/ignore card. The offer is the decision, so it gets
    /// its own card rather than a line in the log.</summary>
    private void ShowOpportunity(GameRoot.PendingOpportunity? pending)
    {
        if (pending is null)
        {
            _opportunityCard.Visible = false;
            return;
        }

        var offer = pending.Offer;
        _opportunityTitle.Text = offer.Name;
        _opportunityPrompt.Text = offer.Prompt;

        var seconds = offer.ExtraIntervalTicks / (double)GameRoot.TicksPerSecond;
        var risk = offer.RiskWeight <= 0
            ? "no risk"
            : $"{offer.RiskWeight:P0} chance it comes to nothing";
        _opportunityCost.Text = $"Costs {seconds:0.#}s · {risk}";
        _opportunityCard.Visible = true;
    }

    /// <summary>
    /// The Character Lab (docs/classes.md).
    ///
    /// <para>Its purpose is one sentence: <b>swap any one component and immediately understand
    /// what changed.</b> With 15 × 25 × 50 combinations there is no other way to judge whether
    /// a Base, Prefix or Suffix is pulling its weight — so the diff panel is the feature, and
    /// the readout is the supporting evidence.</para>
    /// </summary>
    private void BuildCharacterLabSection(VBoxContainer root)
    {
        root.AddChild(SectionTitle("Character Lab"));

        _labNameLabel = Wrapping(Accent);
        _labNameLabel.AddThemeFontSizeOverride("font_size", 17);
        root.AddChild(Card(_labNameLabel));

        // --- Selectors -------------------------------------------------------
        var baseRow = Row();
        root.AddChild(baseRow);
        baseRow.AddChild(new Label { Text = "Base:", CustomMinimumSize = new Vector2(64, 0) });
        _basePicker = new OptionButton { CustomMinimumSize = new Vector2(200, 0) };
        foreach (var @base in _game.Bases)
            _basePicker.AddItem(@base.Name);
        _basePicker.ItemSelected += _ => ApplyLabSelection();
        baseRow.AddChild(_basePicker);

        // On its own line, not in the row: an autowrapping Label inside an HBoxContainer gets
        // squeezed to its minimum width and wraps one character per line.
        _labEngineLabel = Wrapping(Muted);
        root.AddChild(_labEngineLabel);

        var prefixRow = Row();
        root.AddChild(prefixRow);
        prefixRow.AddChild(new Label { Text = "Prefix:", CustomMinimumSize = new Vector2(64, 0) });
        _prefixPicker = new OptionButton { CustomMinimumSize = new Vector2(200, 0) };
        foreach (var prefix in _game.PrefixCatalog)
            _prefixPicker.AddItem(prefix.Name);
        _prefixPicker.ItemSelected += _ => ApplyLabSelection();
        prefixRow.AddChild(_prefixPicker);

        _labMechanicLabel = Wrapping(Muted);
        root.AddChild(_labMechanicLabel);

        var suffixRow = Row();
        root.AddChild(suffixRow);
        suffixRow.AddChild(new Label { Text = "Suffix:", CustomMinimumSize = new Vector2(64, 0) });
        _suffixPicker = new OptionButton { CustomMinimumSize = new Vector2(200, 0) };
        foreach (var suffix in _game.SuffixCatalog)
            _suffixPicker.AddItem(suffix.IsFullyExpressed ? suffix.Name : suffix.Name + "  (roster only)");
        _suffixPicker.ItemSelected += _ => ApplyLabSelection();
        suffixRow.AddChild(_suffixPicker);
        suffixRow.AddChild(MakeButton("Random", RandomiseLabBuild, Accent));

        // --- What changed ----------------------------------------------------
        root.AddChild(new HSeparator());
        var diffHead = new Label { Text = "What changed:" };
        diffHead.AddThemeColorOverride("font_color", Muted);
        root.AddChild(diffHead);
        _labDiffLabel = Wrapping(Positive);
        root.AddChild(Card(_labDiffLabel));

        // --- Full readout ----------------------------------------------------
        // Deliberately not wrapping: the report is pre-formatted with column padding and its
        // own line breaks, and autowrap would cut the table apart mid-row.
        _labReportLabel = new Label();
        root.AddChild(Card(_labReportLabel));

        SyncLabPickers();
        RefreshCharacterLab(Array.Empty<string>());
    }

    /// <summary>Points the pickers at whatever build is currently active.</summary>
    private void SyncLabPickers()
    {
        var build = _game.CurrentBuild;

        _basePicker.Selected = Math.Max(0, _game.Bases.ToList().FindIndex(b => b.Id == build.BaseClassId.Value));
        _prefixPicker.Selected = Math.Max(0, _game.PrefixCatalog.ToList().FindIndex(p => p.Id == build.PrefixId.Value));
        _suffixPicker.Selected = Math.Max(0, _game.SuffixCatalog.ToList().FindIndex(s => s.Id == build.SuffixId.Value));
    }

    private void ApplyLabSelection()
    {
        var bases = _game.Bases;
        var prefixes = _game.PrefixCatalog;
        var suffixes = _game.SuffixCatalog;

        if (_basePicker.Selected < 0 || _prefixPicker.Selected < 0 || _suffixPicker.Selected < 0)
            return;

        var previous = _game.ResolveBuild(_game.CurrentBuild);

        _game.SetBuild(
            bases[_basePicker.Selected].Id,
            prefixes[_prefixPicker.Selected].Id,
            suffixes[_suffixPicker.Selected].Id);

        RefreshCharacterLab(Dungeons.Characters.Composition.BuildResolver.Diff(
            previous, _game.ResolveBuild(_game.CurrentBuild)));
    }

    private void RandomiseLabBuild()
    {
        _basePicker.Selected = (int)(GD.Randi() % (uint)_basePicker.ItemCount);
        _prefixPicker.Selected = (int)(GD.Randi() % (uint)_prefixPicker.ItemCount);
        _suffixPicker.Selected = (int)(GD.Randi() % (uint)_suffixPicker.ItemCount);
        ApplyLabSelection();
    }

    private void RefreshCharacterLab(IReadOnlyList<string> diff)
    {
        if (_labReportLabel is null)
            return; // tab not built yet

        var build = _game.ResolveBuild(_game.CurrentBuild);

        _labNameLabel.Text = build.Name;
        _labEngineLabel.Text = build.Base.Engine;
        _labMechanicLabel.Text = build.Prefix?.Mechanic ?? string.Empty;
        _labReportLabel.Text = _game.BuildReport();
        _labDiffLabel.Text = diff.Count == 0 ? "(pick a different component)" : string.Join("\n", diff);
    }

    /// <summary>
    /// The Realm tab is two screens that swap: <b>preparation</b> when the player is in the
    /// Hideout, and the <b>run</b> once they are inside. That is the whole reason this tab
    /// exists — it used to open on a list of every realm in the game with an Enter button beside
    /// each, which is a menu, not a decision.
    /// </summary>
    private void BuildRealmSection(VBoxContainer root)
    {
        _preparation = new RealmPreparationPanel(_game);
        root.AddChild(_preparation);

        _realmRunView = new VBoxContainer();
        _realmRunView.AddThemeConstantOverride("separation", 8);
        root.AddChild(_realmRunView);

        _realmRunView.AddChild(SectionTitle("Realm"));
        // Same height floor as the Combat card: the report re-renders per frame during a realm
        // fight, and the fight buttons live directly below it.
        _realmLabel = new Label { CustomMinimumSize = new Vector2(0, 150) };
        _realmRunView.AddChild(Card(_realmLabel));

        _realmControls = new VBoxContainer();
        _realmControls.AddThemeConstantOverride("separation", 4);
        _realmRunView.AddChild(_realmControls);
    }

    private void BuildCombatSection(VBoxContainer root)
    {
        root.AddChild(SectionTitle("Combat"));
        // Reserve height for the tallest routine report (combatants + ready line + intents +
        // gauges). The label re-renders every frame mid-fight, and without a floor its line
        // count resizes the card — shoving the move buttons around exactly when the player is
        // trying to click one.
        _combatLabel = new Label { CustomMinimumSize = new Vector2(0, 150) };
        root.AddChild(Card(_combatLabel));

        var startRow = Row();
        root.AddChild(startRow);
        startRow.AddChild(new Label { Text = "Start fight:" });
        foreach (var actor in _game.EnemyActors)
        {
            var actorId = actor.Id;
            startRow.AddChild(MakeButton(actor.Name, () => _game.StartCombat(actorId), Danger));
        }

        // E4: one button per resolved move. Rebuilt only when the move list itself changes
        // (fight start, equipment swap, runtime grantMove) — RefreshCombat runs per frame
        // during a fight, and recreating buttons every frame would eat the click.
        _moveButtonsRow = Row();
        root.AddChild(_moveButtonsRow);

        var actionRow = Row();
        root.AddChild(actionRow);
        actionRow.AddChild(MakeButton("Attack", () => _game.CombatAttack(), Accent));
        actionRow.AddChild(MakeButton("Block", () => _game.CombatBlock()));
        actionRow.AddChild(MakeButton("Dodge", () => _game.CombatDodge()));
        if (_game.PlayerCanParry)
            actionRow.AddChild(MakeButton("Parry", () => _game.CombatParry(), Accent));
        actionRow.AddChild(MakeButton("Use Salve", () => _game.CombatUseConsumable("consumable.healing_salve"), Positive));
        actionRow.AddChild(MakeButton("Wait", () => _game.CombatWait()));

        var trace = new CheckButton { Text = "Hit trace" };
        trace.Toggled += on =>
        {
            _game.ShowHitLog = on;    // stream per-hit traces into the event log
            _hitLogCard.Visible = on; // and pin the last hit's full trace here
            if (on)
                _hitLogLabel.Text = _game.LastHitLog;
        };
        actionRow.AddChild(trace);

        _hitLogLabel = new Label();
        _hitLogLabel.AddThemeFontOverride("font",
            new SystemFont { FontNames = new[] { "Consolas", "monospace" } });
        _hitLogCard = Card(_hitLogLabel);
        _hitLogCard.Visible = false;
        root.AddChild(_hitLogCard);
    }

    private void BuildInventorySection(VBoxContainer root)
    {
        root.AddChild(SectionTitle("Inventory"));
        _inventoryLabel = new Label { Text = "…" };
        root.AddChild(Card(_inventoryLabel));

        root.AddChild(SectionTitle("Techniques"));
        _techniqueControls = new VBoxContainer();
        root.AddChild(_techniqueControls);
        root.AddChild(MakeButton("Grant Techniques (debug)", () => _game.GrantTestTechniques(), Accent));
    }

    private void RebuildTechniqueControls()
    {
        ClearChildren(_techniqueControls);

        var owned = _game.OwnedTechniques;
        if (owned.Count == 0)
        {
            _techniqueControls.AddChild(new Label { Text = "  (no technique items in the stash)" });
            return;
        }

        foreach (var (technique, quantity, known) in owned)
        {
            var row = Row();
            _techniqueControls.AddChild(row);
            row.AddChild(new Label
            {
                Text = $"{technique.Name} ×{quantity} — {technique.Description}",
                CustomMinimumSize = new Vector2(420, 0),
            });
            if (known)
                row.AddChild(new Label { Text = "(known)" });
            else
            {
                var id = technique.Id;
                row.AddChild(MakeButton("Learn", () => _game.LearnTechnique(id), Positive));
            }
        }
    }

    // --- Dynamic control groups --------------------------------------------

    private void RebuildEquipmentControls()
    {
        ClearChildren(_equipmentControls);

        // Driven by the slot vocabulary, not by a hand-written list — a new slot appears here
        // the moment it exists.
        foreach (var slot in EquipmentSlots.DisplayOrder)
        {
            var summary = slot == EquipmentSlot.Weapon ? _game.EquippedWeaponSummary() : string.Empty;
            AddSlotRow(slot, _game.Equipped(slot), summary);
        }

        var wornRow = new Label { Text = $"Worn total: {_game.EquippedArmorSummary()}" };
        wornRow.AddThemeColorOverride("font_color", Muted);
        _equipmentControls.AddChild(wornRow);

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
            row.AddChild(new Label { Text = _game.ItemLabel(instance), CustomMinimumSize = new Vector2(340, 0) });
            var id = instance.InstanceId;
            row.AddChild(MakeButton("Equip", () => _game.EquipFromStash(id), Accent));
        }
    }

    private void AddSlotRow(EquipmentSlot slot, ItemInstance? equipped, string summary)
    {
        var row = Row();
        _equipmentControls.AddChild(row);
        var name = equipped?.DisplayName ?? "— (empty)";
        var position = EquipmentSlotNames.PositionOf(slot);
        var line = summary.Length > 0 ? $"{position}: {name}  →  {summary}" : $"{position}: {name}";
        row.AddChild(new Label { Text = line, CustomMinimumSize = new Vector2(420, 0) });
        if (equipped is not null)
            row.AddChild(MakeButton("Unequip", () => _game.UnequipToStash(slot), Danger));
    }

    /// <summary>One button per resolved move (E4), keyed by the move-id list so the per-frame
    /// combat refresh never recreates a button mid-click.</summary>
    private void RebuildMoveButtons()
    {
        var moves = _game.PlayerMoveset;
        var key = string.Join("|", moves.Select(m => m.Id));
        if (key == _moveButtonsKey)
            return;

        _moveButtonsKey = key;
        ClearChildren(_moveButtonsRow);

        _moveButtonsRow.AddChild(new Label { Text = "Moves:" });
        AddMoveButtons(_moveButtonsRow);
    }

    private void AddMoveButtons(Container row)
    {
        foreach (var move in _game.PlayerMoveset)
        {
            var id = move.Id;
            var button = MakeButton(move.Name, () => _game.CombatUseMove(id), Accent);
            button.TooltipText = MoveTooltip(move);
            row.AddChild(button);
        }
    }

    private static string MoveTooltip(ResolvedMove move)
    {
        var costs = move.Costs.Count == 0 ? "free" : string.Join(", ", move.Costs);
        var tooltip = $"{move.Id}\nCost: {costs}";
        if (move.CooldownTicks > 0)
            tooltip += $"\nCooldown: {move.CooldownTicks} ticks";
        tooltip += $"\nFrom: {move.Provenance[0]}";
        return tooltip;
    }

    /// <summary>The in-run controls. Entering is the preparation screen's job, so this no longer
    /// has an out-of-realm branch.</summary>
    private void RebuildRealmControls()
    {
        ClearChildren(_realmControls);

        if (!_game.InRealm)
            return;

        if (_game.RealmBusy)
        {
            _realmControls.AddChild(new Label { Text = "In combat — act here (telegraphs show in the Combat tab):" });
            var movesRow = Row();
            _realmControls.AddChild(movesRow);
            AddMoveButtons(movesRow);
            var fightRow = Row();
            _realmControls.AddChild(fightRow);
            fightRow.AddChild(MakeButton("Attack", () => _game.CombatAttack(), Accent));
            fightRow.AddChild(MakeButton("Block", () => _game.CombatBlock()));
            fightRow.AddChild(MakeButton("Dodge", () => _game.CombatDodge()));
            if (_game.PlayerCanParry)
                fightRow.AddChild(MakeButton("Parry", () => _game.CombatParry(), Accent));
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
        RebuildMoveButtons(); // a build swap can change the moveset (guarded — no-op if unchanged)
        RefreshPreparation(); // the loadout screen shows the same slots and the same moveset

        // The build can also change from the Character tab (Cycle Suffix), so the Lab follows.
        // Setting OptionButton.Selected does not raise ItemSelected, so this cannot loop.
        if (_labReportLabel is not null)
        {
            SyncLabPickers();
            RefreshCharacterLab(Array.Empty<string>());
        }
    }

    private void RefreshHideoutAndInventory()
    {
        _professionSummaryLabel.Text = _game.ProfessionSummary();

        // A level-up unlocks ladder rungs, plots and obstacles, a harvest empties a plot, and
        // every material picker is a snapshot — the whole open page follows the inventory.
        RefreshOpenStation();

        _inventoryLabel.Text = _game.InventoryReport();
        RebuildEquipmentControls(); // stash equipment may have changed
        RebuildTechniqueControls(); // technique items granted, learned, or spent
        RebuildMoveButtons(); // a weapon swap changes the granted moves (guarded — no-op if unchanged)
        RefreshPreparation(); // banked supplies and spare gear are both read from the Stash
    }

    /// <summary>Stations the player is not standing in are left stale on purpose; each is
    /// refreshed when it is opened, which is the only moment its contents are read.</summary>
    private void RefreshOpenStation() => _openStation?.Refresh();

    private void RefreshCombat()
    {
        var report = _game.CombatReport();
        var gauges = _game.GaugeReadout;
        if (gauges.Count > 0)
            report += "\nGauges:  " + string.Join("   ", gauges);
        _combatLabel.Text = report;

        RebuildMoveButtons();
        if (_hitLogCard.Visible)
            _hitLogLabel.Text = _game.LastHitLog;

        if (!_game.InRealm)
            return;

        _realmLabel.Text = _game.RealmReport(); // keep party HP live during a realm fight
        if (!_game.RealmBusy)
            RebuildRealmControls(); // combat just ended → surface travel/extract options again
    }

    /// <summary>Swaps the Realm tab between preparing and running, and re-reads whichever is
    /// showing. The two never render at once — an Enter button beside a live run would be a
    /// question the game has already answered.</summary>
    private void RefreshRealm()
    {
        var inRun = _game.InRealm;
        _preparation.Visible = !inRun;
        _realmRunView.Visible = inRun;

        if (!inRun)
        {
            _preparation.Refresh();
            return;
        }

        _realmLabel.Text = _game.RealmReport();
        RebuildRealmControls();
    }

    /// <summary>
    /// The preparation screen reads equipment, the Stash and Realm Knowledge, so it follows all
    /// three — but <b>only while it is actually on screen</b>.
    ///
    /// <para>Same rule as an unopened station page, for a sharper reason: a briefing resolves
    /// every enemy in the realm through the family → role → actor fold, and passive gathering
    /// raises <c>InventoryChanged</c> on every completed action. Rebuilding a screen nobody is
    /// looking at, once per profession tick, would be the most expensive thing the UI does.</para>
    /// </summary>
    private void RefreshPreparation()
    {
        if (!_game.InRealm && _preparation.Visible && _tabs.CurrentTab == _realmTabIndex)
            _preparation.Refresh();
    }

    private void OnTabChanged(long tab)
    {
        if (tab == _realmTabIndex)
            RefreshPreparation();
    }

    private void AppendLog(string message) => _log.AddText(message + "\n");

    /// <summary>1.0 when the active-timing sweep is dead-centre, falling to 0 at the edges.</summary>
    private double CurrentTimingPerformance() =>
        ProfessionTuning.TimingPerformance(_timingBar.Value / 100.0);
}
