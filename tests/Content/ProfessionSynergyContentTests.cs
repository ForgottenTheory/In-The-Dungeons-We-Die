using Dungeons.Content;
using Dungeons.Professions;
using Xunit;

namespace Dungeons.Tests.Content;

/// <summary>
/// Validation for the cross-profession and global bonus table — one failing-content test per
/// rule, the discipline every content type here ships with.
///
/// <para>These were written <b>before</b> the table they check, on the precedent Phase 8 set
/// with the mastery ladder: every failure they catch is silent. A synergy worth nothing per
/// level, capped at nothing, or unlocking above the ceiling loads cleanly and then does nothing
/// forever — which is the exact condition Phase 10 exists to get the progression tracks out of.</para>
/// </summary>
public class ProfessionSynergyContentTests
{
    private const string Mining = "profession.mining";
    private const string Smithing = "profession.smithing";

    private static DataStore<ProfessionDefinition> Roster()
    {
        var store = new DataStore<ProfessionDefinition>();
        store.Add(new ProfessionDefinition { Id = Mining, Name = "Mining" });
        store.Add(new ProfessionDefinition { Id = Smithing, Name = "Smithing" });
        return store;
    }

    private static ProfessionSynergyDefinition Synergy(
        string id = "synergy.test",
        ProfessionBenefitKind kind = ProfessionBenefitKind.InputPreservation,
        string? source = Mining,
        string? target = Smithing,
        int unlockLevel = 10,
        double perLevel = 0.001,
        double max = 0.05,
        string description = "It helps.") => new()
    {
        Id = id,
        Kind = kind,
        SourceProfession = source,
        TargetProfession = target,
        UnlockLevel = unlockLevel,
        PerLevel = perLevel,
        Max = max,
        Description = description,
    };

    private static IReadOnlyList<ContentProblem> ProblemsFor(params ProfessionSynergyDefinition[] synergies)
    {
        var store = new DataStore<ProfessionSynergyDefinition>();
        foreach (var synergy in synergies)
            store.Add(synergy);

        return ContentValidator.Validate(new ContentBundle { Synergies = store, Professions = Roster() })
            .Where(problem => problem.Category == "synergies")
            .ToList();
    }

    /// <summary>A coherent table passes — without this, every rule below could be satisfied by a
    /// validator that rejects everything.</summary>
    [Fact]
    public void ACoherentTableIsAccepted() => Assert.Empty(ProblemsFor(Synergy()));

    [Fact]
    public void AGlobalSynergyIsAccepted() =>
        Assert.Empty(ProblemsFor(Synergy(source: null, target: null, unlockLevel: 50, perLevel: 0.0001, max: 0.019)));

    [Fact]
    public void ASynergyWorthNothingPerLevelIsRejected() =>
        Assert.Contains(ProblemsFor(Synergy(perLevel: 0)), problem => problem.Message.Contains("per level"));

    [Fact]
    public void ASynergyCappedAtNothingIsRejected() =>
        Assert.Contains(ProblemsFor(Synergy(max: 0)), problem => problem.Message.Contains("capped at"));

    [Fact]
    public void AnUnknownSourceProfessionIsRejected() =>
        Assert.Contains(ProblemsFor(Synergy(source: "profession.imaginary")), problem => problem.Message.Contains("paid for by unknown"));

    [Fact]
    public void AnUnknownTargetProfessionIsRejected() =>
        Assert.Contains(ProblemsFor(Synergy(target: "profession.imaginary")), problem => problem.Message.Contains("pays into unknown"));

    /// <summary>A profession paying itself for its own level is a mastery rung with extra steps
    /// — and a self-amplifying one, because the benefit it grants makes the work that raises the
    /// level go faster.</summary>
    [Fact]
    public void AProfessionPayingItselfIsRejected() =>
        Assert.Contains(
            ProblemsFor(Synergy(source: Mining, target: Mining)),
            problem => problem.Message.Contains("mastery rung"));

    /// <summary>The rule that caught the mastery table on its first run, transplanted: a cap the
    /// source level can never reach is a promise the table cannot keep.</summary>
    [Fact]
    public void ACapTheSourceCannotReachIsRejected() =>
        Assert.Contains(
            ProblemsFor(Synergy(perLevel: 0.001, max: 0.5)),
            problem => problem.Message.Contains("reaches only"));

    /// <summary>A global synergy's ceiling is the whole roster's, so what is unreachable for one
    /// profession may be perfectly reachable globally. The rule has to know the difference.</summary>
    [Fact]
    public void AGlobalSynergyIsMeasuredAgainstTheWholeRoster()
    {
        // 0.5 needs source level 500 — impossible for one profession (99), fine across a roster
        // whose total ceiling is 198 in this fixture... which it is not, so this must still fail.
        Assert.Contains(
            ProblemsFor(Synergy(source: null, target: null, perLevel: 0.001, max: 0.5)),
            problem => problem.Message.Contains("reaches only"));

        // The same rate capped at what the roster can deliver passes.
        Assert.Empty(ProblemsFor(Synergy(source: null, target: null, perLevel: 0.001, max: 0.198)));
    }

    [Fact]
    public void AnUnlockAboveTheCeilingIsRejected() =>
        Assert.Contains(
            ProblemsFor(Synergy(unlockLevel: ProfessionLeveling.MaxLevel + 1)),
            problem => problem.Message.Contains("could never switch on"));

    [Fact]
    public void TwoSynergiesForTheSamePairAreRejected() =>
        Assert.Contains(
            ProblemsFor(Synergy("synergy.a"), Synergy("synergy.b")),
            problem => problem.Message.Contains("second"));

    [Fact]
    public void ASynergyWithNoDescriptionIsRejected() =>
        Assert.Contains(ProblemsFor(Synergy(description: "")), problem => problem.Message.Contains("no description"));

    /// <summary>An empty table is a bundle that carries no synergy content — every other test
    /// fixture in the suite — and is left alone.</summary>
    [Fact]
    public void AnEmptyTableIsLeftAlone() => Assert.Empty(ProblemsFor());
}
