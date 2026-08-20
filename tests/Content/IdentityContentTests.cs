using Dungeons.Content;
using Dungeons.Events;
using Xunit;

namespace Dungeons.Tests.Content;

/// <summary>
/// The identity-system content types (migration Phase 1 — docs/identity-foundation.md,
/// D42–D48): the shipped vocabulary must match the approved design exactly, and every
/// validator rule must catch its deliberately-broken content. The pins here express design
/// decisions, not code paths — changing the roster or the behavior set should require
/// changing a test that names the decision it came from.
/// </summary>
public class IdentityContentTests
{
    // --- Shipped vocabulary pins ---------------------------------------------

    [Fact]
    public void TheRosterIsExactlyD44s24()
    {
        // The 24 identities approved in D44. A new identity is a design decision (a new
        // D-number), never a casual content addition — this pin is what makes that true.
        var expected = new[]
        {
            "identity.arcane", "identity.balanced", "identity.blighted", "identity.charmed",
            "identity.corrosive", "identity.dense", "identity.earthen", "identity.ember",
            "identity.frost", "identity.hardened", "identity.keen", "identity.leeching",
            "identity.pure", "identity.radiant", "identity.resonant", "identity.serrated",
            "identity.storm", "identity.swift", "identity.thorned", "identity.umbral",
            "identity.venomous", "identity.verdant", "identity.vital", "identity.warded",
        };

        var shipped = TestPaths.LoadStore<IdentityDefinition>("identities")
            .GetAll().Select(identity => identity.Id).OrderBy(id => id, StringComparer.Ordinal);

        Assert.Equal(expected, shipped);
    }

    [Fact]
    public void OnlyMachineryBackedBehaviorsShip()
    {
        // D30's fence on the grammar: a behavior ships only when the machinery its assembler
        // composes exists. The three designed-but-parked verbs are named here so shipping one
        // is an act of intent — delete it from the absent list when its effect kind lands
        // (detonate → consumeStatus, spread → area status application, bloom → delayed effects).
        var expected = new[]
        {
            "afflict", "amplify", "convert", "direct", "drain", "echo",
            "exchange", "imbue", "retaliate", "store", "sustain",
        };
        var parkedUntilMachineryExists = new[] { "detonate", "spread", "bloom" };

        var shipped = TestPaths.LoadStore<SignatureBehaviorDefinition>("signature_behaviors")
            .GetAll().Select(behavior => behavior.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();

        Assert.Equal(expected, shipped);
        foreach (var parked in parkedUntilMachineryExists)
            Assert.DoesNotContain(parked, shipped);
    }

    [Fact]
    public void EveryShippedTriggerBindsToAPublishedEventOrIsStanding()
    {
        var triggers = TestPaths.LoadStore<SignatureTriggerDefinition>("signature_triggers").GetAll();

        Assert.NotEmpty(triggers);
        foreach (var trigger in triggers)
        {
            if (trigger.Standing)
                Assert.Null(trigger.Event);
            else
                Assert.Contains(trigger.Event!, GameEvents.All);
        }
    }

    // --- Validator rules, each proven to fire --------------------------------

    [Fact]
    public void AnUnknownIdentityReferenceOnAMaterialFails()
    {
        var bundle = BundleWithVocabulary();
        bundle.Materials.Add(MaterialCarrying(new IdentityGrant { Id = "identity.nonexistent" }));

        AssertProblem(bundle, "material_identity", "identity.nonexistent");
    }

    [Fact]
    public void AnIdentityRankOutsideTheRungsFails()
    {
        var bundle = BundleWithVocabulary();
        bundle.Materials.Add(MaterialCarrying(new IdentityGrant { Id = "identity.vital", Rank = 5 }));

        AssertProblem(bundle, "material_identity", "rank 5");
    }

    [Fact]
    public void ACapacityOutsideTheSlotRangeFails()
    {
        var bundle = BundleWithVocabulary();
        bundle.Materials.Add(new MaterialDefinition { Id = "material.test", Name = "Test", Capacity = 5 });

        AssertProblem(bundle, "material_identity", "capacity 5");
    }

    [Fact]
    public void AuthoringMoreActiveIdentitiesThanCapacityFails()
    {
        // Authored materials start Stable — overfill is play, not authoring (§10.3).
        var bundle = BundleWithVocabulary();
        bundle.Identities.Add(TestIdentity("identity.second"));
        bundle.Materials.Add(new MaterialDefinition
        {
            Id = "material.test",
            Name = "Test",
            Capacity = 1,
            Identities = new[]
            {
                new IdentityGrant { Id = "identity.vital" },
                new IdentityGrant { Id = "identity.second" },
            },
        });

        AssertProblem(bundle, "material_identity", "over a capacity");
    }

    [Fact]
    public void AnIdentityBothActiveAndLatentFails()
    {
        var bundle = BundleWithVocabulary();
        bundle.Materials.Add(new MaterialDefinition
        {
            Id = "material.test",
            Name = "Test",
            Identities = new[] { new IdentityGrant { Id = "identity.vital" } },
            Latent = new[] { "identity.vital" },
        });

        AssertProblem(bundle, "material_identity", "both active and latent");
    }

    [Fact]
    public void AProfileReferencingUnknownVocabularyFails()
    {
        var bundle = BundleWithVocabulary();
        bundle.Materials.Add(new MaterialDefinition
        {
            Id = "material.test",
            Name = "Test",
            SignatureProfile = new SignatureProfile
            {
                Themes = new[] { "no_such_theme" },
                FavoredTriggers = new[] { "no_such_trigger" },
                FavoredBehaviors = new[] { "no_such_behavior" },
            },
        });

        AssertProblem(bundle, "material_identity", "no_such_theme");
        AssertProblem(bundle, "material_identity", "no_such_trigger");
        AssertProblem(bundle, "material_identity", "no_such_behavior");
    }

    [Fact]
    public void ABaseStatOutsideZeroToTenFails()
    {
        var bundle = BundleWithVocabulary();
        bundle.Materials.Add(new MaterialDefinition
        {
            Id = "material.test",
            Name = "Test",
            Base = new MaterialBaseStats { Heft = 11 },
        });

        AssertProblem(bundle, "material_identity", "heft = 11");
    }

    [Fact]
    public void ATriggerWithAnUnknownEventFails()
    {
        var bundle = BundleWithVocabulary();
        bundle.SignatureTriggers.Add(new SignatureTriggerDefinition
        {
            Id = "on_wishing", Name = "On Wishing", Event = "WishGranted",
        });

        AssertProblem(bundle, "signature_trigger", "WishGranted");
    }

    [Fact]
    public void ATriggerDeclaringBothEventAndStandingFails()
    {
        var bundle = BundleWithVocabulary();
        bundle.SignatureTriggers.Add(new SignatureTriggerDefinition
        {
            Id = "confused", Name = "Confused", Event = GameEvents.Blocked, Standing = true,
        });

        AssertProblem(bundle, "signature_trigger", "exactly one");
    }

    [Fact]
    public void ADottedVocabularyIdFails()
    {
        // Grammar ids are bare keys like property names, so profiles read as designed.
        var bundle = BundleWithVocabulary();
        bundle.SignatureBehaviors.Add(new SignatureBehaviorDefinition
        {
            Id = "behavior.store", Name = "Store", Description = "x",
        });

        AssertProblem(bundle, "signature_behavior", "bare keys");
    }

    [Fact]
    public void AnUnknownClusterFails()
    {
        var bundle = BundleWithVocabulary();
        bundle.Identities.Add(new IdentityDefinition
        {
            Id = "identity.mysterious", Name = "Mysterious", Cluster = "cosmic", Description = "x",
        });

        AssertProblem(bundle, "identity", "cosmic");
    }

    // --- Harness -------------------------------------------------------------

    /// <summary>A bundle carrying one valid entry per identity-system registry, so the broken
    /// thing under test is the only broken thing of its kind.</summary>
    private static ContentBundle BundleWithVocabulary()
    {
        var bundle = new ContentBundle();
        bundle.Identities.Add(TestIdentity("identity.vital"));
        bundle.SignatureTriggers.Add(new SignatureTriggerDefinition
        {
            Id = "on_block", Name = "On Block", Event = GameEvents.Blocked, Description = "x",
        });
        bundle.SignatureBehaviors.Add(new SignatureBehaviorDefinition
        {
            Id = "store", Name = "Store", Description = "x",
        });
        bundle.SignatureThemes.Add(new SignatureThemeDefinition
        {
            Id = "renewal", Name = "Renewal", Description = "x",
        });
        return bundle;
    }

    private static IdentityDefinition TestIdentity(string id) => new()
    {
        Id = id, Name = "Test Identity", Cluster = "sustain", Description = "A test identity.",
    };

    private static MaterialDefinition MaterialCarrying(IdentityGrant grant) => new()
    {
        Id = "material.test", Name = "Test", Capacity = 2, Identities = new[] { grant },
    };

    private static void AssertProblem(ContentBundle bundle, string category, string messageFragment)
    {
        var problems = ContentValidator.Validate(bundle);
        Assert.Contains(problems, p => p.Category == category && p.Message.Contains(messageFragment));
    }
}
