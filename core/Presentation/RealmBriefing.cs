using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Loot;
using Dungeons.Realms;

namespace Dungeons.Presentation;

/// <summary>How a damage lane lands on a creature. <see cref="Fraction"/> is the resistance as
/// authored, so a negative value is a real weakness rather than a small resistance.</summary>
public sealed record ResistanceReading(string Lane, double Fraction);

/// <summary>
/// Something that lives here, once the party has learned to read the place.
///
/// <para>Three separate readings because a creature is counterable in three separate ways, and
/// flattening them into one "weak to" list would lose the distinction the player acts on:
/// <see cref="VulnerableDamageTypes"/> is how you hit it (Slashing, Crushing…),
/// <see cref="ExposedLanes"/> is what burns it, and <see cref="ResistedLanes"/> is what to leave
/// at home.</para>
/// </summary>
public sealed record KnownThreat(
    string Name,
    string LocationName,
    int Depth,
    EnemyRank Rank,
    IReadOnlyList<string> VulnerableDamageTypes,
    IReadOnlyList<ResistanceReading> ExposedLanes,
    IReadOnlyList<ResistanceReading> ResistedLanes);

/// <summary>Ground that costs health to cross.</summary>
public sealed record KnownHazard(string Name, int Depth, int HealthCost);

/// <summary>A working worth spending run-time on, and what it takes to work it.</summary>
public sealed record KnownResource(
    string LocationName,
    int Depth,
    string ProfessionName,
    string ActionName,
    int RequiredLevel);

/// <summary>A way through, or a way out.</summary>
public sealed record KnownRoute(string Name, int Depth, RealmLocationType Type, bool Hidden);

/// <summary>Something this place is known to give up, and how ordinary it is.</summary>
public sealed record KnownYield(string MaterialName, LootRarity Rarity);

/// <summary>
/// Everything the party knows about a Realm they have <b>not entered yet</b> — the read-model
/// the preparation screen is made of.
///
/// <para>This lives in <see cref="Dungeons.Presentation"/> for the same reason
/// <see cref="AssayLens"/> does: it <b>redacts a reading, it never changes one</b>. The Realm is
/// exactly as lethal at 0 knowledge as at 560; what changes is how much of it the player is
/// allowed to see before committing. Knowledge buys options, never damage (GDD §11.4).</para>
///
/// <para><b>Every gate goes through <see cref="RealmKnowledgeLevels.Reveals"/>.</b> There is no
/// second threshold table here, and adding one would be the way this and the in-run intel
/// quietly start disagreeing about what the player has earned.</para>
/// </summary>
public sealed record RealmBriefing(
    string RealmId,
    string RealmName,
    IReadOnlyList<string> Tags,
    int MaxDepth,
    int Knowledge,
    IReadOnlyList<RealmInsight> Unlocked,
    (RealmInsight Insight, int Required)? NextInsight,
    IReadOnlyList<KnownThreat> Threats,
    IReadOnlyList<KnownHazard> Hazards,
    IReadOnlyList<KnownResource> Resources,
    IReadOnlyList<KnownRoute> Routes,
    IReadOnlyList<KnownYield> Yield,
    int DeepestEntry)
{
    public bool Knows(RealmInsight insight) => RealmKnowledgeLevels.Reveals(Knowledge, insight);

    /// <summary>
    /// Compiles what <paramref name="knowledge"/> has earned the right to see of
    /// <paramref name="realm"/>.
    ///
    /// <para>Takes the whole <see cref="ContentBundle"/> rather than six stores: a briefing
    /// reaches across enemies, professions and the realm graph, and threading the stores
    /// individually would put a parameter list at every call site that grows every time the
    /// screen learns to show one more thing.</para>
    /// </summary>
    public static RealmBriefing Compile(ContentBundle content, RealmDefinition realm, int knowledge)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(realm);

        return new RealmBriefing(
            realm.Id,
            realm.Name,
            realm.Tags,
            realm.MaxDepth,
            knowledge,
            RealmKnowledgeLevels.Unlocked(knowledge),
            RealmKnowledgeLevels.Next(knowledge),
            CompileThreats(content, realm, knowledge),
            CompileHazards(realm, knowledge),
            CompileResources(content, realm, knowledge),
            CompileRoutes(realm, knowledge),
            CompileYield(content, realm, knowledge),
            RealmRun.DeepestReachableEntry(realm, knowledge));
    }

    /// <summary>
    /// What this place is made of, gated on <see cref="RealmInsight.CommonResources"/> — the
    /// cheapest rung, so a first expedition comes home knowing something.
    ///
    /// <para>Walked out of the loot tables the realm's own nodes and creatures already point at,
    /// with <see cref="LootReachability.ItemsReachableFrom"/> — the function written for exactly
    /// this question ("what can this source <em>possibly</em> drop?"). <b>Zero new content:</b>
    /// authoring a per-realm material list beside the tables would be the same facts written
    /// twice, and the second copy would be the one that goes stale.</para>
    /// </summary>
    private static IReadOnlyList<KnownYield> CompileYield(
        ContentBundle content,
        RealmDefinition realm,
        int knowledge)
    {
        if (!RealmKnowledgeLevels.Reveals(knowledge, RealmInsight.CommonResources))
            return Array.Empty<KnownYield>();

        var tableIds = new List<string>();
        foreach (var location in realm.VisibleAt(knowledge))
        {
            if (location.LootTableId is { Length: > 0 } nodeTable)
                tableIds.Add(nodeTable);

            if (location.ActorId is { Length: > 0 } actorId && content.Actors.TryGetById(actorId, out var actor))
            {
                var resolved = ActorResolver.Resolve(actor, content.EnemyFamilies, content.EnemyRoles, content.AiProfiles);
                tableIds.AddRange(resolved.LootTableIds);
            }
        }

        var materialIds = tableIds
            .SelectMany(tableId => LootReachability.ItemsReachableFrom(content.LootTables, tableId))
            .ToHashSet(StringComparer.Ordinal);

        return materialIds
            .Where(content.Materials.Contains)
            .Select(materialId => content.Materials.GetById(materialId))
            .Select(material => new KnownYield(material.Name, LootRarities.FromTags(material.Tags) ?? LootRarity.Common))
            .OrderBy(entry => entry.Rarity)
            .ThenBy(entry => entry.MaterialName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// What lives here, gated on <see cref="RealmInsight.EnemyWeaknesses"/>. The counter was
    /// always there — reading it beforehand is what makes it a plan instead of a surprise.
    /// </summary>
    private static IReadOnlyList<KnownThreat> CompileThreats(
        ContentBundle content,
        RealmDefinition realm,
        int knowledge)
    {
        if (!RealmKnowledgeLevels.Reveals(knowledge, RealmInsight.EnemyWeaknesses))
            return Array.Empty<KnownThreat>();

        var threats = new List<KnownThreat>();
        foreach (var location in realm.VisibleAt(knowledge)
                     .Where(location => location.Type == RealmLocationType.Combat))
        {
            if (location.ActorId is not { Length: > 0 } actorId
                || !content.Actors.TryGetById(actorId, out var actor))
                continue;

            var resolved = ActorResolver.Resolve(actor, content.EnemyFamilies, content.EnemyRoles, content.AiProfiles);

            threats.Add(new KnownThreat(
                resolved.Name,
                location.Name,
                location.Depth,
                EnemyRanks.Of(resolved.Tags),
                resolved.Vulnerable.Where(type => type.Value > 1.0)
                    .OrderByDescending(type => type.Value)
                    .Select(type => type.Key)
                    .ToList(),
                LanesWhere(resolved.Resistances, resistance => resistance < 0),
                LanesWhere(resolved.Resistances, resistance => resistance > 0)));
        }

        return threats.OrderBy(threat => threat.Depth).ThenBy(threat => threat.Name).ToList();
    }

    /// <summary>
    /// The lanes matching a sign test, strongest first.
    ///
    /// <para>A <b>negative</b> resistance is a real weakness, not a small resistance — it is how
    /// a troll burns. Splitting on the sign here is what stops the briefing repeating the in-run
    /// intel's omission, which lists only what a creature shrugs off and silently drops the most
    /// actionable fact about it.</para>
    /// </summary>
    private static IReadOnlyList<ResistanceReading> LanesWhere(
        IReadOnlyDictionary<string, double> resistances,
        Func<double, bool> matches) =>
        resistances.Where(lane => matches(lane.Value))
            .OrderByDescending(lane => Math.Abs(lane.Value))
            .Select(lane => new ResistanceReading(lane.Key, lane.Value))
            .ToList();

    private static IReadOnlyList<KnownHazard> CompileHazards(RealmDefinition realm, int knowledge) =>
        RealmKnowledgeLevels.Reveals(knowledge, RealmInsight.Hazards)
            ? realm.VisibleAt(knowledge)
                .Where(location => location.Type == RealmLocationType.Hazard)
                .OrderBy(location => location.Depth)
                .Select(location => new KnownHazard(location.Name, location.Depth, location.HazardDamage))
                .ToList()
            : Array.Empty<KnownHazard>();

    /// <summary>
    /// The rich workings, gated on <see cref="RealmInsight.RichNodes"/>. "Rich" is the same rule
    /// the in-run intel uses — a Gather node carrying a Realm loot table <em>on top of</em> its
    /// profession action, which is exactly what makes it worth walking to.
    /// </summary>
    private static IReadOnlyList<KnownResource> CompileResources(
        ContentBundle content,
        RealmDefinition realm,
        int knowledge)
    {
        if (!RealmKnowledgeLevels.Reveals(knowledge, RealmInsight.RichNodes))
            return Array.Empty<KnownResource>();

        var resources = new List<KnownResource>();
        foreach (var location in realm.VisibleAt(knowledge)
                     .Where(location => location.Type == RealmLocationType.Gather)
                     .Where(location => !string.IsNullOrEmpty(location.LootTableId)))
        {
            if (location.ProfessionActionId is not { Length: > 0 } actionId
                || !content.Actions.TryGetById(actionId, out var action))
                continue;

            var professionName = content.Professions.TryGetById(action.ProfessionId, out var profession)
                ? profession.Name
                : action.ProfessionId;

            resources.Add(new KnownResource(
                location.Name, location.Depth, professionName, action.Name, action.RequiredLevel));
        }

        return resources.OrderBy(resource => resource.Depth).ThenBy(resource => resource.LocationName).ToList();
    }

    /// <summary>
    /// Ways through and ways out — the one section with <b>two</b> gates, because they answer
    /// different questions. <see cref="RealmInsight.HiddenRoutes"/> reveals the nodes that were
    /// in the graph all along; <see cref="RealmInsight.ExtractionRoutes"/> reveals where the
    /// exits and stairs are. A hidden exit needs both, which is why the filter is nested rather
    /// than combined.
    /// </summary>
    private static IReadOnlyList<KnownRoute> CompileRoutes(RealmDefinition realm, int knowledge)
    {
        var knowsExits = RealmKnowledgeLevels.Reveals(knowledge, RealmInsight.ExtractionRoutes);

        return realm.VisibleAt(knowledge)
            .Where(location => location.Hidden
                || (knowsExits && location.Type is RealmLocationType.Extraction or RealmLocationType.Descent))
            .OrderBy(location => location.Depth)
            .ThenBy(location => location.Name)
            .Select(location => new KnownRoute(location.Name, location.Depth, location.Type, location.Hidden))
            .ToList();
    }
}
