namespace Dungeons.Presentation;

/// <summary>
/// The five qualitative states of the player crafting language (D30;
/// docs/presentation-architecture.md §2B). Neutral words by decision — the flavourful naming
/// ladders (Warmed → Emberlit → Cindered → Searing) remain the <i>naming</i> voice, because a
/// reading vocabulary has to be learnable across 21 properties at a glance and 21 × 4 unique
/// flavour words is a lexicon, not a grammar.
/// </summary>
public enum PropertyTier
{
    None,
    Trace,
    Low,
    Moderate,
    Strong,
    Extreme,
}

/// <summary>The one shared tier function every surface reads through, so "Strong" always means
/// the same thing. Display-only (D30): never an input to identity, naming or the algebra.</summary>
public static class Tiers
{
    public static PropertyTier Of(double value) => value switch
    {
        <= 0 => PropertyTier.None,
        <= PresentationTuning.TraceCeiling => PropertyTier.Trace,
        <= PresentationTuning.LowCeiling => PropertyTier.Low,
        <= PresentationTuning.ModerateCeiling => PropertyTier.Moderate,
        <= PresentationTuning.StrongCeiling => PropertyTier.Strong,
        _ => PropertyTier.Extreme,
    };

    public static string Word(PropertyTier tier) => tier switch
    {
        PropertyTier.Trace => "Trace",
        PropertyTier.Low => "Low",
        PropertyTier.Moderate => "Moderate",
        PropertyTier.Strong => "Strong",
        PropertyTier.Extreme => "Extreme",
        _ => "None",
    };

    /// <summary>§2C — the at-a-glance intensity meter: ●●●○○.</summary>
    public static string Pips(PropertyTier tier)
    {
        var filled = (int)tier;
        return new string('●', filled) + new string('○', 5 - filled);
    }

    /// <summary>Integrity as a wear word — Fresh · Sturdy · Worn · Fragile. The number stays
    /// in Advanced; the player reasons in wear, not arithmetic.</summary>
    public static string WearWord(int integrity) => integrity switch
    {
        >= PresentationTuning.FreshFloor => "Fresh",
        >= PresentationTuning.SturdyFloor => "Sturdy",
        >= PresentationTuning.WornFloor => "Worn",
        > 0 => "Fragile",
        _ => "Destroyed",
    };
}
