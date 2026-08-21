using System.Text.Json.Serialization;
using Dungeons.Content;

namespace Dungeons.Hideout;

/// <summary>
/// A fixture in the Hideout — the Forge, the Apothecary, the Fishing Dock — and the player's
/// entry point into everything that fixture hosts.
///
/// <para>A station <b>owns no rules</b>. It is a routing table: which profession ladders are
/// trained here, which crafting actions the bench here offers, which blueprints can be
/// assembled here. Every one of those still resolves through the system that already owned it
/// (<c>ProfessionSystem</c>, <c>MaterialTransformationEngine</c>, <c>EquipmentAssemblyEngine</c>),
/// under the same gates. Hosting Distill at the Alchemy Lab does not move its Herblore
/// requirement — a station decides <em>where</em> you stand, never <em>whether</em> you may.</para>
///
/// <para>Deliberately absent: flags for the bespoke panels. The Farming plots belong to
/// whichever station hosts <c>profession.farming</c> and the training course to Agility's, so
/// the identity that already exists carries them rather than a second, drift-prone switch.</para>
/// </summary>
public sealed class StationDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    /// <summary>One line naming what you come here to do. Twenty destinations only read as
    /// twenty places if each can say what it does that its neighbours do not.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>The profession ladders trained here. Every profession has exactly one station
    /// and every station has at least one profession — both enforced by the validator, so a
    /// new profession cannot ship without a way to reach it.</summary>
    public IReadOnlyList<string> Professions { get; init; } = Array.Empty<string>();

    /// <summary>Crafting action ids this station's bench offers. An action may appear at more
    /// than one station (Grind is ungated: a mortar at the Apothecary, a mill at the
    /// Workbench), and a station with none simply shows no bench.</summary>
    [JsonPropertyName("crafting_actions")]
    public IReadOnlyList<string> CraftingActions { get; init; } = Array.Empty<string>();

    /// <summary>Equipment blueprint ids that can be assembled here, by what the blueprint is
    /// mostly made of — a Longsword at the Forge, a Vest at the Tannery or the Loom.</summary>
    public IReadOnlyList<string> Blueprints { get; init; } = Array.Empty<string>();

    /// <summary>Identity-system crafting actions (<see cref="VerbActionDefinition"/>) offered
    /// here — the new bench, routing exactly as <see cref="CraftingActions"/> does for the
    /// outgoing one. Both lists coexist through the migration; the old one dies with its
    /// engine (Phase 7).</summary>
    [JsonPropertyName("verb_actions")]
    public IReadOnlyList<string> VerbActions { get; init; } = Array.Empty<string>();

    /// <summary>The profession whose shelf this station sorts onto in the Hideout — the first
    /// one listed. A station hosting several is filed under the one it is named for.</summary>
    public string PrimaryProfessionId => Professions.Count > 0 ? Professions[0] : string.Empty;

    /// <summary>True when the material-transformation bench is worth drawing here.</summary>
    public bool HasBench => CraftingActions.Count > 0;

    /// <summary>True when something can be assembled into equipment here.</summary>
    public bool HasAssembly => Blueprints.Count > 0;

    /// <summary>True if this station trains <paramref name="professionId"/>.</summary>
    public bool Hosts(string professionId) =>
        Professions.Contains(professionId, StringComparer.Ordinal);
}
