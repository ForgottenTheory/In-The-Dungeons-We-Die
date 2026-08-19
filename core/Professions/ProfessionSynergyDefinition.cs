using System.Text.Json.Serialization;
using Dungeons.Content;

namespace Dungeons.Professions;

/// <summary>
/// Progress in one place paying off in another: what a profession's <em>level</em> — or the sum
/// of every level the player has earned — is worth to some other profession's work.
///
/// <para><b>One type covers both hooks Phase 10 asked for.</b> A cross-profession bonus names a
/// <see cref="SourceProfession"/>; a global passive bonus leaves it null and reads the player's
/// <b>total</b> profession level instead. They are the same statement — "this much progress buys
/// this much benefit" — differing only in whose progress is counted, and two content types for
/// that would have been two validators, two loaders and two ways to author the same number.</para>
///
/// <para><b>The formula is the mastery ladder's, deliberately.</b> Below
/// <see cref="UnlockLevel"/> it is worth nothing; at or above it, <c>sourceLevel × PerLevel</c>,
/// capped at <see cref="Max"/> — the same three fields, read the same way
/// (<see cref="MasteryBenefitDefinition"/>). A second shape here would mean a balance pass has
/// to hold two rules in its head to answer one question.</para>
///
/// <para><b>Source and target must differ.</b> A synergy from a profession to itself is a
/// mastery rung wearing a different hat, and a self-amplifying one at that; the validator
/// refuses it.</para>
/// </summary>
public sealed class ProfessionSynergyDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;

    /// <summary>Which of the six quantities this pays into.</summary>
    public ProfessionBenefitKind Kind { get; init; }

    /// <summary>
    /// The profession whose level pays for this, or <b>null for the player's total profession
    /// level</b> — the always-on global bonus. Null is not "no source": it is the widest
    /// possible source, and it is what makes a global bonus something the player earns rather
    /// than a constant hidden in a tuning class.
    /// </summary>
    [JsonPropertyName("source")]
    public string? SourceProfession { get; init; }

    /// <summary>The profession that receives it, or null for every profession.</summary>
    [JsonPropertyName("target")]
    public string? TargetProfession { get; init; }

    /// <summary>The source level at which this switches on. Below it, worth nothing.</summary>
    [JsonPropertyName("unlock_level")]
    public int UnlockLevel { get; init; } = 1;

    /// <summary>Value gained per source level at or above <see cref="UnlockLevel"/>.</summary>
    [JsonPropertyName("per_level")]
    public double PerLevel { get; init; }

    /// <summary>The ceiling. Validated against what the source can actually reach.</summary>
    public double Max { get; init; }

    /// <summary>What this means, in the fiction. The player reads this on the profession page.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>True when this reads the player's total level rather than one profession's.</summary>
    public bool IsGlobalSource => SourceProfession is null;

    /// <summary>True when every profession receives this.</summary>
    public bool AppliesToEveryProfession => TargetProfession is null;

    /// <summary>Whether <paramref name="professionId"/> is one of this synergy's beneficiaries.</summary>
    public bool Benefits(string? professionId) =>
        AppliesToEveryProfession
        || (professionId is not null && string.Equals(TargetProfession, professionId, StringComparison.Ordinal));

    /// <summary>What this is worth at <paramref name="sourceLevel"/>.</summary>
    public double ValueAt(int sourceLevel) =>
        sourceLevel < UnlockLevel ? 0.0 : Math.Min(Max, sourceLevel * PerLevel);
}
