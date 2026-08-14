using Dungeons.Content;

namespace Dungeons.Crafting;

public enum ExperimentFailure
{
    None,
    NoMatch,
    MissingInputs,
    ProfessionTooLow,
}

/// <summary>The authoritative result of a crafting experiment.</summary>
public sealed class ExperimentOutcome
{
    public required bool Success { get; init; }
    public ExperimentFailure Failure { get; init; } = ExperimentFailure.None;

    public string? InteractionId { get; init; }
    public string? ResultItemId { get; init; }
    public int ResultQuantity { get; init; }
    public bool WasNewDiscovery { get; init; }

    /// <summary>Properties carried by the produced material, for feedback.</summary>
    public IReadOnlyList<MaterialProperty> ResultProperties { get; init; } = Array.Empty<MaterialProperty>();

    /// <summary>The profession that fell short, when <see cref="Failure"/> is ProfessionTooLow.</summary>
    public string? UnmetProfessionId { get; init; }
    public int UnmetRequiredLevel { get; init; }

    public static ExperimentOutcome Failed(ExperimentFailure failure, string? interactionId = null,
        string? unmetProfessionId = null, int unmetRequiredLevel = 0) =>
        new()
        {
            Success = false,
            Failure = failure,
            InteractionId = interactionId,
            UnmetProfessionId = unmetProfessionId,
            UnmetRequiredLevel = unmetRequiredLevel,
        };
}
