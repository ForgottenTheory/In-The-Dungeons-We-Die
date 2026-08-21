using System.Text.Json.Serialization;
using Dungeons.Crafting.Identity;
using Dungeons.Items;

namespace Dungeons.Content;

/// <summary>
/// One crafting action of the identity system — what the player actually clicks
/// (docs/transformation-verbs.md §1, D47): <c>verb + parameters + gates + costs + fiction
/// name</c>. Smithing's "Smelt" and the mortar's "Grind" are both the Process verb with
/// different data; the forge's "Alloy" is Fuse wearing its fiction. The verbs are code with
/// one executor each (<see cref="CraftVerb"/>); everything here is authoring.
///
/// <para><b>Gates scope domains, never permissions-to-exist</b> (D48): a substrate tag gate
/// is how Smithing's actions work metal and Tailoring's work cloth; an identity scope is how
/// Runecrafting works magical identities on <em>any</em> substrate. Hosting stations route
/// (<see cref="Dungeons.Hideout.StationDefinition"/>); the gates live here and move with the
/// action, exactly like the old processes.</para>
/// </summary>
public sealed record VerbActionDefinition : IDefinition
{
    /// <summary>Stable id, <c>craft.&lt;slug&gt;</c> (e.g. <c>craft.smelt_iron</c>).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The fiction name the player sees — "Smelt", "Prospect the Vein", "Alloy".</summary>
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    /// <summary>Which of the ten executors runs this action.</summary>
    public CraftVerb Verb { get; init; }

    /// <summary>Gate profession id, or empty for an ungated action (the Grind precedent).
    /// Hosting a station never changes this (D48: where you stand, never whether you may).</summary>
    public string Profession { get; init; } = string.Empty;

    [JsonPropertyName("required_level")]
    public int RequiredLevel { get; init; }

    /// <summary>Profession XP one run awards (migration Phase 5 — bench work trains). Paid
    /// whenever the work actually happened: success, fracture, and destruction alike (the
    /// gamble was taken; the hand learned). Gate and engine refusals pay nothing. A
    /// profession-gated action must author a positive value (validated).</summary>
    public int Experience { get; init; }

    /// <summary>Any-of tag gate on the substrate — the domain scoping (Smithing:
    /// <c>form:metal</c>). Empty means any migrated material.</summary>
    [JsonPropertyName("substrate_tags")]
    public IReadOnlyList<string> SubstrateTags { get; init; } = Array.Empty<string>();

    /// <summary>Exact-material gate, for the actions whose fiction is one conversion
    /// ("Smelt Iron"). Matches the substrate's definition <b>or its primary provenance
    /// root</b> — which is why smelting Dense Iron Ore into a Dense ingot needs no second
    /// action. Combines with <see cref="SubstrateTags"/>; empty means no exact gate.</summary>
    [JsonPropertyName("substrate_id")]
    public string? SubstrateId { get; init; }

    /// <summary>Which identities this action may target — Runecrafting's lever: Transfer and
    /// Develop scoped to the magical identities on any substrate. Empty means any identity;
    /// only meaningful on the identity-targeting verbs (validated).</summary>
    [JsonPropertyName("identity_scope")]
    public IReadOnlyList<string> IdentityScope { get; init; } = Array.Empty<string>();

    /// <summary>Process only: the authored definition the substrate converts into. One action
    /// per conversion, the same authoring grain the profession actions already use — mundane
    /// conversion is allowed to be explicit; emergent outcomes never are.</summary>
    public string? Output { get; init; }

    /// <summary>Item costs beyond the consumed sources — Restore's repair feedstock, Expand's
    /// catalysts. Consumed on commit by the application layer.</summary>
    [JsonPropertyName("extra_costs")]
    public IReadOnlyList<ItemStack> ExtraCosts { get; init; } = Array.Empty<ItemStack>();
}
