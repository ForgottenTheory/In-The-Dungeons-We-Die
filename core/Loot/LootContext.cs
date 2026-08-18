namespace Dungeons.Loot;

/// <summary>
/// Everything a loot table is allowed to know about the circumstances of a drop: how deep the
/// party is, what tier the Realm is, and a bag of tags.
///
/// <para><b>Tags are the extension point.</b> Rather than growing a field per question a
/// designer might ask, the caller states the circumstances as tags and entries gate on them.
/// That is what makes the system ready for content that does not exist yet:</para>
/// <list type="bullet">
///   <item><c>active</c> / <c>passive</c> — set by the profession path, and the reason Realm
///   gathering can reach rewards that safe passive training never will.</item>
///   <item><c>elite</c>, <c>boss</c> — set from an enemy's own identity tags, so elite-only
///   spoils work the day the first elite is authored, with no code change.</item>
///   <item>an enemy's family/role tags (<c>family:goblin</c>), a realm's tags
///   (<c>forest</c>, <c>fey</c>), and each rolled table's own <see cref="LootTableDefinition.Tags"/>.</item>
/// </list>
/// </summary>
public sealed class LootContext
{
    private readonly HashSet<string> _tags;

    public LootContext(int depth = 0, int tier = 1, IEnumerable<string>? tags = null)
    {
        Depth = depth;
        Tier = tier;
        _tags = tags is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Realm depth. 0 is the Hideout — nowhere, and safe.</summary>
    public int Depth { get; }

    public int Tier { get; }

    public IReadOnlyCollection<string> Tags => _tags;

    public bool HasTag(string tag) => _tags.Contains(tag);

    /// <summary>The same circumstances with extra tags folded in. Used as the resolver
    /// descends into a nested table, so a shared table can read the identity of whatever
    /// reached it.</summary>
    public LootContext With(IEnumerable<string> extraTags)
    {
        ArgumentNullException.ThrowIfNull(extraTags);
        var combined = new HashSet<string>(_tags, StringComparer.OrdinalIgnoreCase);
        var added = false;
        foreach (var tag in extraTags)
            added |= combined.Add(tag);

        return added ? new LootContext(Depth, Tier, combined) : this;
    }

    /// <summary>Safe, depth-0 circumstances — the Hideout, passive work.</summary>
    public static LootContext Hideout(params string[] tags) => new(depth: 0, tier: 1, tags);
}

/// <summary>The tags the game itself sets on a <see cref="LootContext"/>, as constants rather
/// than as string literals scattered through the wiring. Content may use any tag it likes;
/// these are the ones the code guarantees.</summary>
public static class LootContextTags
{
    /// <summary>The player was actively performing the work, not running it passively. The only
    /// tag with a structural guarantee behind it: the passive path never sets it.</summary>
    public const string Active = "active";

    /// <summary>Passive/offline work. Set whenever <see cref="Active"/> is not.</summary>
    public const string Passive = "passive";

    /// <summary>The drop is being rolled inside a Realm run rather than at the Hideout.</summary>
    public const string InRealm = "in_realm";
}
