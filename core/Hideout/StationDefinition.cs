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

    /// <summary>The identity bench's crafting actions (<see cref="VerbActionDefinition"/>)
    /// offered here. An action may appear at more than one station; a station with none
    /// simply shows no bench.</summary>
    [JsonPropertyName("verb_actions")]
    public IReadOnlyList<string> VerbActions { get; init; } = Array.Empty<string>();

    /// <summary>True when this station hosts the identity forge. Forms need no per-station
    /// routing — the forge offers every migrated form (Phase 7, D54).</summary>
    [JsonPropertyName("has_assembly")]
    public bool HasAssembly { get; init; }

    /// <summary>The profession whose shelf this station sorts onto in the Hideout — the first
    /// one listed. A station hosting several is filed under the one it is named for.</summary>
    public string PrimaryProfessionId => Professions.Count > 0 ? Professions[0] : string.Empty;

    /// <summary>True if this station trains <paramref name="professionId"/>.</summary>
    public bool Hosts(string professionId) =>
        Professions.Contains(professionId, StringComparer.Ordinal);
}
