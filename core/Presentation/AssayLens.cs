using System.Text;

namespace Dungeons.Presentation;

/// <summary>
/// How much of a material's reading the player has earned the right to read. Each step is a
/// strictly larger view of the <em>same</em> reading — Assay never changes what a material is,
/// only how much of it is legible (docs/professions.md §7, GDD §20).
/// </summary>
public enum AssayDepth
{
    /// <summary>Name and descriptor. "Hot metal", and nothing else.</summary>
    Superficial,

    /// <summary>Which qualities lead: the property strip.</summary>
    Composition,

    /// <summary>How it behaves under work: bonding, receptiveness, wear.</summary>
    Reactive,

    /// <summary>Which traits it carries, and their drawbacks.</summary>
    Traits,

    /// <summary>Its essence load and resonance, and whether the vessel is strained.</summary>
    Essence,

    /// <summary>What it could become: potential pressure, slot fit, modifier eligibility.</summary>
    Potential,
}

/// <summary>The five things a reading can hide, in the order Assay uncovers them.</summary>
public enum AssayFacet
{
    Identity,
    Composition,
    ReactiveBehaviour,
    Traits,
    Essence,
    Potential,
}

/// <summary>Assay level thresholds. One place, so the reveal ladder is legible as a ladder.</summary>
public static class AssayTuning
{
    public const int CompositionLevel = 10;
    public const int ReactiveLevel = 25;
    public const int TraitsLevel = 45;
    public const int EssenceLevel = 65;
    public const int PotentialLevel = 85;

    /// <summary>What an unrevealed line reads as. Deliberately not blank: the player should
    /// see that something is there and that Assay is what opens it.</summary>
    public const string Redacted = "???";
}

/// <summary>
/// The one place Assay level turns into "what may be shown".
///
/// <para>Assay is the profession that pays in comprehension: it never adds a point of damage,
/// it removes <c>???</c>. Because the underlying reading is computed the same way at every
/// level, a player who levels Assay is not getting a better item — they are finally reading
/// the item they already had.</para>
/// </summary>
public static class AssayLens
{
    public static AssayDepth DepthFor(int assayLevel) => assayLevel switch
    {
        >= AssayTuning.PotentialLevel => AssayDepth.Potential,
        >= AssayTuning.EssenceLevel => AssayDepth.Essence,
        >= AssayTuning.TraitsLevel => AssayDepth.Traits,
        >= AssayTuning.ReactiveLevel => AssayDepth.Reactive,
        >= AssayTuning.CompositionLevel => AssayDepth.Composition,
        _ => AssayDepth.Superficial,
    };

    public static bool Reveals(AssayDepth depth, AssayFacet facet) => facet switch
    {
        AssayFacet.Identity => true,
        AssayFacet.Composition => depth >= AssayDepth.Composition,
        AssayFacet.ReactiveBehaviour => depth >= AssayDepth.Reactive,
        AssayFacet.Traits => depth >= AssayDepth.Traits,
        AssayFacet.Essence => depth >= AssayDepth.Essence,
        AssayFacet.Potential => depth >= AssayDepth.Potential,
        _ => false,
    };

    /// <summary>The Assay level at which <paramref name="facet"/> becomes legible.</summary>
    public static int LevelFor(AssayFacet facet) => facet switch
    {
        AssayFacet.Identity => 1,
        AssayFacet.Composition => AssayTuning.CompositionLevel,
        AssayFacet.ReactiveBehaviour => AssayTuning.ReactiveLevel,
        AssayFacet.Traits => AssayTuning.TraitsLevel,
        AssayFacet.Essence => AssayTuning.EssenceLevel,
        AssayFacet.Potential => AssayTuning.PotentialLevel,
        _ => 1,
    };

    public static string FacetLabel(AssayFacet facet) => facet switch
    {
        AssayFacet.Composition => "Composition",
        AssayFacet.ReactiveBehaviour => "Reactive behaviour",
        AssayFacet.Traits => "Traits",
        AssayFacet.Essence => "Essence",
        AssayFacet.Potential => "Potential",
        _ => "Identity",
    };

    /// <summary>
    /// Renders a material reading through the lens: revealed facets read exactly as
    /// <see cref="SemanticFormat.Material"/> renders them, and everything above the player's
    /// depth reads as a labelled <c>???</c> with the level that would open it. Two voices
    /// would have drifted, so the revealed half delegates rather than re-formatting.
    /// </summary>
    public static string Material(MaterialReading reading, PropertyGlossary glossary, AssayDepth depth)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(glossary);

        if (depth >= AssayDepth.Essence)
            return SemanticFormat.Material(reading, glossary);

        var builder = new StringBuilder();
        builder.Append(reading.Name).Append(" — ").Append(reading.Descriptor);

        AppendFacet(builder, AssayFacet.Composition, depth, () =>
            reading.Leading.Count == 0
                ? "no leading quality"
                : string.Join(" · ", reading.Leading.Select(p =>
                    $"{glossary.Label(p.Property)} {Tiers.Word(p.Tier)} {Tiers.Pips(p.Tier)}")));

        AppendFacet(builder, AssayFacet.ReactiveBehaviour, depth, () =>
        {
            var receptive = reading.Receptive
                .Where(r => r.Tier >= PropertyTier.Low)
                .OrderByDescending(r => r.Tier)
                .ToList();
            var media = receptive.Count == 0
                ? "inert under every medium"
                : string.Join(" · ", receptive.Select(SemanticFormat.ReceptivenessPhrase));
            return $"{SemanticFormat.BondingPhrase(reading.Bonding)} · {media} · {Tiers.WearWord(reading.Workability)}";
        });

        AppendFacet(builder, AssayFacet.Traits, depth, () =>
            reading.Traits.Count == 0
                ? "no traits"
                : string.Join(" · ", reading.Traits.Select(t => t.Name)));

        AppendFacet(builder, AssayFacet.Essence, depth, () =>
            reading.Essence.Count == 0
                ? "no essence"
                : string.Join(" · ", reading.Essence.Select(e => $"{e.Name} {Tiers.Word(e.Tier)}")));

        return builder.ToString();
    }

    private static void AppendFacet(StringBuilder builder, AssayFacet facet, AssayDepth depth, Func<string> revealed)
    {
        builder.AppendLine();
        builder.Append(FacetLabel(facet)).Append(": ");
        builder.Append(Reveals(depth, facet)
            ? revealed()
            : $"{AssayTuning.Redacted}  (Assay {LevelFor(facet)})");
    }
}
