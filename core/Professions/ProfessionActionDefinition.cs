using System.Text.Json.Serialization;
using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Professions;

/// <summary>
/// Where an action's <see cref="ProfessionActionDefinition.RealmKnowledgeGain"/> lands.
/// Cartography's survey actions raise Realm Knowledge instead of inventing a second
/// map-progression currency (docs/professions.md §11).
/// </summary>
public sealed class RealmKnowledgeGain
{
    public string RealmId { get; init; } = string.Empty;
    public int Amount { get; init; }
}

/// <summary>
/// Data-driven profession activity: gathering (no inputs) or processing/crafting
/// (with inputs). Passive and active execution share this one definition
/// (docs/json-schema.md §9, docs/architecture.md §20).
/// </summary>
public sealed class ProfessionActionDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string ProfessionId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int RequiredLevel { get; init; } = 1;
    public int BaseIntervalTicks { get; init; } = 100;
    public long Experience { get; init; }

    /// <summary>
    /// Chance in [0, 1] that a completed attempt actually lands. Below 1 for the
    /// professions whose fiction is an attempt rather than a task — Hunting's prey bolts,
    /// Thieving's mark notices. Everything else leaves this at 1: a swung pickaxe always
    /// produces ore, and rolling for that would be noise, not tension.
    /// </summary>
    public double SuccessChance { get; init; } = 1.0;

    public IReadOnlyList<ItemStack> Inputs { get; init; } = Array.Empty<ItemStack>();
    public IReadOnlyList<ItemStack> Outputs { get; init; } = Array.Empty<ItemStack>();
    public IReadOnlyList<ItemChance> BonusOutputs { get; init; } = Array.Empty<ItemChance>();

    /// <summary>
    /// Things the work turns up that are not the work's product: a seed shaken loose from a
    /// felled tree, a grub in the bait tin, a coin in the silt. Rolled on any attempt that
    /// lands, in addition to <see cref="Outputs"/> and <see cref="BonusOutputs"/>.
    ///
    /// <para><b>Why this is not just more <see cref="BonusOutputs"/>.</b> Those are <em>more of
    /// the same work</em>, and mastery and active performance raise their odds — they are a
    /// profession-progression lever. A drop table is <em>something else entirely</em>: it does
    /// not scale with mastery, it can express weighted variety and quantity ranges, and one
    /// table is shared by every action that would otherwise copy the same six lines.</para>
    ///
    /// <para>The table is rolled with <c>active</c> or <c>passive</c> on the context, so an
    /// entry can be reachable only when the player is actually there doing the work — which is
    /// what keeps Realm gathering ahead of safe Hideout training without a second balance
    /// model.</para>
    /// </summary>
    [JsonPropertyName("loot_table")]
    public string? LootTableId { get; init; }

    /// <summary>Discoveries only active play can surface. Empty for most actions.</summary>
    public IReadOnlyList<ProfessionOpportunityDefinition> Opportunities { get; init; } =
        Array.Empty<ProfessionOpportunityDefinition>();

    /// <summary>Realm Knowledge this action teaches, if any (Cartography).</summary>
    public RealmKnowledgeGain? RealmKnowledgeGain { get; init; }
}
