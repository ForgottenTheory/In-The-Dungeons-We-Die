using Dungeons.Content;
using Dungeons.Crafting.Identity;
using Dungeons.Hideout;
using Dungeons.Items;
using Dungeons.Professions;
using Xunit;

namespace Dungeons.Tests.Content;

/// <summary>
/// The verb-action content type (migration Phase 2b — docs/transformation-verbs.md §1,
/// D47/D48): every validator rule proven to fire against deliberately broken content. The
/// shipped file is deliberately empty until the Phase 4 material migration; these fixtures
/// are the rules waiting for it — the validator-before-content method.
/// </summary>
public class VerbActionContentTests
{
    [Fact]
    public void AValidActionOfferedAtAStationPassesCleanly()
    {
        var bundle = BundleWith(SmeltIron());
        Assert.DoesNotContain(ContentValidator.Validate(bundle),
            p => p.Category is "verb_actions" or "stations");
    }

    [Fact]
    public void AnActionOfferedAtNoStationFails()
    {
        var bundle = BundleWith(SmeltIron(), offerAtStation: false);
        AssertProblem(bundle, "stations", "offered at no station");
    }

    [Fact]
    public void AStationOfferingAnUnknownVerbActionFails()
    {
        var bundle = BundleWith(SmeltIron(), stationOffers: new[] { "craft.smelt_iron", "craft.nonexistent" });
        AssertProblem(bundle, "stations", "unknown verb action 'craft.nonexistent'");
    }

    [Fact]
    public void AWrongIdPrefixFails()
    {
        var bundle = BundleWith(SmeltIron() with { Id = "process.smelt_iron" }, offerAtStation: false);
        AssertProblem(bundle, "verb_actions", "must start with 'craft.'");
    }

    [Fact]
    public void AnUnknownGateProfessionFails()
    {
        var bundle = BundleWith(SmeltIron() with { Profession = "profession.wizardry" });
        AssertProblem(bundle, "verb_actions", "unknown profession 'profession.wizardry'");
    }

    [Fact]
    public void AProcessActionWithoutAnOutputFails()
    {
        var bundle = BundleWith(SmeltIron() with { Output = null });
        AssertProblem(bundle, "verb_actions", "must name their output");
    }

    [Fact]
    public void AProcessOutputThatIsNotMigratedFails()
    {
        var bundle = BundleWith(SmeltIron() with { Output = "material.test_legacy" });
        AssertProblem(bundle, "verb_actions", "has not been migrated");
    }

    [Fact]
    public void ANonProcessActionDeclaringAnOutputFails()
    {
        var bundle = BundleWith(SmeltIron() with
        {
            Verb = CraftVerb.Refine, Output = "material.test_ingot",
        });
        AssertProblem(bundle, "verb_actions", "only Process converts substance");
    }

    [Fact]
    public void AnUnsatisfiableSubstrateGateFails()
    {
        var bundle = BundleWith(SmeltIron() with { SubstrateTags = new[] { "form:moonsilk" } });
        AssertProblem(bundle, "verb_actions", "no material satisfies");
    }

    [Fact]
    public void AMalformedSubstrateTagFails()
    {
        var bundle = BundleWith(SmeltIron() with { SubstrateTags = new[] { "not-a-tag" } });
        AssertProblem(bundle, "verb_actions", "not a family:value tag");
    }

    [Fact]
    public void AnIdentityScopeOnANonTargetingVerbFails()
    {
        // Scoping Refine would be a silent no-op — Runecrafting's lever only means something
        // on the identity-targeting verbs (D48).
        var bundle = BundleWith(SmeltIron() with
        {
            Verb = CraftVerb.Refine, Output = null, IdentityScope = new[] { "identity.arcane" },
        });
        AssertProblem(bundle, "verb_actions", "silent no-op");
    }

    [Fact]
    public void AnUnknownIdentityInTheScopeFails()
    {
        var bundle = BundleWith(SmeltIron() with
        {
            Verb = CraftVerb.Transfer, Output = null, IdentityScope = new[] { "identity.wishes" },
        });
        AssertProblem(bundle, "verb_actions", "unknown identity 'identity.wishes'");
    }

    [Fact]
    public void AnUnknownExtraCostFails()
    {
        var bundle = BundleWith(SmeltIron() with
        {
            ExtraCosts = new[] { new ItemStack("material.phlogiston", 2) },
        });
        AssertProblem(bundle, "verb_actions", "extra cost 'material.phlogiston'");
    }

    // --- Harness -------------------------------------------------------------

    private static VerbActionDefinition SmeltIron() => new()
    {
        Id = "craft.smelt_iron",
        Name = "Smelt",
        Description = "Iron ore into an ingot; identities carry through.",
        Verb = CraftVerb.Process,
        Profession = "profession.test_smithing",
        RequiredLevel = 1,
        SubstrateTags = new[] { "form:ore" },
        Output = "material.test_ingot",
    };

    private static ContentBundle BundleWith(
        VerbActionDefinition action,
        bool offerAtStation = true,
        IReadOnlyList<string>? stationOffers = null)
    {
        var bundle = new ContentBundle();
        bundle.VerbActions.Add(action);
        bundle.Identities.Add(new IdentityDefinition
        {
            Id = "identity.arcane", Name = "Arcane", Cluster = "magical", Description = "The magic economy.",
        });
        bundle.Professions.Add(new ProfessionDefinition
        {
            Id = "profession.test_smithing", Name = "Smithing",
        });
        bundle.Materials.Add(new MaterialDefinition
        {
            Id = "material.test_iron_ore", Name = "Iron Ore",
            Tags = new[] { "form:ore", "form:metal" }, Capacity = 2,
        });
        bundle.Materials.Add(new MaterialDefinition
        {
            Id = "material.test_ingot", Name = "Iron Ingot",
            Tags = new[] { "form:metal", "form:ingot" }, Capacity = 2,
        });
        bundle.Materials.Add(new MaterialDefinition
        {
            Id = "material.test_legacy", Name = "Legacy Material",
            Tags = new[] { "form:metal" },
        });

        // The station always exists — the reverse-reachability rules deliberately go quiet
        // on a bundle with no stations at all, so "offered nowhere" needs a station that
        // simply doesn't offer it.
        bundle.Stations.Add(new StationDefinition
        {
            Id = "station.test_forge", Name = "Forge",
            Professions = new[] { "profession.test_smithing" },
            VerbActions = stationOffers ?? (offerAtStation ? new[] { action.Id } : Array.Empty<string>()),
        });

        return bundle;
    }

    private static void AssertProblem(ContentBundle bundle, string category, string messageFragment)
    {
        var problems = ContentValidator.Validate(bundle);
        Assert.Contains(problems, p => p.Category == category && p.Message.Contains(messageFragment));
    }
}
