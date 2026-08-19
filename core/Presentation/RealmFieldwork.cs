using Dungeons.Content;
using Dungeons.Realms;

namespace Dungeons.Presentation;

/// <summary>
/// One trade a Realm asks for, measured against the party that is about to walk in.
///
/// <para>Counts rather than a single verdict: a profession with a level-1 node and a level-40
/// node is neither "ready" nor "not ready", and collapsing it to one boolean would tell the
/// player to stay home from ground they can already work.</para>
/// </summary>
public sealed record FieldworkRequirement(
    string ProfessionId,
    string ProfessionName,
    int PlayerLevel,
    int WorkableNodeCount,
    int TotalNodeCount,
    int? NextLevelNeeded)
{
    public bool CanWorkAnything => WorkableNodeCount > 0;

    public bool CanWorkEverything => WorkableNodeCount == TotalNodeCount;
}

/// <summary>
/// What it takes to actually work a Realm: which trades its gathering nodes call for, and how
/// far the party's professions have come. The preparation screen's answer to "am I equipped for
/// the job half of this run, not just the fight half".
///
/// <para><b>Node names stay behind <see cref="RealmInsight.RichNodes"/>; the trades do not.</b>
/// Which professions a place rewards is the sort of thing you hear before you go — the insight
/// on offer is telling the rich workings from the poor ones, not knowing that a forest has
/// trees. So this counts nodes and never names them, and <see cref="RealmBriefing.Resources"/>
/// remains the only thing that says where they are.</para>
///
/// <para><b>Worn profession tools are not here, because they do not exist yet.</b> Tool slots,
/// tool forms and the yield pipeline that would read them are E6; the components Smithing and
/// Artifice already make are waiting on it, as are the Agility course's bonus keys. When they
/// land, they belong on this reading — a tool is exactly "what you bring to the job".</para>
/// </summary>
public static class RealmFieldwork
{
    /// <param name="professionLevel">The party's level in a profession, by id. Passed in rather
    /// than looked up so this stays a pure reading over content plus one number per trade.</param>
    public static IReadOnlyList<FieldworkRequirement> Survey(
        ContentBundle content,
        RealmDefinition realm,
        int knowledge,
        Func<string, int> professionLevel)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(realm);
        ArgumentNullException.ThrowIfNull(professionLevel);

        var requiredLevelsByProfession = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        foreach (var location in realm.VisibleAt(knowledge)
                     .Where(location => location.Type == RealmLocationType.Gather))
        {
            if (location.ProfessionActionId is not { Length: > 0 } actionId
                || !content.Actions.TryGetById(actionId, out var action))
                continue;

            if (!requiredLevelsByProfession.TryGetValue(action.ProfessionId, out var levels))
                requiredLevelsByProfession[action.ProfessionId] = levels = new List<int>();
            levels.Add(action.RequiredLevel);
        }

        return requiredLevelsByProfession
            .Select(entry => Measure(content, entry.Key, entry.Value, professionLevel(entry.Key)))
            .OrderBy(requirement => requirement.ProfessionName, StringComparer.Ordinal)
            .ToList();
    }

    private static FieldworkRequirement Measure(
        ContentBundle content,
        string professionId,
        IReadOnlyList<int> requiredLevels,
        int playerLevel)
    {
        var outOfReach = requiredLevels.Where(required => required > playerLevel).ToList();

        return new FieldworkRequirement(
            professionId,
            content.Professions.TryGetById(professionId, out var profession) ? profession.Name : professionId,
            playerLevel,
            WorkableNodeCount: requiredLevels.Count - outOfReach.Count,
            TotalNodeCount: requiredLevels.Count,
            NextLevelNeeded: outOfReach.Count == 0 ? null : outOfReach.Min());
    }
}
