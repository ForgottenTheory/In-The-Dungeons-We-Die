using Dungeons.Content;
using Dungeons.Crafting.Identity;
using Dungeons.Presentation;
using Xunit;

namespace Dungeons.Tests.Presentation;

/// <summary>
/// The bench inspector's material card (migration Phase 6): every §11.2 facet in a sentence,
/// ranks as rung words, meanings attached to the states that need them — and no simulation
/// numbers outside the slot counts the design itself surfaces.
/// </summary>
public class IdentityMaterialReadingTests
{
    private const string Vital = "identity.vital";
    private const string Dense = "identity.dense";
    private const string Storm = "identity.storm";

    [Fact]
    public void AFullReadingSpeaksEveryFacet()
    {
        var content = Content();
        var state = new IdentityMaterialState
        {
            Identities = new[] { new IdentityStake(Dense, 1), new IdentityStake(Vital, 2) },
            Latent = new[] { Storm },
            Capacity = 3,
            Condition = Condition.Worked,
            Quality = 55,
        };

        var summary = IdentityMaterialReadings.Summary(state, content);

        Assert.Contains("Dense · Vital (improved) — 2 of 3 identity slots taken", summary);
        Assert.Contains("Latent: Storm — Reveal can wake it", summary);
        Assert.Contains("Worked — has taken deep work · fine workmanship", summary);
        Assert.DoesNotContain("55", summary);
    }

    [Fact]
    public void EmptyStockSaysItsSlotsAreOpen()
    {
        var summary = IdentityMaterialReadings.Summary(
            new IdentityMaterialState { Capacity = 2 }, Content());

        Assert.Contains("Carries nothing — 2 identity slots, open", summary);
    }

    [Fact]
    public void OverfillIsCountedStraightAndExplained()
    {
        var content = Content();
        var state = new IdentityMaterialState
        {
            Identities = new[]
            {
                new IdentityStake(Dense, 1), new IdentityStake(Vital, 1), new IdentityStake(Storm, 1),
            },
            Capacity = 1,
        };

        var summary = IdentityMaterialReadings.Summary(state, content);

        Assert.Contains("3 identities on 1 slot", summary);
        Assert.Contains("Volatile — fracture is likely", summary);
    }

    [Fact]
    public void ACarrierAnnouncesItsFidelity()
    {
        var content = Content();
        var state = new IdentityMaterialState
        {
            Identities = new[] { new IdentityStake(Vital, 2) },
            Capacity = 1,
            IsCarrier = true,
        };

        var summary = IdentityMaterialReadings.Summary(state, content);

        Assert.Contains("A prepared carrier — delivers its full depth on Transfer", summary);
    }

    [Fact]
    public void AFragileMaterialWarnsBeforeTheGamble()
    {
        var summary = IdentityMaterialReadings.Summary(
            new IdentityMaterialState { Condition = Condition.Fragile }, Content());

        Assert.Contains("Fragile — further deep work gambles destruction", summary);
    }

    private static ContentBundle Content() => new()
    {
        Identities = TestPaths.LoadStore<IdentityDefinition>("identities"),
    };
}
