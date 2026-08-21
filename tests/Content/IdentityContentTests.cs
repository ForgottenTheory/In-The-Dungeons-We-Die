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

    // --- The payload registry (Phase 3, D50) ---------------------------------

    [Fact]
    public void ShippedPayloadsGiveEveryOwningIdentityExactlyOneFloor()
    {
        // D50 category 1: the floor is authored content, and it is singular. The validator
        // enforces this for any bundle; this pin proves the shipped starter set honors it.
        var payloads = TestPaths.LoadStore<SignaturePayloadDefinition>("signature_payloads").GetAll();

        Assert.NotEmpty(payloads);
        var owningIdentities = payloads.SelectMany(p => p.Families.Select(f => f.Identity)).Distinct();
        foreach (var identity in owningIdentities)
        {
            var floors = payloads.Where(p => p.Floor is not null && p.Families.Any(f => f.Identity == identity));
            Assert.Single(floors);
        }
    }

    [Fact]
    public void ADottedPayloadIdFails()
    {
        var bundle = BundleWithVocabulary();
        bundle.SignaturePayloads.Add(TestPayload(payload => payload with { Id = "payload.mending" }));

        AssertProblem(bundle, "signature_payload", "bare keys");
    }

    [Fact]
    public void APayloadWithNoFamilyFails()
    {
        var bundle = BundleWithVocabulary();
        bundle.SignaturePayloads.Add(TestPayload(payload => payload with { Families = Array.Empty<PayloadFamilyStake>() }));

        AssertProblem(bundle, "signature_payload", "orphan");
    }

    [Fact]
    public void APayloadFamilyOutsideTheRosterFails()
    {
        var bundle = BundleWithVocabulary();
        bundle.SignaturePayloads.Add(TestPayload(payload => payload with
        {
            Families = new[] { new PayloadFamilyStake { Identity = "identity.nonexistent" } },
        }));

        AssertProblem(bundle, "signature_payload", "identity.nonexistent");
    }

    [Fact]
    public void APayloadRungOutsideTheLadderFails()
    {
        var bundle = BundleWithVocabulary();
        bundle.SignaturePayloads.Add(TestPayload(payload => payload with
        {
            Families = new[] { new PayloadFamilyStake { Identity = "identity.vital", Rung = 5 } },
        }));

        AssertProblem(bundle, "signature_payload", "rung 5");
    }

    [Fact]
    public void AnUnknownBindingKindFails()
    {
        var bundle = BundleWithVocabulary();
        bundle.SignaturePayloads.Add(TestPayload(payload => payload with
        {
            Binding = new PayloadBinding { Kind = "wish" },
        }));

        AssertProblem(bundle, "signature_payload", "binding kind 'wish'");
    }

    [Fact]
    public void ABindingToAnUnknownModifierKeyFails()
    {
        // The D30 fence in its payload form: the binding must name machinery that resolves.
        var bundle = BundleWithVocabulary();
        bundle.SignaturePayloads.Add(TestPayload(payload => payload with
        {
            Binding = new PayloadBinding { Kind = "modifier", Key = "combat.wish.granted" },
        }));

        AssertProblem(bundle, "signature_payload", "combat.wish.granted");
    }

    [Fact]
    public void ABindingScopeDisagreeingWithTheKeysDimensionFails()
    {
        var bundle = BundleWithVocabulary();
        bundle.SignaturePayloads.Add(TestPayload(payload => payload with
        {
            Binding = new PayloadBinding { Kind = "modifier", Key = "resource.max_health", Scope = "lane:heat" },
        }));

        AssertProblem(bundle, "signature_payload", "scoped by");
    }

    [Fact]
    public void ABindingToAnUnknownStatusFails()
    {
        var bundle = BundleWithVocabulary();
        bundle.SignaturePayloads.Add(TestPayload(payload => payload with
        {
            Binding = new PayloadBinding { Kind = "status", Key = "status.wishful" },
        }));

        AssertProblem(bundle, "signature_payload", "status.wishful");
    }

    [Fact]
    public void AMagnitudeBearingBindingWithoutARangeFails()
    {
        var bundle = BundleWithVocabulary();
        bundle.SignaturePayloads.Add(TestPayload(payload => payload with { Range = Array.Empty<double>() }));

        AssertProblem(bundle, "signature_payload", "needs a [lo, hi] range");
    }

    [Fact]
    public void AnInvertedRangeFails()
    {
        var bundle = BundleWithVocabulary();
        bundle.SignaturePayloads.Add(TestPayload(payload => payload with { Range = new[] { 10.0, 2.0 } }));

        AssertProblem(bundle, "signature_payload", "lo ≤ hi");
    }

    [Fact]
    public void AFloorPayloadAboveRungOneFails()
    {
        // The floor is what carrying the identity at all promises — it cannot sit behind
        // development the identity may not have.
        var bundle = BundleWithVocabulary();
        bundle.SignaturePayloads.Add(TestPayload(payload => payload with
        {
            Families = new[] { new PayloadFamilyStake { Identity = "identity.vital", Rung = 2 } },
            Floor = new PayloadFloorSentence { Trigger = "on_block", Behavior = "store" },
        }));

        AssertProblem(bundle, "signature_payload", "rung 1");
    }

    [Fact]
    public void AnIdentityOwningPayloadsWithoutAFloorFails()
    {
        var bundle = BundleWithVocabulary();
        bundle.SignaturePayloads.Add(TestPayload(payload => payload));

        AssertProblem(bundle, "signature_payload", "0 floor expressions");
    }

    [Fact]
    public void TwoFloorsForOneIdentityFails()
    {
        var bundle = BundleWithVocabulary();
        var floorSentence = new PayloadFloorSentence { Trigger = "on_block", Behavior = "store" };
        bundle.SignaturePayloads.Add(TestPayload(payload => payload with { Floor = floorSentence }));
        bundle.SignaturePayloads.Add(TestPayload(payload => payload with { Id = "mending_twin", Floor = floorSentence }));

        AssertProblem(bundle, "signature_payload", "2 floor expressions");
    }

    [Fact]
    public void AProfileFavoringAnUnknownPayloadFails()
    {
        var bundle = BundleWithVocabulary();
        bundle.Materials.Add(new MaterialDefinition
        {
            Id = "material.test",
            Name = "Test",
            SignatureProfile = new SignatureProfile { FavoredPayloads = new[] { "no_such_payload" } },
        });

        AssertProblem(bundle, "material_identity", "no_such_payload");
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
        bundle.ModifierKeys.Add(new Dungeons.Modifiers.ModifierKeyDefinition
        {
            Id = "resource.max_health", Name = "Max Health",
        });
        return bundle;
    }

    /// <summary>A valid heal payload owned by Vital, reshaped per test via <c>with</c> — the
    /// broken field under test is the only broken thing about it.</summary>
    private static SignaturePayloadDefinition TestPayload(
        Func<SignaturePayloadDefinition, SignaturePayloadDefinition> reshape) => reshape(new SignaturePayloadDefinition
    {
        Id = "mending",
        Name = "Mending",
        Families = new[] { new PayloadFamilyStake { Identity = "identity.vital", Rung = 1 } },
        Binding = new PayloadBinding { Kind = "heal" },
        Range = new[] { 2.0, 5.0 },
        Weight = 10,
        Description = "A modest heal.",
    });

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
