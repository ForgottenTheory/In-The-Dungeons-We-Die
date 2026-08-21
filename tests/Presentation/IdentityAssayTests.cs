using Dungeons.Content;
using Dungeons.Crafting.Identity;
using Dungeons.Presentation;
using Xunit;

namespace Dungeons.Tests.Presentation;

/// <summary>
/// The Assay re-aim (Phase 6, D45/D48/D53): the ladder now opens vessel, latency, latent
/// names, leanings and potential — information only, computed identically at every level.
/// Active identities and the overfill word are never gated; themes are never visible at any
/// depth (§6.1).
/// </summary>
public class IdentityAssayTests
{
    private const string Vital = "identity.vital";
    private const string Dense = "identity.dense";
    private const string Storm = "identity.storm";

    [Fact]
    public void TheLadderOpensTheIdentityFacetsInOrder()
    {
        Assert.False(AssayLens.Reveals(AssayDepth.Superficial, IdentityAssayFacet.Vessel));
        Assert.True(AssayLens.Reveals(AssayDepth.Vessel, IdentityAssayFacet.Vessel));
        Assert.False(AssayLens.Reveals(AssayDepth.Vessel, IdentityAssayFacet.Latency));
        Assert.True(AssayLens.Reveals(AssayDepth.Latency, IdentityAssayFacet.Latency));
        Assert.False(AssayLens.Reveals(AssayDepth.Latency, IdentityAssayFacet.LatentNames));
        Assert.True(AssayLens.Reveals(AssayDepth.Latents, IdentityAssayFacet.LatentNames));
        Assert.False(AssayLens.Reveals(AssayDepth.Latents, IdentityAssayFacet.Leanings));
        Assert.True(AssayLens.Reveals(AssayDepth.Leanings, IdentityAssayFacet.Leanings));
        Assert.False(AssayLens.Reveals(AssayDepth.Leanings, IdentityAssayFacet.Potential));
        Assert.True(AssayLens.Reveals(AssayDepth.Potential, IdentityAssayFacet.Potential));
    }

    [Fact]
    public void ActiveIdentitiesAndOverfillAreNeverGated()
    {
        // D42: identities are legible by design; §4 fairness: chosen risk shows wherever the
        // material is offered — even a Superficial reading says both.
        var state = new IdentityMaterialState
        {
            Identities = new[]
            {
                new IdentityStake(Dense, 1), new IdentityStake(Vital, 1), new IdentityStake(Storm, 1),
            },
            Capacity = 1,
        };

        var reading = Read(state, AssayDepth.Superficial);

        Assert.Contains("Dense", reading);
        Assert.Contains("Volatile", reading);
        Assert.Contains($"Vessel: {AssayTuning.Redacted}", reading);
    }

    [Fact]
    public void LatencyTellsExistenceBeforeNames()
    {
        var state = new IdentityMaterialState { Latent = new[] { Storm }, Capacity = 2 };

        var atReactive = Read(state, AssayDepth.Latency);
        Assert.Contains("something sleeps in this", atReactive);
        Assert.Contains($"Latent: {AssayTuning.Redacted}", atReactive);
        Assert.DoesNotContain("Storm", atReactive);

        var atTraits = Read(state, AssayDepth.Latents);
        Assert.Contains("Latent: Storm — Reveal can wake it", atTraits);
    }

    [Fact]
    public void NothingSleepingIsSaidOnceNotTwice()
    {
        var state = new IdentityMaterialState { Capacity = 2 };

        var reading = Read(state, AssayDepth.Potential);

        Assert.Contains("nothing sleeps in it", reading);
        Assert.DoesNotContain("Latent:", reading);
    }

    [Fact]
    public void LeaningsSpeakTheDecidedWords()
    {
        var profile = new MergedSignatureProfile(
            Array.Empty<WeightedLean>(),
            new[] { new WeightedLean("on_block", 0.8) },
            new[] { new WeightedLean("store", 0.6) },
            new[] { new WeightedLean("bulwark", 0.7) });
        var state = new IdentityMaterialState { Capacity = 1 };

        var below = Read(state, AssayDepth.Latents, profile);
        Assert.Contains($"Leanings: {AssayTuning.Redacted}", below);

        var revealed = Read(state, AssayDepth.Leanings, profile);
        Assert.Contains("leans toward On Block · Store work", revealed);
        Assert.Contains("favors Bulwark", revealed);
    }

    [Fact]
    public void ThemesNeverShowAtAnyDepth()
    {
        // §6.1: themes are scoring metadata. A profile heavy with them reads exactly as one
        // without them.
        var themed = new MergedSignatureProfile(
            new[] { new WeightedLean("renewal", 1.0) },
            Array.Empty<WeightedLean>(), Array.Empty<WeightedLean>(), Array.Empty<WeightedLean>());
        var state = new IdentityMaterialState { Capacity = 1 };

        var reading = Read(state, AssayDepth.Potential, themed);

        Assert.DoesNotContain("renewal", reading, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no particular leanings", reading);
    }

    [Fact]
    public void PotentialQuotesTheFloorRule()
    {
        var state = new IdentityMaterialState
        {
            Identities = new[] { new IdentityStake(Vital, 1) }, Capacity = 1,
        };

        var reading = Read(state, AssayDepth.Potential);

        Assert.Contains("on gear, promises Vitality", reading);
    }

    [Fact]
    public void ACarrierReadsAsOneAtEveryDepth()
    {
        var state = new IdentityMaterialState
        {
            Identities = new[] { new IdentityStake(Vital, 2) },
            Capacity = 1,
            IsCarrier = true,
        };

        Assert.Contains("A prepared carrier", Read(state, AssayDepth.Superficial));
    }

    // --- Harness -------------------------------------------------------------

    private static string Read(
        IdentityMaterialState state, AssayDepth depth, MergedSignatureProfile? profile = null) =>
        AssayLens.IdentityMaterial(
            "Test Stock", state, profile ?? MergedSignatureProfile.Neutral, Content(), depth);

    private static ContentBundle Content() => new()
    {
        Identities = TestPaths.LoadStore<IdentityDefinition>("identities"),
        SignatureTriggers = TestPaths.LoadStore<SignatureTriggerDefinition>("signature_triggers"),
        SignatureBehaviors = TestPaths.LoadStore<SignatureBehaviorDefinition>("signature_behaviors"),
        SignaturePayloads = TestPaths.LoadStore<SignaturePayloadDefinition>("signature_payloads"),
    };
}
