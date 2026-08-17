namespace Dungeons.Presentation;

/// <summary>
/// Thresholds for the player-facing semantic vocabulary (docs/presentation-architecture.md §2).
///
/// <para><b>Display-only by hard rule (D30):</b> nothing in this class may feed identity,
/// quantization, naming or the reaction algebra. The tier bands here deliberately sit close to
/// the naming ladders' four word-bands so the display state and a material's name never argue,
/// but they are separate on purpose — <c>QuantizationTuning</c> is identity and must never
/// read presentation code.</para>
/// </summary>
public static class PresentationTuning
{
    // ---- PropertyTier bands over the 0–100 scale (upper bounds, inclusive) ------------------
    public const double TraceCeiling = 15.0;
    public const double LowCeiling = 35.0;
    public const double ModerateCeiling = 60.0;
    public const double StrongCeiling = 85.0;

    /// <summary>Net movement at or under this reads as Steady rather than a trend — a craft
    /// preview full of ±0.4 arrows would be noise pretending to be information.</summary>
    public const double SteadyWindow = 1.5;

    // ---- Workability wear words (the player never sees the number outside Advanced) -----------
    public const int FreshFloor = 90;
    public const int SturdyFloor = 60;
    public const int WornFloor = 30;

    // ---- RiskBand boundaries (§3: SAFE · COSTLY · STRAINED · PERILOUS · DESTROYS) -----------
    /// <summary>Expected workability cost at or above this reads COSTLY even with no risk.</summary>
    public const double CostlyCost = 10.0;

    /// <summary>Projected workability at or under this reads STRAINED — the §6.2c "below ~25 the
    /// projection widens into a chance" band, named before the chance turns nonzero.</summary>
    public const int StressedWorkability = 25;

    // ---- Trait proximity (the §2E "something unusual is close" hint) -------------------------
    /// <summary>A trait reads as nearby only while at most this many conditions are unmet…</summary>
    public const int ProximityMaxUnmet = 2;

    /// <summary>…and no single deficit is wider than this window.</summary>
    public const double ProximityWindow = 20.0;

    // ---- stat_map read-weight bands (slot readings, §2E) -------------------------------------
    public const double HeavyReadWeight = 0.6;
    public const double ModerateReadWeight = 0.3;

    // ---- trait expression bands (trait expression preview) --------------------------------------------
    public const double FullApertureFloor = 0.8;
    public const double PartialApertureFloor = 0.4;

    // ---- crafting action severity bands ---------------------------------------------------------------
    public const double GentleSeverity = 0.30;
    public const double FirmSeverity = 0.45;
    public const double ForcefulSeverity = 0.60;

    /// <summary>How many leading properties a material reading shows before the rest is noise.</summary>
    public const int LeadingPropertyCount = 4;
}
