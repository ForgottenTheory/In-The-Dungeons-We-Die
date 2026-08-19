using Dungeons.Items;

namespace Dungeons.Professions;

/// <summary>
/// Something active play notices and passive play never does: a richer vein, a shape under
/// the boat, an unattended satchel, an unmarked side path. Discovering one does not collect
/// it — it is <em>offered</em>, and the player decides whether the extra time
/// (<see cref="ExtraIntervalTicks"/>, which inside a Realm is time spent not extracting) and
/// the <see cref="RiskWeight"/> are worth the payoff.
///
/// <para>This is the one mechanism behind every profession's active mode. Twenty different
/// minigames would have been twenty balance surfaces and twenty UIs; the same three fields
/// read completely differently per profession because the <see cref="Prompt"/>, the cost and
/// the payoff are content. Opportunities are nested inside their action rather than being a
/// top-level store, because one only ever exists in the context of the action that surfaces
/// it (docs/professions.md §4).</para>
/// </summary>
public sealed class ProfessionOpportunityDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    /// <summary>The player-facing offer, in gameplay language. This is the decision.</summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>Base chance for one active attempt to surface this, before mastery and performance.</summary>
    public double DiscoveryChance { get; init; }

    /// <summary>
    /// Mastery in the surfacing action below which this offer does not exist. Zero — the default,
    /// and what almost every opportunity carries — means anyone can stumble onto it.
    ///
    /// <para><b>This is the action-specific unlock, and it is an option rather than a
    /// percentage.</b> Mastery already makes offers <em>likelier</em>; a gate makes some of them
    /// <em>possible</em>, so a thousand swings at one rock face buys a different list of things
    /// that can happen rather than a better number. Not rolled at all below the gate — the same
    /// structural trick the active/passive seam uses, so "a novice cannot find this" is a fact
    /// about the code rather than a very small probability.</para>
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("required_mastery")]
    public int RequiredMasteryLevel { get; init; }

    /// <summary>Time pursuing costs on top of the action itself — the price of the decision.</summary>
    public int ExtraIntervalTicks { get; init; } = 100;

    /// <summary>Chance in [0, 1] that the pursuit comes to nothing (the vein pinches out, the
    /// mark looks up). The time is spent either way; that is what makes it a gamble.</summary>
    public double RiskWeight { get; init; }

    /// <summary>Extra materials the pursuit consumes, beyond what the action already cost.</summary>
    public IReadOnlyList<ItemStack> Inputs { get; init; } = Array.Empty<ItemStack>();

    public IReadOnlyList<ItemStack> Outputs { get; init; } = Array.Empty<ItemStack>();
    public IReadOnlyList<ItemChance> BonusOutputs { get; init; } = Array.Empty<ItemChance>();

    public long Experience { get; init; }
}
