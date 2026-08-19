using Dungeons.Content;

namespace Dungeons.Professions;

/// <summary>
/// What the rest of the player's progress is currently worth to one profession — the
/// cross-profession and global half of <see cref="ProfessionBenefits"/>.
///
/// <para><b>Contributions sum; they do not displace.</b> The mastery ladder lets a
/// profession-specific rung <em>replace</em> the general one, because there mastery in one
/// action can only be worth one thing. Here the opposite is right: three professions each
/// contributing a little to Smithing is the mechanic, so a second synergy for the same
/// quantity adds to the first rather than silently winning over it. Each synergy is capped
/// individually, which keeps the ceiling readable — no total is bigger than the sum of the
/// caps the player can read on the page.</para>
/// </summary>
public sealed class ProfessionSynergies
{
    /// <summary>No synergies at all. For tests, and for a host with no synergy content.</summary>
    public static readonly ProfessionSynergies None = new(Array.Empty<ProfessionSynergyDefinition>());

    private readonly List<ProfessionSynergyDefinition> _synergies;

    public ProfessionSynergies(IEnumerable<ProfessionSynergyDefinition> synergies)
    {
        ArgumentNullException.ThrowIfNull(synergies);
        _synergies = synergies.ToList();
    }

    public ProfessionSynergies(DataStore<ProfessionSynergyDefinition> synergies)
        : this((synergies ?? throw new ArgumentNullException(nameof(synergies))).GetAll())
    {
    }

    /// <summary>Every synergy, for the page the player reads.</summary>
    public IReadOnlyList<ProfessionSynergyDefinition> All => _synergies;

    /// <summary>Every synergy <paramref name="professionId"/> benefits from, in authored order.</summary>
    public IReadOnlyList<ProfessionSynergyDefinition> Reaching(string? professionId) =>
        _synergies.Where(synergy => synergy.Benefits(professionId)).ToList();

    /// <summary>
    /// What <paramref name="kind"/> is currently worth to <paramref name="professionId"/> from
    /// every synergy that reaches it.
    /// </summary>
    /// <param name="levelOf">A profession's current level.</param>
    /// <param name="totalLevel">The sum of every profession's level — what a global synergy reads.</param>
    public double ValueOf(
        ProfessionBenefitKind kind,
        string? professionId,
        Func<string, int> levelOf,
        Func<int> totalLevel)
    {
        ArgumentNullException.ThrowIfNull(levelOf);
        ArgumentNullException.ThrowIfNull(totalLevel);

        var total = 0.0;
        foreach (var synergy in _synergies)
        {
            if (synergy.Kind != kind || !synergy.Benefits(professionId))
                continue;

            var sourceLevel = synergy.IsGlobalSource ? totalLevel() : levelOf(synergy.SourceProfession!);
            total += synergy.ValueAt(sourceLevel);
        }

        return total;
    }
}
