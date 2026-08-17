using System.Collections.Generic;
using System.Linq;
using Dungeons.Crafting;
using Godot;
using static Dungeons.Game.Ui.ConsoleTheme;

namespace Dungeons.Game.Ui;

/// <summary>
/// The fixed <see cref="CraftingInteractionDefinition"/> set a station can perform — today just
/// the Healing Salve at the Apothecary.
///
/// <para>These are the <b>legacy</b> half of crafting: an authored input list with an authored
/// result, kept alive only because consumables have no emergent path yet (P5c). The emergent
/// bench next to it is the real system and has no table of any kind. When consumable forms land,
/// this panel and the interactions behind it go together.</para>
///
/// <para>An undiscovered interaction shows as <c>???</c> rather than being hidden, so the player
/// can see there is something here to find.</para>
/// </summary>
public partial class CraftingInteractionsPanel : VBoxContainer
{
    private readonly GameRoot _game;
    private readonly IReadOnlyList<CraftingInteractionDefinition> _interactions;
    private VBoxContainer _rows = null!;

    public CraftingInteractionsPanel(GameRoot game, IReadOnlyList<CraftingInteractionDefinition> interactions)
    {
        _game = game;
        _interactions = interactions;
        AddThemeConstantOverride("separation", 4);
        AddChild(SectionTitle("Known Interactions"));
        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 4);
        AddChild(_rows);
        Refresh();
    }

    public void Refresh()
    {
        ClearChildren(_rows);

        foreach (var interaction in _interactions)
        {
            var known = _game.IsDiscovered(interaction.DiscoveryId);
            var row = Row();
            _rows.AddChild(row);

            var inputs = string.Join(" + ", interaction.Inputs.Select(input => $"{input.Quantity} {_game.ItemName(input.ItemId)}"));
            var label = new Label
            {
                Text = $"{(known ? interaction.Name : "???")}   —   {inputs}  →  "
                     + $"{interaction.ResultQuantity} {_game.ItemName(interaction.ResultItemId)}",
                CustomMinimumSize = new Vector2(420, 0),
            };
            if (!known)
                label.AddThemeColorOverride("font_color", Muted);
            row.AddChild(label);

            var gate = string.Join(", ", interaction.ProfessionRequirements.Select(requirement =>
                $"{_game.ProfessionName(requirement.ProfessionId)} L{requirement.Level} "
                + $"(have L{_game.ProfessionLevel(requirement.ProfessionId)})"));
            var gateLabel = new Label { Text = gate, CustomMinimumSize = new Vector2(220, 0) };
            gateLabel.AddThemeColorOverride("font_color", Muted);
            row.AddChild(gateLabel);

            var id = interaction.Id;
            row.AddChild(MakeButton("Make", () => _game.MakeInteraction(id), Positive));
        }
    }
}
