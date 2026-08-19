using System;
using System.Collections.Generic;
using System.Linq;
using Dungeons.Combat;
using Dungeons.Items;
using Dungeons.Presentation;
using Dungeons.Realms;
using Godot;
using static Dungeons.Game.Ui.ConsoleTheme;

namespace Dungeons.Game.Ui;

/// <summary>
/// The portal screen: the one place the player decides what they are taking and where they are
/// taking it, standing between the Hideout and a run.
///
/// <para>This is the first surface in the project built to be <b>played</b> rather than driven —
/// it reads as a briefing, not as a control panel. Everything on it already existed somewhere in
/// the console; what it adds is that the equipment you fabricated, the supplies you cooked and
/// the Realm Knowledge you earned are finally legible in the same glance, at the moment the
/// decision is actually made.</para>
///
/// <para><b>Only what has been earned is shown.</b> Every gated section renders its own "you do
/// not know this yet" line rather than disappearing, because an absent section reads as an empty
/// realm and a locked one reads as something to go and learn (see
/// <see cref="PreparationText.NotYetKnown"/>).</para>
/// </summary>
public partial class RealmPreparationPanel : VBoxContainer
{
    private readonly GameRoot _game;

    private Label _realmName = null!;
    private Label _realmSubtitle = null!;
    private OptionButton _realmPicker = null!;
    private HBoxContainer _depthRow = null!;
    private OptionButton _depthPicker = null!;
    private VBoxContainer _loadoutRows = null!;
    private VBoxContainer _consumableRows = null!;
    private VBoxContainer _toolRows = null!;
    private VBoxContainer _threatRows = null!;
    private VBoxContainer _resourceRows = null!;
    private VBoxContainer _realmInfoRows = null!;

    /// <summary>Realm ids in picker order, so a selection maps back without parsing the label.</summary>
    private readonly List<string> _pickerRealmIds = new();

    /// <summary>Wide enough for "Rusty Sword" beside "Weapon" without the buttons jittering as
    /// names change length.</summary>
    private const int SlotLabelWidth = 90;
    private const int ItemLabelWidth = 300;

    public RealmPreparationPanel(GameRoot game)
    {
        _game = game;
        AddThemeConstantOverride("separation", 10);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        Build();
        Refresh();
    }

    private void Build()
    {
        var header = new VBoxContainer();
        header.AddThemeConstantOverride("separation", 2);

        _realmName = new Label();
        _realmName.AddThemeFontSizeOverride("font_size", 24);
        _realmName.AddThemeColorOverride("font_color", Accent);
        header.AddChild(_realmName);

        _realmSubtitle = Wrapping(Muted);
        header.AddChild(_realmSubtitle);
        AddChild(header);

        var destination = Row();
        destination.AddChild(new Label { Text = "Destination:" });
        _realmPicker = new OptionButton { CustomMinimumSize = new Vector2(260, 0) };
        _realmPicker.ItemSelected += OnRealmPicked;
        destination.AddChild(_realmPicker);

        // The depth picker is the one control on this screen that Realm Knowledge switches on
        // rather than fills in (GDD §11.4's portal targeting). It stays hidden until earned —
        // a greyed-out control the player cannot explain is worse than no control.
        _depthRow = Row();
        _depthRow.AddChild(new Label { Text = "   Enter at depth:" });
        _depthPicker = new OptionButton { CustomMinimumSize = new Vector2(90, 0) };
        _depthPicker.ItemSelected += index => _game.SetStartingDepth((int)index + 1);
        _depthRow.AddChild(_depthPicker);
        destination.AddChild(_depthRow);
        AddChild(destination);

        _loadoutRows = AddSection("Loadout");
        _consumableRows = AddSection("Consumables");
        _toolRows = AddSection("Tools");
        _threatRows = AddSection("Known Threats");
        _resourceRows = AddSection("Known Resources");
        _realmInfoRows = AddSection("Realm Information");

        AddChild(new HSeparator());
        var enter = MakeButton("[ ENTER REALM ]", () => _game.EnterPreparedRun(), Positive);
        enter.CustomMinimumSize = new Vector2(0, 40);
        enter.AddThemeFontSizeOverride("font_size", 16);
        AddChild(enter);
    }

    /// <summary>A titled card with an empty body the refresh fills. Returns the body.</summary>
    private VBoxContainer AddSection(string title)
    {
        AddChild(SectionTitle(title));
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 3);
        AddChild(Card(body));
        return body;
    }

    public void Refresh()
    {
        RebuildRealmPicker();

        var briefing = _game.Briefing();
        _realmName.Text = (briefing?.RealmName ?? "Nowhere").ToUpperInvariant();
        _realmSubtitle.Text = briefing is null
            ? "No Realm is available to enter."
            : $"{briefing.MaxDepth} depth(s) · {string.Join(" · ", briefing.Tags)} · Realm Knowledge {briefing.Knowledge}"
              + $"    ·    Character level {_game.CharacterLevel}";

        RebuildDepthPicker();
        RebuildLoadout();
        RebuildConsumables();
        RebuildTools();
        RebuildThreats(briefing);
        RebuildResources(briefing);
        RebuildRealmInformation(briefing);
    }

    private void RebuildRealmPicker()
    {
        var realms = _game.Realms;
        var selectedId = _game.SelectedRealm?.Id;

        // Rebuilt only when the roster or the selection actually moved: OptionButton.Selected
        // does not raise ItemSelected, but clearing and refilling on every inventory change
        // would close the dropdown under the player's cursor.
        var unchanged = _pickerRealmIds.Count == realms.Count
            && _pickerRealmIds.SequenceEqual(realms.Select(realm => realm.Id));

        if (!unchanged)
        {
            _realmPicker.Clear();
            _pickerRealmIds.Clear();
            foreach (var realm in realms)
            {
                _realmPicker.AddItem(realm.Name);
                _pickerRealmIds.Add(realm.Id);
            }
        }

        var index = selectedId is null ? -1 : _pickerRealmIds.IndexOf(selectedId);
        if (index >= 0 && _realmPicker.Selected != index)
            _realmPicker.Selected = index;
    }

    private void OnRealmPicked(long index)
    {
        if (index >= 0 && index < _pickerRealmIds.Count)
            _game.SelectRealm(_pickerRealmIds[(int)index]);
    }

    /// <summary>
    /// Where the expedition starts. Only visible once <see cref="RealmInsight.DeepEntry"/> is
    /// earned, which is the last rung of the ladder — you cannot aim at a door you have not
    /// found.
    /// </summary>
    private void RebuildDepthPicker()
    {
        var deepest = _game.DeepestStartingDepth;
        _depthRow.Visible = deepest > 1;
        if (!_depthRow.Visible)
            return;

        if (_depthPicker.ItemCount != deepest)
        {
            _depthPicker.Clear();
            for (var depth = 1; depth <= deepest; depth++)
                _depthPicker.AddItem(depth.ToString());
        }

        var chosen = Math.Clamp(_game.StartingDepth, 1, deepest) - 1;
        if (_depthPicker.Selected != chosen)
            _depthPicker.Selected = chosen;
    }

    // --- Loadout ------------------------------------------------------------

    /// <summary>
    /// The gear half of the loadout. Reads and writes the <b>real</b> equipment through the
    /// normal equip path — there is no second copy of "what the player is wearing" behind this
    /// screen, which is why what you see here is exactly what combat resolves.
    /// </summary>
    private void RebuildLoadout()
    {
        ClearChildren(_loadoutRows);
        var status = _game.LoadoutStatus();

        foreach (var slot in status.Slots)
        {
            var row = Row();
            _loadoutRows.AddChild(row);

            row.AddChild(new Label
            {
                Text = EquipmentSlotNames.PositionOf(slot.Slot),
                CustomMinimumSize = new Vector2(SlotLabelWidth, 0),
            });

            var name = new Label
            {
                Text = slot.ItemName ?? "— empty —",
                CustomMinimumSize = new Vector2(ItemLabelWidth, 0),
            };
            if (!slot.Filled)
                name.AddThemeColorOverride("font_color", Muted);
            row.AddChild(name);

            if (slot.Filled)
            {
                var position = slot.Slot;
                row.AddChild(MakeButton("Unequip", () => _game.UnequipToStash(position), Danger));
            }
        }

        AddMutedLine(_loadoutRows, $"Attack: {_game.EquippedWeaponSummary()}    Worn: {_game.EquippedArmorSummary()}");

        var moves = _game.PlayerMoveset.Select(move => move.Name).ToList();
        AddMutedLine(_loadoutRows, moves.Count == 0
            ? "You have no moves. Something is wrong."
            : "You will fight with: " + string.Join(", ", moves));

        AddStashGear();

        foreach (var issue in status.Issues)
            AddWarning(_loadoutRows, PreparationText.Describe(issue));

        // GDD §13.1 — the game owes a broke character a way back in, and this is where the debt
        // is visible. It appears only when there is genuinely nothing to fight with.
        if (status.NeedsStarterKit)
        {
            AddMutedLine(_loadoutRows, "You have nothing to fight with. The Hideout keeps spares.");
            _loadoutRows.AddChild(MakeButton("Issue Starter Kit", () => _game.IssueStarterKit(), Accent));
        }
    }

    private void AddStashGear()
    {
        var stash = _game.StashEquipment;
        if (stash.Count == 0)
        {
            AddMutedLine(_loadoutRows, "Nothing spare in the Stash.");
            return;
        }

        AddMutedLine(_loadoutRows, "In the Stash:");
        foreach (var instance in stash)
        {
            var row = Row();
            _loadoutRows.AddChild(row);

            var slot = _game.SlotOf(instance);
            var worn = slot is null ? "unreadable" : EquipmentSlotNames.CategoryOf(slot.Value);
            row.AddChild(new Label
            {
                Text = $"  {instance.DisplayName} ({worn})",
                CustomMinimumSize = new Vector2(SlotLabelWidth + ItemLabelWidth, 0),
            });

            var instanceId = instance.InstanceId;
            row.AddChild(MakeButton("Equip", () => _game.EquipFromStash(instanceId), Accent));
        }
    }

    // --- Consumables --------------------------------------------------------

    /// <summary>
    /// What the party carries in. Packing is a <b>plan</b>: the supplies stay banked until the
    /// run starts, and the plan survives the run so "I always take three salves" is said once.
    /// </summary>
    private void RebuildConsumables()
    {
        ClearChildren(_consumableRows);
        var choices = _game.ConsumableChoices;

        if (choices.Count == 0)
        {
            AddMutedLine(_consumableRows, "Nothing to take. Supplies are cooked, brewed and mixed in the Hideout.");
            return;
        }

        foreach (var (consumable, inStash, packed) in choices)
        {
            var row = Row();
            _consumableRows.AddChild(row);

            row.AddChild(new Label
            {
                Text = consumable.Name,
                CustomMinimumSize = new Vector2(ItemLabelWidth, 0),
            });

            var count = new Label
            {
                Text = $"packing {packed}   ({inStash} banked)",
                CustomMinimumSize = new Vector2(180, 0),
            };
            count.AddThemeColorOverride("font_color", packed > 0 ? Positive : Muted);
            row.AddChild(count);

            var id = consumable.Id;
            row.AddChild(MakeButton("+", () => _game.PackConsumable(id), Accent));
            row.AddChild(MakeButton("−", () => _game.UnpackConsumable(id)));
        }

        AddMutedLine(_consumableRows, "Packed supplies are unsecured from the moment you step through — "
            + "spend them or bring them home. The pack keeps its plan between runs.");

        if (choices.Any(choice => choice.Packed > 0))
            _consumableRows.AddChild(MakeButton("Clear pack", () => _game.ClearPack()));
    }

    // --- Tools --------------------------------------------------------------

    /// <summary>
    /// Whether the party can do the <em>work</em> half of the run, not just the fighting half.
    ///
    /// <para>Worn profession tools are E6 and are deliberately absent rather than mocked up: a
    /// tool slot with no yield pipeline behind it would put a number on screen that changes
    /// nothing, which is the one thing D30 forbids. What this shows instead is real — the trades
    /// the place asks for, and how far your professions have come.</para>
    /// </summary>
    private void RebuildTools()
    {
        ClearChildren(_toolRows);
        var fieldwork = _game.Fieldwork();

        if (fieldwork.Count == 0)
        {
            AddMutedLine(_toolRows, "There is nothing to gather here.");
            return;
        }

        foreach (var trade in fieldwork)
        {
            var row = Row();
            _toolRows.AddChild(row);

            row.AddChild(new Label
            {
                Text = $"{trade.ProfessionName} L{trade.PlayerLevel}",
                CustomMinimumSize = new Vector2(ItemLabelWidth, 0),
            });

            var reach = new Label
            {
                Text = trade.CanWorkEverything
                    ? $"can work all {trade.TotalNodeCount} node(s)"
                    : $"can work {trade.WorkableNodeCount} of {trade.TotalNodeCount} — next needs L{trade.NextLevelNeeded}",
            };
            reach.AddThemeColorOverride("font_color", trade.CanWorkAnything ? Positive : Danger);
            row.AddChild(reach);
        }

        AddMutedLine(_toolRows, "Profession tools are not made yet — until then, your levels are the tools.");
    }

    // --- The briefing -------------------------------------------------------

    private void RebuildThreats(RealmBriefing? briefing)
    {
        ClearChildren(_threatRows);
        if (briefing is null)
            return;

        if (!briefing.Knows(RealmInsight.EnemyWeaknesses))
            AddMutedLine(_threatRows, PreparationText.NotYetKnown(RealmInsight.EnemyWeaknesses));

        foreach (var threat in briefing.Threats)
        {
            var rank = PreparationText.RankLabel(threat.Rank);
            var heading = new Label
            {
                Text = rank is null
                    ? $"{threat.Name} — {threat.LocationName}, depth {threat.Depth}"
                    : $"{threat.Name} [{rank}] — {threat.LocationName}, depth {threat.Depth}",
            };
            heading.AddThemeColorOverride("font_color", threat.Rank == EnemyRank.Normal ? TextColor : Danger);
            _threatRows.AddChild(heading);

            AddMutedLine(_threatRows, "    hit it with: " + Describe(threat.VulnerableDamageTypes, "nothing in particular"));
            AddMutedLine(_threatRows, "    burns to: " + Describe(threat.ExposedLanes, "nothing"));
            AddMutedLine(_threatRows, "    shrugs off: " + Describe(threat.ResistedLanes, "nothing"));
        }

        if (!briefing.Knows(RealmInsight.Hazards))
        {
            AddMutedLine(_threatRows, PreparationText.NotYetKnown(RealmInsight.Hazards));
            return;
        }

        foreach (var hazard in briefing.Hazards)
            _threatRows.AddChild(new Label
            {
                Text = $"{hazard.Name} — dangerous ground at depth {hazard.Depth}, costs {hazard.HealthCost} health to cross",
            });
    }

    private void RebuildResources(RealmBriefing? briefing)
    {
        ClearChildren(_resourceRows);
        if (briefing is null)
            return;

        // Two rungs, two different facts. WHAT the place yields is the cheapest thing to learn;
        // WHERE the good ground is costs far more.
        if (!briefing.Knows(RealmInsight.CommonResources))
            AddMutedLine(_resourceRows, PreparationText.NotYetKnown(RealmInsight.CommonResources));
        else if (briefing.Yield.Count == 0)
            AddMutedLine(_resourceRows, "This place gives up nothing you would carry home.");
        else
            AddMutedLine(_resourceRows, "This place yields: " + DescribeYield(briefing.Yield));

        if (!briefing.Knows(RealmInsight.RichNodes))
        {
            AddMutedLine(_resourceRows, PreparationText.NotYetKnown(RealmInsight.RichNodes));
            return;
        }

        if (briefing.Resources.Count == 0)
        {
            AddMutedLine(_resourceRows, "No workings here are worth a special trip.");
            return;
        }

        foreach (var resource in briefing.Resources)
            _resourceRows.AddChild(new Label
            {
                Text = $"{resource.LocationName} — depth {resource.Depth} · {resource.ActionName} "
                     + $"({resource.ProfessionName} L{resource.RequiredLevel})",
            });
    }

    /// <summary>
    /// The realm's material list, ordinary first. Capped, because a Dark Forest that names all
    /// ~90 of its materials is a wall of text rather than a briefing — the rare end is the part
    /// worth reading, so that is the part that survives the cut.
    /// </summary>
    private static string DescribeYield(IReadOnlyList<KnownYield> yield)
    {
        const int NamedYieldLimit = 14;

        var rarestFirst = yield.OrderByDescending(entry => entry.Rarity).ToList();
        var named = rarestFirst.Take(NamedYieldLimit).Select(entry => entry.MaterialName);
        var remainder = rarestFirst.Count - NamedYieldLimit;

        return string.Join(", ", named) + (remainder > 0 ? $", and {remainder} more ordinary things" : string.Empty);
    }

    private void RebuildRealmInformation(RealmBriefing? briefing)
    {
        ClearChildren(_realmInfoRows);
        if (briefing is null)
            return;

        _realmInfoRows.AddChild(new Label
        {
            Text = $"Realm Knowledge {briefing.Knowledge} — {briefing.Unlocked.Count} of "
                 + $"{RealmKnowledgeLevels.Required.Count} insights earned.",
        });

        foreach (var insight in briefing.Unlocked)
            AddMutedLine(_realmInfoRows, $"    known: you {PreparationText.DescribeInsight(insight)}");

        if (briefing.NextInsight is { } next)
            AddMutedLine(_realmInfoRows,
                $"    at {next.Required} knowledge you will {PreparationText.DescribeInsight(next.Insight)}");

        if (!briefing.Knows(RealmInsight.ExtractionRoutes) && !briefing.Knows(RealmInsight.HiddenRoutes))
        {
            AddMutedLine(_realmInfoRows, PreparationText.NotYetKnown(RealmInsight.ExtractionRoutes));
            return;
        }

        foreach (var route in briefing.Routes)
            _realmInfoRows.AddChild(new Label
            {
                Text = route.Hidden
                    ? $"{route.Name} — depth {route.Depth}, a way nobody marked"
                    : $"{route.Name} — depth {route.Depth}, {route.Type.ToString().ToLowerInvariant()}",
            });
    }

    // --- Row helpers --------------------------------------------------------

    private static void AddMutedLine(Node parent, string text)
    {
        var label = Wrapping(Muted);
        label.Text = text;
        parent.AddChild(label);
    }

    private static void AddWarning(Node parent, string text)
    {
        var label = Wrapping(Danger);
        label.Text = "⚠ " + text;
        parent.AddChild(label);
    }

    private static string Describe(IReadOnlyList<string> values, string whenEmpty) =>
        values.Count == 0 ? whenEmpty : string.Join(", ", values.Select(PreparationText.LaneLabel));

    private static string Describe(IReadOnlyList<ResistanceReading> lanes, string whenEmpty) =>
        lanes.Count == 0
            ? whenEmpty
            : string.Join(", ", lanes.Select(lane => $"{PreparationText.LaneLabel(lane.Lane)} {lane.Fraction:P0}"));
}
