namespace Dungeons.Crafting.Identity;

/// <summary>
/// Which of the item-effect pipeline's output categories a sentence belongs to (D50's three,
/// plus the overfill drawback D45 §10.3 approved). Kept apart on the item so the player can
/// always tell the identity's promise from the roll from the rarity.
/// ⚠ Member names become save keys when instances persist — rename only with a migration.
/// </summary>
public enum ItemEffectCategory
{
    /// <summary>Guaranteed: the expression an active identity promises at its rank (D50
    /// category 1). Deterministic — no dice touched it.</summary>
    Floor,

    /// <summary>The workaday weighted layer, drawn from the scored candidate table.</summary>
    Generated,

    /// <summary>The special material/process-derived layer (§7, D43/D50): earned, not
    /// rolled-by-default, and coherent across its sentences.</summary>
    Signature,

    /// <summary>The cost of minting from a Volatile material (§10.3): a sentence aimed at
    /// the wearer. Chosen risk, like every identity-system loss.</summary>
    Drawback,
}

/// <summary>
/// One generated effect sentence — <i>trigger → behavior → payload</i> with its rolled
/// magnitude and chance (§7.1). This is what persists on a minted item: compact, stable ids
/// into the vocabulary registries, recompiled into live grants deterministically by
/// <see cref="SentenceAssemblers"/> whenever the item is read or worn — the same
/// store-the-roll-not-the-grant shape <c>RolledAffix</c> proved.
/// </summary>
public sealed record ItemEffectSentence(
    ItemEffectCategory Category,
    string TriggerId,
    string BehaviorId,
    string PayloadId,
    double Magnitude,
    double Chance,
    bool AfflictsWearer = false)
{
    /// <summary>Stable id for cooldown bookkeeping and log attribution. Two identically
    /// worded sentences on one item share it — acceptable while cooldowns are rare
    /// (provisional; revisit if shipped content ever cools sentences down).</summary>
    public string RuleId =>
        $"sentence.{Category}.{TriggerId}.{BehaviorId}.{PayloadId}".ToLowerInvariant();
}
