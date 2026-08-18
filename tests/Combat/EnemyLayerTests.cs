using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Loot;
using Xunit;

namespace Dungeons.Tests.Combat;

/// <summary>
/// The physiology/archetype layer the enemy-breadth pass added: families are what a body IS,
/// roles are what an archetype DOES, and an actor is the pair plus a name and a moveset.
///
/// <para>These are rules about the <em>layer</em>, not about any one creature — they hold as
/// actors land in waves, and they are what stops the twenty-first family from being a copy of
/// the first with the numbers nudged.</para>
/// </summary>
public class EnemyLayerTests
{
    private static DataStore<EnemyFamilyDefinition> Families() =>
        TestPaths.LoadStore<EnemyFamilyDefinition>("enemy_families");

    private static DataStore<CombatRoleDefinition> Roles() =>
        TestPaths.LoadStore<CombatRoleDefinition>("enemy_roles");

    // ---- The layer is broad enough to be worth having -----------------------------------------

    /// <summary>
    /// Breadth is the point of the pass, so it is asserted rather than assumed. The floor is
    /// deliberately well below what ships: this test should fail when someone deletes half the
    /// file, not when they merge two families that turned out to be the same body.
    /// </summary>
    [Fact]
    public void TheFamilyLayerCoversTheMajorCreatureGroups()
    {
        var families = Families().GetAll();
        Assert.True(families.Count >= 18, $"only {families.Count} families — the breadth pass is regressing.");

        // The groups a realm cannot be populated without. Named individually because "18 of
        // something" would pass with eighteen kinds of goblin.
        foreach (var required in new[]
                 {
                     "family.goblin", "family.orc", "family.troll", "family.giant", "family.draconic",
                     "family.beast", "family.vermin", "family.undead", "family.spirit",
                     "family.construct", "family.ooze", "family.plant", "family.elemental",
                     "family.fey", "family.fiend", "family.human",
                 })
            Assert.True(Families().Contains(required), $"{required} is missing from the family layer.");
    }

    // ---- Every family is a distinct body ------------------------------------------------------

    /// <summary>
    /// A family exists to be a <em>different body</em>. Two families with identical attributes,
    /// resistances and vulnerabilities are one family with two names — the enemy-side version of
    /// the rule that no two equipment forms may be the same form.
    /// </summary>
    [Fact]
    public void NoTwoFamiliesAreTheSameBody()
    {
        string Physiology(EnemyFamilyDefinition family) =>
            $"{family.Attributes.Strength},{family.Attributes.Dexterity},{family.Attributes.Intelligence}," +
            $"{family.Attributes.Constitution},{family.Attributes.Wisdom},{family.Attributes.Endurance}|" +
            string.Join(",", family.Resistances.OrderBy(r => r.Key).Select(r => $"{r.Key}{r.Value}")) + "|" +
            string.Join(",", family.Vulnerable.OrderBy(v => v.Key).Select(v => $"{v.Key}{v.Value}"));

        var duplicates = Families().GetAll()
            .GroupBy(Physiology)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(" == ", group.Select(f => f.Id)))
            .ToList();

        Assert.True(duplicates.Count == 0, "families that are the same body: " + string.Join(" | ", duplicates));
    }

    /// <summary>Every family must give the player something its body is actually made of.
    /// A family with no drop table is a kill that pays nothing.</summary>
    [Fact]
    public void EveryFamilyDropsSomething()
    {
        var lootTables = TestPaths.LoadStore<LootTableDefinition>("loot_tables");

        foreach (var family in Families().GetAll())
        {
            Assert.False(string.IsNullOrEmpty(family.LootTableId), $"{family.Id} has no loot table.");
            Assert.True(lootTables.Contains(family.LootTableId!), $"{family.Id} points at a missing table.");
        }
    }

    /// <summary>
    /// Every family must pay out <b>structurally</b>, not on average.
    ///
    /// <para>This exists because the probabilistic check caught the same bug twice. A family
    /// whose only guaranteed line is <c>loot.shared.creature_remains</c> looks safe and is not:
    /// that table carries a <c>dropsNothing</c> weight, so a kill can pay nothing and whether it
    /// does is down to the seed. Seven families shipped that way, and the roll-based test only
    /// noticed when a later wave added an actor unlucky enough to hit it.</para>
    ///
    /// <para>So: at least one <c>alwaysDrops</c> entry must be a direct item, or a table that
    /// itself guarantees one. No dice involved.</para>
    /// </summary>
    [Fact]
    public void EveryFamilyLeavesSomethingBehindWithoutRelyingOnLuck()
    {
        var lootTables = TestPaths.LoadStore<LootTableDefinition>("loot_tables");

        bool GuaranteesAnItem(string tableId, HashSet<string> visited)
        {
            if (!visited.Add(tableId) || !lootTables.TryGetById(tableId, out var table))
                return false;

            return table.AlwaysDrops.Any(entry =>
                !string.IsNullOrEmpty(entry.ItemId)
                || (!string.IsNullOrEmpty(entry.TableId) && GuaranteesAnItem(entry.TableId, visited)));
        }

        foreach (var family in Families().GetAll())
            Assert.True(
                GuaranteesAnItem(family.LootTableId!, new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
                $"{family.Id} can roll an empty haul — nothing in its alwaysDrops chain is certain.");
    }

    // ---- Roles ------------------------------------------------------------------------------

    /// <summary>
    /// A role without a brain falls back to uniform move selection, which makes it a stat block
    /// wearing an archetype's name. Every role must actually behave like itself.
    /// </summary>
    [Fact]
    public void EveryRoleHasABrainAndADropTable()
    {
        var profiles = TestPaths.LoadStore<AiProfileDefinition>("ai_profiles");
        var lootTables = TestPaths.LoadStore<LootTableDefinition>("loot_tables");

        foreach (var role in Roles().GetAll())
        {
            Assert.False(string.IsNullOrEmpty(role.AiProfile), $"{role.Id} has no AI profile.");
            Assert.True(profiles.Contains(role.AiProfile!), $"{role.Id} points at a missing AI profile.");
            Assert.False(string.IsNullOrEmpty(role.LootTableId), $"{role.Id} has no loot table.");
            Assert.True(lootTables.Contains(role.LootTableId!), $"{role.Id} points at a missing table.");
        }
    }

    /// <summary>
    /// Roles are deltas, so they must actually differ from each other in what they adjust —
    /// otherwise "Guardian" and "Brute" are the same enemy twice.
    /// </summary>
    [Fact]
    public void NoTwoRolesAreTheSameArchetype()
    {
        string Shape(CombatRoleDefinition role) =>
            $"{role.AttributeTweaks.Strength},{role.AttributeTweaks.Dexterity},{role.AttributeTweaks.Constitution}," +
            $"{role.AttributeTweaks.Intelligence},{role.AttributeTweaks.Endurance}|{role.Armor}|{role.AiProfile}";

        var duplicates = Roles().GetAll()
            .GroupBy(Shape)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(" == ", group.Select(r => r.Id)))
            .ToList();

        Assert.True(duplicates.Count == 0, "roles that are the same archetype: " + string.Join(" | ", duplicates));
    }

    // ---- The layer composes -------------------------------------------------------------------

    /// <summary>
    /// The claim the whole layering rests on: <b>any family paired with any role resolves</b>.
    /// If some combination throws or produces a dead actor, then families and roles are not
    /// really independent and every future actor is a special case.
    /// </summary>
    [Fact]
    public void EveryFamilyComposesWithEveryRole()
    {
        var families = Families();
        var roles = Roles();
        var profiles = TestPaths.LoadStore<AiProfileDefinition>("ai_profiles");

        foreach (var family in families.GetAll())
        foreach (var role in roles.GetAll())
        {
            var actor = new ActorDefinition { Id = "actor.probe", Name = "Probe", Family = family.Id, Role = role.Id };
            var resolved = ActorResolver.Resolve(actor, families, roles, profiles);

            Assert.True(resolved.Resources.Health > 0,
                $"{family.Id} + {role.Id} resolves to {resolved.Resources.Health} health — an enemy that is already dead.");
            Assert.NotEmpty(resolved.Ai);
        }
    }
}
