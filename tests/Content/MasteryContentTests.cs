using Dungeons.Content;
using Dungeons.Professions;
using Xunit;

namespace Dungeons.Tests.Content;

/// <summary>
/// Validation for the mastery ladder — one failing-content test per rule, the same discipline
/// every other content type ships with.
///
/// <para>Each rule exists because the failure it catches is <b>silent</b>: a rung worth nothing
/// per level, capped at nothing, or unlocking above the ceiling all load cleanly and then quietly
/// do nothing forever. That is precisely the state Phase 8 got mastery out of, and these are what
/// stop it drifting back.</para>
/// </summary>
public class MasteryContentTests
{
    private static MasteryBenefitDefinition Rung(
        string id = "mastery.test",
        ProfessionBenefitKind kind = ProfessionBenefitKind.IntervalReduction,
        int unlockLevel = 1,
        double perLevel = 0.005,
        double max = 0.4,
        string? profession = null,
        string description = "It helps.") => new()
    {
        Id = id,
        Kind = kind,
        UnlockLevel = unlockLevel,
        PerLevel = perLevel,
        Max = max,
        ProfessionId = profession,
        Description = description,
    };

    private static IReadOnlyList<ContentProblem> ProblemsFor(params MasteryBenefitDefinition[] ladder)
    {
        var store = new DataStore<MasteryBenefitDefinition>();
        foreach (var rung in ladder)
            store.Add(rung);

        return ContentValidator.Validate(new ContentBundle { MasteryBenefits = store })
            .Where(problem => problem.Category == "mastery")
            .ToList();
    }

    /// <summary>A full, coherent ladder passes. Without this the rules below could all be
    /// satisfied by a validator that rejects everything.</summary>
    [Fact]
    public void ACompleteLadderIsAccepted()
    {
        var ladder = Enum.GetValues<ProfessionBenefitKind>()
            .Select(kind => Rung($"mastery.{kind}", kind))
            .ToArray();

        Assert.Empty(ProblemsFor(ladder));
    }

    [Fact]
    public void ARungWorthNothingPerLevelIsRejected()
    {
        Assert.Contains(ProblemsFor(Rung(perLevel: 0)), p => p.Message.Contains("per level"));
    }

    [Fact]
    public void ARungCappedAtNothingIsRejected()
    {
        Assert.Contains(ProblemsFor(Rung(max: 0)), p => p.Message.Contains("capped at"));
    }

    [Fact]
    public void ARungUnlockingAboveTheCeilingIsRejected()
    {
        Assert.Contains(
            ProblemsFor(Rung(unlockLevel: MasteryLeveling.MaxLevel + 1)),
            p => p.Message.Contains("outside"));
    }

    /// <summary>A cap the ceiling cannot deliver is a promise the ladder can never keep — this is
    /// the rule that caught the shipped table on its first run.</summary>
    [Fact]
    public void ACapTheCeilingCannotReachIsRejected()
    {
        Assert.Contains(
            ProblemsFor(Rung(perLevel: 0.001, max: 0.9)),
            p => p.Message.Contains("reaches only"));
    }

    [Fact]
    public void ARungScopedToAnUnknownProfessionIsRejected()
    {
        Assert.Contains(
            ProblemsFor(Rung(profession: "profession.imaginary")),
            p => p.Message.Contains("unknown profession"));
    }

    /// <summary>Two rungs for the same kind and scope means one silently wins over the other.</summary>
    [Fact]
    public void TwoRungsForTheSameScopeAreRejected()
    {
        Assert.Contains(
            ProblemsFor(Rung("mastery.a"), Rung("mastery.b")),
            p => p.Message.Contains("second"));
    }

    [Fact]
    public void ARungWithNoDescriptionIsRejected()
    {
        Assert.Contains(ProblemsFor(Rung(description: "")), p => p.Message.Contains("no description"));
    }

    /// <summary>A PARTIAL ladder is the dangerous shape: somebody authored rungs and forgot a
    /// kind, so that benefit does nothing for anyone.</summary>
    [Fact]
    public void APartialLadderIsRejected()
    {
        Assert.Contains(
            ProblemsFor(Rung(kind: ProfessionBenefitKind.IntervalReduction)),
            p => p.Message.Contains("no general rung"));
    }

    /// <summary>An EMPTY ladder is a bundle that simply carries no mastery content — every test
    /// fixture in the suite — and is left alone. That the shipped game carries all six is held by
    /// <c>MasteryTests.TheShippedLadderAuthorsEveryBenefitKind</c>.</summary>
    [Fact]
    public void AnEmptyLadderIsLeftAlone()
    {
        Assert.Empty(ProblemsFor());
    }
}
