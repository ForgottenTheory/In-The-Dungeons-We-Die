using System.Text;
using Dungeons.Content;
using Dungeons.Crafting.Identity;

namespace Dungeons.Presentation;

/// <summary>
/// How much of a material's reading the player has earned the right to read. Each step is a
/// strictly larger view of the <em>same</em> reading — Assay never changes what a material is,
/// only how much of it is legible (D45/D48: information only, never capability).
/// </summary>
public enum AssayDepth
{
    /// <summary>Name, open stakes and the overfill word — what anyone can see.</summary>
    Superficial,

    /// <summary>Slots, condition, workmanship — how much material there is to work with.</summary>
    Vessel,

    /// <summary>Whether anything sleeps in it at all.</summary>
    Latency,

    /// <summary>Which identities sleep in it — what Reveal could wake.</summary>
    Latents,

    /// <summary>The signature profile as leanings in words (D53).</summary>
    Leanings,

    /// <summary>What its identities would guarantee on gear — reading potential.</summary>
    Potential,
}

/// <summary>
/// What Assay uncovers about a material, in reveal order (the Phase 6 re-aim, D45/D48).
/// Active identities and the overfill word are never gated: identities are legible by design
/// (D42), and chosen risk must be visible wherever a material is offered.
/// </summary>
public enum IdentityAssayFacet
{
    Vessel,
    Latency,
    LatentNames,
    Leanings,
    Potential,
}

/// <summary>Assay level thresholds. One place, so the reveal ladder is legible as a ladder.</summary>
public static class AssayTuning
{
    public const int VesselLevel = 10;
    public const int LatencyLevel = 25;
    public const int LatentsLevel = 45;
    public const int LeaningsLevel = 65;
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
/// the material they already had.</para>
/// </summary>
public static class AssayLens
{
    public static AssayDepth DepthFor(int assayLevel) => assayLevel switch
    {
        >= AssayTuning.PotentialLevel => AssayDepth.Potential,
        >= AssayTuning.LeaningsLevel => AssayDepth.Leanings,
        >= AssayTuning.LatentsLevel => AssayDepth.Latents,
        >= AssayTuning.LatencyLevel => AssayDepth.Latency,
        >= AssayTuning.VesselLevel => AssayDepth.Vessel,
        _ => AssayDepth.Superficial,
    };

    public static bool Reveals(AssayDepth depth, IdentityAssayFacet facet) => facet switch
    {
        IdentityAssayFacet.Vessel => depth >= AssayDepth.Vessel,
        IdentityAssayFacet.Latency => depth >= AssayDepth.Latency,
        IdentityAssayFacet.LatentNames => depth >= AssayDepth.Latents,
        IdentityAssayFacet.Leanings => depth >= AssayDepth.Leanings,
        IdentityAssayFacet.Potential => depth >= AssayDepth.Potential,
        _ => false,
    };

    /// <summary>The Assay level at which <paramref name="facet"/> becomes legible.</summary>
    public static int LevelFor(IdentityAssayFacet facet) => facet switch
    {
        IdentityAssayFacet.Vessel => AssayTuning.VesselLevel,
        IdentityAssayFacet.Latency => AssayTuning.LatencyLevel,
        IdentityAssayFacet.LatentNames => AssayTuning.LatentsLevel,
        IdentityAssayFacet.Leanings => AssayTuning.LeaningsLevel,
        IdentityAssayFacet.Potential => AssayTuning.PotentialLevel,
        _ => 1,
    };

    public static string FacetLabel(IdentityAssayFacet facet) => facet switch
    {
        IdentityAssayFacet.Vessel => "Vessel",
        IdentityAssayFacet.Latency => "Latency",
        IdentityAssayFacet.LatentNames => "Latent",
        IdentityAssayFacet.Leanings => "Leanings",
        IdentityAssayFacet.Potential => "Potential",
        _ => "Identity",
    };

    /// <summary>
    /// A material through the lens: the open stakes and overfill word always; vessel,
    /// latency, latent names, leanings and potential each behind their rung, reading as a
    /// labelled <c>???</c> until earned. Revealed facets delegate to
    /// <see cref="IdentityMaterialReadings"/> — one voice, two surfaces.
    /// </summary>
    public static string IdentityMaterial(
        string materialName, IdentityMaterialState state, MergedSignatureProfile profile,
        ContentBundle content, AssayDepth depth)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(content);

        var builder = new StringBuilder();
        builder.Append(materialName).Append(" — ")
            .Append(IdentityMaterialReadings.StakeNames(state, content));

        if (state.IsCarrier)
        {
            builder.AppendLine();
            builder.Append("A prepared carrier — delivers its full depth on Transfer");
        }

        AppendFacet(builder, IdentityAssayFacet.Vessel, depth,
            () => IdentityMaterialReadings.VesselPhrase(state));

        AppendFacet(builder, IdentityAssayFacet.Latency, depth,
            () => state.Latent.Count > 0 ? "something sleeps in this" : "nothing sleeps in it");

        // Naming what sleeps only matters while something does — a revealed empty list would
        // repeat what Latency already said.
        if (state.Latent.Count > 0 || !Reveals(depth, IdentityAssayFacet.LatentNames))
        {
            AppendFacet(builder, IdentityAssayFacet.LatentNames, depth,
                () => string.Join(", ", state.Latent.Select(id =>
                    content.Identities.TryGetById(id, out var identity) ? identity.Name : id))
                    + " — Reveal can wake it");
        }

        AppendFacet(builder, IdentityAssayFacet.Leanings, depth,
            () => IdentityMaterialReadings.LeaningsPhrase(profile, content));

        AppendFacet(builder, IdentityAssayFacet.Potential, depth,
            () => IdentityMaterialReadings.PotentialPhrase(state, content));

        return builder.ToString();
    }

    private static void AppendFacet(
        StringBuilder builder, IdentityAssayFacet facet, AssayDepth depth, Func<string> revealed)
    {
        builder.AppendLine();
        builder.Append(FacetLabel(facet)).Append(": ");
        builder.Append(Reveals(depth, facet)
            ? revealed()
            : $"{AssayTuning.Redacted}  (Assay {LevelFor(facet)})");
    }
}
