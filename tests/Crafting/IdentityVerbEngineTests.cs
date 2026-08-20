using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Crafting.Identity;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Crafting;

/// <summary>
/// The ten transformation verbs (docs/transformation-verbs.md, D47) against the approved
/// design: the rank economy (preparation = fidelity), the overfill and condition ladders,
/// risk living only where the crafter chose it, preview parity, fingerprint stacking, and
/// the doc's own worked chain end to end. Every tuned number these tests quote is
/// provisional; the RULES they pin are not.
/// </summary>
public class IdentityVerbEngineTests
{
    private const string Vital = "identity.vital";
    private const string Dense = "identity.dense";
    private const string Ember = "identity.ember";
    private const string Keen = "identity.keen";

    // --- The rank economy ----------------------------------------------------

    [Fact]
    public void TransferFromARawSourceDeliversRankOne()
    {
        var iron = Migrated(capacity: 2);
        var rawOak = Migrated(capacity: 2) with { Identities = new[] { new IdentityStake(Vital, 3) } };

        var outcome = Engine().Commit(new VerbRequest
        {
            Verb = CraftVerb.Transfer, Substrate = iron, Sources = new[] { rawOak },
        });

        Assert.Equal(VerbResultKind.Succeeded, outcome.Kind);
        Assert.Equal(1, outcome.Result!.StakeOf(Vital)!.Rank);
    }

    [Fact]
    public void TransferFromACarrierDeliversItsFullRank()
    {
        // Preparation = fidelity — the rule that interlocks the professions (D47 §3).
        var iron = Migrated(capacity: 2);
        var tincture = Carrier(Vital, rank: 2);

        var outcome = Engine().Commit(new VerbRequest
        {
            Verb = CraftVerb.Transfer, Substrate = iron, Sources = new[] { tincture },
        });

        Assert.Equal(2, outcome.Result!.StakeOf(Vital)!.Rank);
    }

    [Fact]
    public void TransferRefusesAnIdentityTheSubstrateAlreadyCarries()
    {
        // Feeding an identity it already has is Develop's job — one verb per question.
        var iron = Migrated(capacity: 2) with { Identities = new[] { new IdentityStake(Vital, 1) } };

        var outcome = Engine().Commit(new VerbRequest
        {
            Verb = CraftVerb.Transfer, Substrate = iron, Sources = new[] { Carrier(Vital, 2) },
        });

        Assert.Equal(VerbFailureReason.IdentityAlreadyActive, outcome.Failure);
    }

    [Fact]
    public void DevelopFeedsOnSameIdentitySourcesAndDeepSourcesFeedMore()
    {
        var iron = Migrated(capacity: 2) with { Identities = new[] { new IdentityStake(Vital, 1) } };

        // Leaving rank 1 costs 2 points; one rank-2 carrier pays it alone.
        var fed = Engine().Commit(new VerbRequest
        {
            Verb = CraftVerb.Develop, Substrate = iron, TargetIdentityId = Vital,
            Sources = new[] { Carrier(Vital, 2) },
        });
        Assert.Equal(2, fed.Result!.StakeOf(Vital)!.Rank);

        // One rank-1 source is 1 point — refused, and no partial progress exists anywhere.
        var starved = Engine().Commit(new VerbRequest
        {
            Verb = CraftVerb.Develop, Substrate = iron, TargetIdentityId = Vital,
            Sources = new[] { Carrier(Vital, 1) },
        });
        Assert.Equal(VerbFailureReason.InsufficientDevelopment, starved.Failure);

        var wrongFood = Engine().Commit(new VerbRequest
        {
            Verb = CraftVerb.Develop, Substrate = iron, TargetIdentityId = Vital,
            Sources = new[] { Carrier(Ember, 3) },
        });
        Assert.Equal(VerbFailureReason.SourceLacksIdentity, wrongFood.Failure);
    }

    [Fact]
    public void ExtractPreservesRankOntoAPristineCarrierAndConsumesTheSource()
    {
        var oak = Migrated(capacity: 2) with { Identities = new[] { new IdentityStake(Vital, 3) } };

        var outcome = Engine().Commit(new VerbRequest
        {
            Verb = CraftVerb.Extract, Substrate = oak, TargetIdentityId = Vital,
        });

        Assert.Equal(VerbResultKind.Succeeded, outcome.Kind);
        Assert.Null(outcome.Result); // the source is spent
        var carrier = Assert.Single(outcome.Produced);
        Assert.True(carrier.IsCarrier);
        Assert.Equal(3, carrier.StakeOf(Vital)!.Rank);
        Assert.Equal(1, carrier.Capacity);
        Assert.Equal(Condition.Pristine, carrier.Condition);
    }

    // --- Capacity, stability, and the overfill gamble ------------------------

    [Fact]
    public void RevealNeedsAFreeSlotAndAwakensAtRankOne()
    {
        var oak = Migrated(capacity: 1) with { Latent = new[] { Vital } };

        var revealed = Engine().Commit(new VerbRequest
        {
            Verb = CraftVerb.Reveal, Substrate = oak, TargetIdentityId = Vital,
        });
        Assert.Equal(1, revealed.Result!.StakeOf(Vital)!.Rank);
        Assert.Empty(revealed.Result.Latent);

        var full = Migrated(capacity: 1) with
        {
            Identities = new[] { new IdentityStake(Dense, 1) },
            Latent = new[] { Vital },
        };
        Assert.Equal(VerbFailureReason.NoFreeSlot, Engine().Commit(new VerbRequest
        {
            Verb = CraftVerb.Reveal, Substrate = full, TargetIdentityId = Vital,
        }).Failure);
    }

    [Fact]
    public void TheOverfillingTransferIsSafe_FurtherWorkGambles()
    {
        // §10.3: stepping onto the ladder is a choice; the risk is on FURTHER work.
        var full = Migrated(capacity: 1) with { Identities = new[] { new IdentityStake(Dense, 1) } };

        var overfilling = Engine().Preview(new VerbRequest
        {
            Verb = CraftVerb.Transfer, Substrate = full, Sources = new[] { Carrier(Vital, 1) },
        });
        Assert.Null(overfilling.Failure);
        Assert.Equal(0.0, overfilling.Risks.FractureChance);
        Assert.Equal(Stability.Unstable, overfilling.Result!.Stability);

        var furtherWork = Engine().Preview(new VerbRequest
        {
            Verb = CraftVerb.Transfer, Substrate = overfilling.Result, Sources = new[] { Carrier(Ember, 1) },
        });
        Assert.Equal(IdentityCraftTuning.FractureChanceUnstable, furtherWork.Risks.FractureChance);
    }

    [Fact]
    public void FractureRemovesTheNewestIdentityAndWastesTheVerb()
    {
        var unstable = Migrated(capacity: 1) with
        {
            Identities = new[] { new IdentityStake(Dense, 1), new IdentityStake(Vital, 1) },
        };

        var outcome = Engine(rollingAlways: 0.0).Commit(new VerbRequest
        {
            Verb = CraftVerb.Transfer, Substrate = unstable, Sources = new[] { Carrier(Ember, 1) },
        });

        Assert.Equal(VerbResultKind.Fractured, outcome.Kind);
        Assert.Equal(Vital, outcome.FracturedIdentityId); // the newest breaks away
        Assert.True(outcome.Result!.Carries(Dense));
        Assert.False(outcome.Result.Carries(Vital));
        Assert.False(outcome.Result.Carries(Ember)); // the verb's work was lost
        Assert.Equal(Condition.Worked, outcome.Result.Condition); // the condition was still paid
    }

    [Fact]
    public void BeyondVolatileIsAWallNotARoll()
    {
        var volatileState = Migrated(capacity: 1) with
        {
            Identities = new[]
            {
                new IdentityStake(Dense, 1), new IdentityStake(Vital, 1), new IdentityStake(Ember, 1),
            },
        };
        Assert.Equal(Stability.Volatile, volatileState.Stability);

        var outcome = Engine().Commit(new VerbRequest
        {
            Verb = CraftVerb.Transfer, Substrate = volatileState, Sources = new[] { Carrier(Keen, 1) },
        });
        Assert.Equal(VerbFailureReason.OverfillLimit, outcome.Failure);
    }

    // --- The condition ladder ------------------------------------------------

    [Fact]
    public void ThreeSafeStepsThenTheFragileGamble()
    {
        // Pristine → Worked → Strained → Fragile: three safe identity-changing actions,
        // then every further one gambles (§10.4) — the extraction rhyme at the bench.
        var state = Migrated(capacity: 4);
        var engine = Engine();
        foreach (var identity in new[] { Dense, Vital, Ember })
        {
            state = engine.Commit(new VerbRequest
            {
                Verb = CraftVerb.Transfer, Substrate = state, Sources = new[] { Carrier(identity, 1) },
            }).Result!;
        }
        Assert.Equal(Condition.Fragile, state.Condition);

        var fourth = new VerbRequest
        {
            Verb = CraftVerb.Transfer, Substrate = state, Sources = new[] { Carrier(Keen, 1) },
        };

        Assert.Equal(IdentityCraftTuning.DestructionChanceWhenFragile,
            engine.Preview(fourth).Risks.DestructionChance);

        var survived = Engine(rollingAlways: 0.99).Commit(fourth);
        Assert.Equal(VerbResultKind.Succeeded, survived.Kind);
        Assert.Equal(Condition.Fragile, survived.Result!.Condition);

        var destroyed = Engine(rollingAlways: 0.0).Commit(fourth);
        Assert.Equal(VerbResultKind.Destroyed, destroyed.Kind);
        Assert.Null(destroyed.Result);
        Assert.NotNull(destroyed.Byproduct); // destruction always pays
        Assert.Equal("material.slag", destroyed.Byproduct!.Value.ItemId);
    }

    [Fact]
    public void GentleVerbsCostNoConditionAndRollNothing()
    {
        var worked = Migrated(capacity: 2) with { Condition = Condition.Strained, Quality = 50 };

        var refined = Engine().Commit(new VerbRequest { Verb = CraftVerb.Refine, Substrate = worked });
        Assert.Equal(Condition.Strained, refined.Result!.Condition);
        Assert.Equal(60, refined.Result.Quality);
        Assert.False(refined.Risks.Any);

        var restored = Engine().Commit(new VerbRequest { Verb = CraftVerb.Restore, Substrate = refined.Result });
        Assert.Equal(Condition.Worked, restored.Result!.Condition);

        // Worked is the ceiling — Pristine cannot be faked.
        Assert.Equal(VerbFailureReason.ConditionAtCeiling, Engine().Commit(new VerbRequest
        {
            Verb = CraftVerb.Restore, Substrate = restored.Result,
        }).Failure);
    }

    // --- The remaining verbs -------------------------------------------------

    [Fact]
    public void ProcessCarriesIdentitiesIntoTheOutputDefinition()
    {
        var ore = Migrated(capacity: 2) with
        {
            Identities = new[] { new IdentityStake(Dense, 2) },
            Condition = Condition.Worked,
        };

        var outcome = Engine().Commit(new VerbRequest
        {
            Verb = CraftVerb.Process, Substrate = ore, OutputDefinitionId = "material.test_ingot",
        });

        Assert.Equal(VerbResultKind.Succeeded, outcome.Kind);
        Assert.Equal(2, outcome.Result!.StakeOf(Dense)!.Rank);
        Assert.Equal(Condition.Worked, outcome.Result.Condition); // carried, not reset
        Assert.Equal("material.test_ingot", Assert.Single(outcome.Result.Roots).DefinitionId);

        Assert.Equal(VerbFailureReason.OutputDefinitionNotMigrated, Engine().Commit(new VerbRequest
        {
            Verb = CraftVerb.Process, Substrate = ore, OutputDefinitionId = "material.test_legacy",
        }).Failure);
    }

    [Fact]
    public void FuseUnionsIdentitiesKeepingTheHigherRank()
    {
        var left = Migrated(capacity: 3) with
        {
            Identities = new[] { new IdentityStake(Dense, 1), new IdentityStake(Vital, 2) },
        };
        var right = Migrated(capacity: 2) with
        {
            Identities = new[] { new IdentityStake(Vital, 1), new IdentityStake(Ember, 1) },
            Condition = Condition.Strained,
        };

        var outcome = Engine().Commit(new VerbRequest
        {
            Verb = CraftVerb.Fuse, Substrate = left, Sources = new[] { right },
        });

        var fused = outcome.Result!;
        Assert.Equal(3, fused.Identities.Count);
        Assert.Equal(2, fused.StakeOf(Vital)!.Rank);       // higher rank wins
        Assert.Equal(3, fused.Capacity);                    // highest input capacity
        Assert.Equal(Condition.Worked, fused.Condition);    // one step below the best input
    }

    [Fact]
    public void DisplaceEjectsTheChosenIdentityWithNoRefund()
    {
        var iron = Migrated(capacity: 2) with
        {
            Identities = new[] { new IdentityStake(Dense, 3), new IdentityStake(Vital, 1) },
        };

        var outcome = Engine().Commit(new VerbRequest
        {
            Verb = CraftVerb.Displace, Substrate = iron,
            DisplacedIdentityId = Dense, Sources = new[] { Carrier(Ember, 2) },
        });

        var result = outcome.Result!;
        Assert.False(result.Carries(Dense));
        Assert.Equal(2, result.StakeOf(Ember)!.Rank);
        Assert.Equal(2, result.Identities.Count);
        Assert.Equal(Stability.Stable, result.Stability);
    }

    [Fact]
    public void ExpandRaisesCapacityToTheExpandedCeilingOnly()
    {
        var state = Migrated(capacity: 4);
        var expanded = Engine().Commit(new VerbRequest { Verb = CraftVerb.Expand, Substrate = state });
        Assert.Equal(5, expanded.Result!.Capacity);

        Assert.Equal(VerbFailureReason.CapacityAtCeiling, Engine().Commit(new VerbRequest
        {
            Verb = CraftVerb.Expand, Substrate = expanded.Result,
        }).Failure);
    }

    // --- Preview parity and the fingerprint ----------------------------------

    [Fact]
    public void PreviewAndCommitAgreeWhenNoRiskExists()
    {
        var iron = Migrated(capacity: 2);
        var request = new VerbRequest
        {
            Verb = CraftVerb.Transfer, Substrate = iron, Sources = new[] { Carrier(Vital, 2) },
        };

        var previewed = Engine().Preview(request).Result!;
        var committed = Engine().Commit(request).Result!;

        Assert.Equal(
            Fingerprint.Canonical(previewed, Array.Empty<string>()),
            Fingerprint.Canonical(committed, Array.Empty<string>()));
    }

    [Fact]
    public void TwoPathsToTheSameStateShareAFingerprint()
    {
        // State is canonical; history only shapes it (§11.3). Acquisition order differs,
        // the fingerprint does not.
        var engine = Engine();
        var viaVitalFirst = Chain(engine, Migrated(capacity: 3), Vital, Dense);
        var viaDenseFirst = Chain(engine, Migrated(capacity: 3), Dense, Vital);

        Assert.Equal(
            Fingerprint.Compute(viaVitalFirst, Array.Empty<string>()),
            Fingerprint.Compute(viaDenseFirst, Array.Empty<string>()));
    }

    [Fact]
    public void RankAndCarrierAndQualityBucketAllChangeThePrint()
    {
        var baseline = Migrated(capacity: 2) with { Identities = new[] { new IdentityStake(Vital, 1) } };
        string Print(IdentityMaterialState state) => Fingerprint.Compute(state, Array.Empty<string>());

        Assert.NotEqual(Print(baseline), Print(baseline with
        {
            Identities = new[] { new IdentityStake(Vital, 2) },
        }));
        Assert.NotEqual(Print(baseline), Print(baseline with { IsCarrier = true }));

        // Quality buckets by 10: 50 and 59 stack, 60 does not.
        Assert.Equal(Print(baseline with { Quality = 50 }), Print(baseline with { Quality = 59 }));
        Assert.NotEqual(Print(baseline with { Quality = 59 }), Print(baseline with { Quality = 60 }));
    }

    // --- Root derivations ----------------------------------------------------

    [Fact]
    public void TheMergedProfileLeansTowardTheHeavierRoot()
    {
        var content = TestContent();
        var state = Migrated(capacity: 2) with
        {
            Roots = new[]
            {
                new ProvenanceRoot("material.test_oak", 0.8),
                new ProvenanceRoot("material.test_iron_ore", 0.2), // authors no profile
            },
        };

        var profile = RootDerivations.ProfileOf(state, content.Materials);

        var onBlock = Assert.Single(profile.FavoredTriggers, lean => lean.Id == "on_block");
        Assert.Equal(0.8, onBlock.Weight, 3);
        Assert.Contains(profile.Themes, lean => lean.Id == "renewal");
    }

    [Fact]
    public void BaseStatsBlendByRootContribution()
    {
        var content = TestContent();
        var state = Migrated(capacity: 2) with
        {
            Roots = new[]
            {
                new ProvenanceRoot("material.test_oak", 0.5),      // give 6, toughness 4
                new ProvenanceRoot("material.test_iron_ore", 0.5), // heft 6, toughness 6
            },
        };

        var baseStats = RootDerivations.BaseOf(state, content.Materials);

        Assert.Equal(5, baseStats.Heft);      // 4 × 0.5 + 6 × 0.5
        Assert.Equal(3, baseStats.Give);      // 6 × 0.5 + 0
        Assert.Equal(5, baseStats.Toughness); // 4 × 0.5 + 6 × 0.5
    }

    // --- The worked chain, end to end (docs/transformation-verbs.md §6) ------

    [Fact]
    public void TheWorkedChainProducesDenseOakboundIronExactlyAsDesigned()
    {
        var engine = Engine();

        // Oak (latent Vital) → Reveal → Extract onto a carrier (rank preserved).
        var oak = Migrated(capacity: 2) with
        {
            Latent = new[] { Vital },
            Roots = new[] { new ProvenanceRoot("material.test_oak", 1.0) },
        };
        var revealedOak = engine.Commit(new VerbRequest
        {
            Verb = CraftVerb.Reveal, Substrate = oak, TargetIdentityId = Vital,
        }).Result!;
        var carrier = engine.Commit(new VerbRequest
        {
            Verb = CraftVerb.Extract, Substrate = revealedOak, TargetIdentityId = Vital,
        }).Produced[0];

        // Develop the carrier to rank 2 (fed by two more revealed oaks: 2 × r1 = the cost).
        var feed = new[] { revealedOak, revealedOak };
        var tincture = engine.Commit(new VerbRequest
        {
            Verb = CraftVerb.Develop, Substrate = carrier, TargetIdentityId = Vital, Sources = feed,
        }).Result!;
        Assert.Equal(2, tincture.StakeOf(Vital)!.Rank);
        Assert.True(tincture.IsCarrier);

        // Iron Ore → Process → Ingot (clean slate) → Transfer Dense (raw, r1) →
        // Transfer Vital from the tincture (carrier fidelity, r2).
        var ore = Migrated(capacity: 2) with
        {
            Roots = new[] { new ProvenanceRoot("material.test_iron_ore", 1.0) },
        };
        var ingot = engine.Commit(new VerbRequest
        {
            Verb = CraftVerb.Process, Substrate = ore, OutputDefinitionId = "material.test_ingot",
        }).Result!;
        var denseIron = engine.Commit(new VerbRequest
        {
            Verb = CraftVerb.Transfer, Substrate = ingot,
            Sources = new[] { Migrated(capacity: 1) with { Identities = new[] { new IdentityStake(Dense, 2) } } },
        }).Result!;
        Assert.Equal(1, denseIron.StakeOf(Dense)!.Rank); // raw source: rank 1, whatever it held

        var denseOakboundIron = engine.Commit(new VerbRequest
        {
            Verb = CraftVerb.Transfer, Substrate = denseIron, Sources = new[] { tincture },
        }).Result!;

        // The doc's exact destination: Dense r1 + Vital r2, both slots full, Stable,
        // condition at Strained — "develop further, or forge now?"
        Assert.Equal(2, denseOakboundIron.Identities.Count);
        Assert.Equal(1, denseOakboundIron.StakeOf(Dense)!.Rank);
        Assert.Equal(2, denseOakboundIron.StakeOf(Vital)!.Rank);
        Assert.Equal(Stability.Stable, denseOakboundIron.Stability);
        Assert.Equal(Condition.Strained, denseOakboundIron.Condition);

        // And the r2 exists BECAUSE the carrier chain touched it — the raw path gives r1.
        var rawPath = engine.Commit(new VerbRequest
        {
            Verb = CraftVerb.Transfer, Substrate = denseIron, Sources = new[] { revealedOak },
        }).Result!;
        Assert.Equal(1, rawPath.StakeOf(Vital)!.Rank);
    }

    // --- Harness -------------------------------------------------------------

    private static IdentityMaterialState Migrated(int capacity) => new()
    {
        Capacity = capacity,
        Roots = new[] { new ProvenanceRoot("material.test_iron_ore", 1.0) },
    };

    private static IdentityMaterialState Carrier(string identityId, int rank) => new()
    {
        Identities = new[] { new IdentityStake(identityId, rank) },
        Capacity = 1,
        IsCarrier = true,
        Roots = new[] { new ProvenanceRoot("material.test_oak", 1.0) },
    };

    private static IdentityMaterialState Chain(
        IdentityCraftingEngine engine, IdentityMaterialState start, params string[] identities)
    {
        var state = start;
        foreach (var identity in identities)
        {
            state = engine.Commit(new VerbRequest
            {
                Verb = CraftVerb.Transfer, Substrate = state, Sources = new[] { Carrier(identity, 1) },
            }).Result!;
        }
        return state;
    }

    private static IdentityCraftingEngine Engine(double? rollingAlways = null) =>
        new(TestContent(), rollingAlways is { } roll ? new FixedRandom(roll) : new SeededRandom(7));

    private static ContentBundle TestContent()
    {
        var bundle = new ContentBundle();
        bundle.Materials.Add(new MaterialDefinition
        {
            Id = "material.test_iron_ore", Name = "Test Iron Ore",
            Tags = new[] { "form:ore", "form:metal" },
            Capacity = 2,
            Base = new MaterialBaseStats { Heft = 6, Toughness = 6 },
        });
        bundle.Materials.Add(new MaterialDefinition
        {
            Id = "material.test_ingot", Name = "Test Ingot",
            Tags = new[] { "form:metal", "form:ingot" },
            Capacity = 2,
            Base = new MaterialBaseStats { Heft = 5, Bite = 6, Toughness = 6 },
        });
        bundle.Materials.Add(new MaterialDefinition
        {
            Id = "material.test_oak", Name = "Test Oak",
            Tags = new[] { "form:wood" },
            Capacity = 2,
            Latent = new[] { Vital },
            Base = new MaterialBaseStats { Heft = 4, Toughness = 4, Give = 6 },
            SignatureProfile = new SignatureProfile
            {
                Themes = new[] { "renewal", "endurance" },
                FavoredTriggers = new[] { "on_block" },
                FavoredBehaviors = new[] { "store" },
            },
        });
        bundle.Materials.Add(new MaterialDefinition
        {
            Id = "material.test_legacy", Name = "Unmigrated Legacy Material",
            Tags = new[] { "form:metal" },
        });
        bundle.Byproducts.Add(new ByproductDefinition
        {
            Id = "byproduct.test_slag", Name = "Slag", Material = "material.slag",
            Forms = new[] { "metal", "ore" },
        });
        bundle.Byproducts.Add(new ByproductDefinition
        {
            Id = "byproduct.test_residue", Name = "Residue", Material = "material.residue",
            Fallback = true,
        });
        return bundle;
    }

    /// <summary>Forces every risk roll to a fixed value — 0.0 lands every gamble, 0.99
    /// survives every one.</summary>
    private sealed class FixedRandom : IRandomSource
    {
        private readonly double _value;
        public FixedRandom(double value) => _value = value;
        public double NextDouble() => _value;
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
