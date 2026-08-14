namespace Dungeons.Characters.Rules;

/// <summary>
/// "Of Unreasonable Confidence" — grants a significant all-round bonus while at or
/// near full Health, which vanishes the moment the character is meaningfully
/// wounded. A stateful rule breaker: the same character has different effective
/// attributes depending on live Health (docs/classes.md §7).
/// </summary>
public sealed class UnreasonableConfidenceRule : ICharacterRule
{
    public const string Id = "rule.unreasonable_confidence";

    /// <summary>At or above this health fraction the bonus applies.</summary>
    public const double HealthThreshold = 0.9;

    /// <summary>Bonus added to every attribute while healthy.</summary>
    public const int Bonus = 2;

    public string RuleId => Id;

    public string Description =>
        $"While Health ≥ {HealthThreshold:P0}, gain +{Bonus} to all attributes; lost when wounded below it.";

    public IEnumerable<AttributeBonus> GetDynamicAttributeBonuses(CharacterSnapshot snapshot)
    {
        if (snapshot.HealthFraction < HealthThreshold)
            yield break;

        foreach (var attribute in AttributeTypes.All)
            yield return new AttributeBonus(attribute, Bonus);
    }
}
