using Dungeons.Crafting;

namespace Dungeons.Presentation;

/// <summary>
/// §3 of docs/presentation-architecture.md — the risk word the pre-commit panel leads with.
/// The §6.2c fairness guarantees are unchanged underneath: DESTROYS is stated outright and
/// never as a percentage; PERILOUS shows the percentage; everything gentler is a cost word.
/// </summary>
public enum RiskBand
{
    Safe,
    Costly,
    Strained,
    Perilous,
    Destroys,
}

public static class Risk
{
    public static RiskBand Of(WorkabilityProjection workability)
    {
        ArgumentNullException.ThrowIfNull(workability);

        return workability.IsCertainDestruction ? RiskBand.Destroys
            : workability.IsAtRisk ? RiskBand.Perilous
            : workability.ProjectedWorkability <= PresentationTuning.StressedWorkability ? RiskBand.Strained
            : workability.ExpectedCost >= PresentationTuning.CostlyCost ? RiskBand.Costly
            : RiskBand.Safe;
    }

    public static string Word(RiskBand band) => band switch
    {
        RiskBand.Safe => "SAFE",
        RiskBand.Costly => "COSTLY",
        RiskBand.Strained => "STRAINED",
        RiskBand.Perilous => "PERILOUS",
        _ => "DESTROYS",
    };
}
