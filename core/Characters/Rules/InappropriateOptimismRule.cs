namespace Dungeons.Characters.Rules;

/// <summary>
/// "Of Inappropriate Optimism" — the mirror of Unreasonable Confidence: grants a
/// sharp offensive bonus only while badly wounded, rewarding dangerous play
/// (docs/classes.md §7). Demonstrates that two characters differing only by suffix
/// behave in opposite ways under the same Health changes.
/// </summary>
public sealed class InappropriateOptimismRule : ICharacterRule
{
    public const string Id = "rule.inappropriate_optimism";

    /// <summary>At or below this health fraction the bonus applies.</summary>
    public const double HealthThreshold = 0.34;

    public const int StrengthBonus = 3;
    public const int DexterityBonus = 3;

    public string RuleId => Id;

    public string Description =>
        $"While Health ≤ {HealthThreshold:P0}, gain +{StrengthBonus} Strength and +{DexterityBonus} Dexterity.";

    public IEnumerable<AttributeBonus> GetDynamicAttributeBonuses(CharacterSnapshot snapshot)
    {
        if (snapshot.HealthFraction > HealthThreshold)
            yield break;

        yield return new AttributeBonus(AttributeType.Strength, StrengthBonus);
        yield return new AttributeBonus(AttributeType.Dexterity, DexterityBonus);
    }
}
