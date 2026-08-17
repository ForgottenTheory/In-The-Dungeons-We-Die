namespace Dungeons.Crafting;

/// <summary>
/// Why a craft cannot proceed, in the player's language — the one piece of craft wording that
/// was always semantic. The rest of the old numeric formatting moved under D30: the
/// player-facing voice is <c>Dungeons.Presentation.SemanticFormat</c>, the numeric voice is
/// <c>Dungeons.Presentation.AdvancedFormat</c> (docs/presentation-architecture.md).
/// </summary>
public static class CraftFormat
{
    /// <summary>Why a craft cannot proceed, in the player's language.</summary>
    public static string Failure(CraftFailure failure) => failure switch
    {
        CraftFailure.None => string.Empty,
        CraftFailure.ProfessionTooLow => "Your skill is not equal to this process yet.",
        CraftFailure.SubstrateRejected => "This process cannot work that material.",
        CraftFailure.MissingInputs => "You do not have the materials.",
        CraftFailure.NoReagents => "Choose at least one reagent.",
        CraftFailure.InvalidQuantity => "Choose how many to craft.",
        CraftFailure.UnknownProcess or CraftFailure.UnknownSubstrate
            or CraftFailure.UnknownReagent or CraftFailure.UnknownCatalyst => "Unknown material or process.",
        _ => "Nothing happens.",
    };
}
