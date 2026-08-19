using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Events;
using Dungeons.Modifiers;
using Dungeons.Professions;
using Dungeons.Rules;

namespace ContentStudio.Services;

/// <summary>
/// The game's closed string vocabularies, read straight off the Core statics so the tool can
/// never drift from the engine. These feed enum dropdowns, tag editors and dictionary-key
/// pickers on the client.
/// </summary>
public static class VocabularyService
{
    public sealed record TagFamilyInfo(string Name, string Cardinality, IReadOnlyList<string>? ClosedValues);

    public static object Snapshot() => new
    {
        moveTags = new
        {
            actions = Sorted(MoveTags.Actions),
            deliveries = Sorted(MoveTags.Deliveries),
            forms = Sorted(MoveTags.Forms),
            mechs = Sorted(MoveTags.Mechs),
            essences = Sorted(MoveTags.Essences),
        },
        damageLanes = Sorted(DamageLanes.All),
        damageAspects = Sorted(DamageAspects.All),
        ruleConditions = Sorted(RuleVocabulary.Conditions),
        ruleEffects = Sorted(RuleVocabulary.Effects),
        gameEvents = Sorted(GameEvents.All),
        moveOps = Sorted(MoveOps.All),
        moveOpFlags = Sorted(MoveOps.Flags),
        moveOpTimingFields = Sorted(MoveOps.TimingFields),
        scopeDimensions = Sorted(ScopeDimensions.All),
        traitCategories = Sorted(EquipmentAssemblyTuning.TraitCategories),
        courseBonusKeys = CourseBonusKeys.All,
        nameFormats = Sorted(ContentValidator.NameFormats),
        tagFamilies = TagFamilies.All
            .Select(family => new TagFamilyInfo(
                family.Name,
                family.Cardinality.ToString(),
                family.ClosedValues is null ? null : Sorted(family.ClosedValues)))
            .ToList(),
        forbiddenNameWords = Sorted(ContentValidator.ForbiddenNameWords),
    };

    private static IReadOnlyList<string> Sorted(IEnumerable<string> values) =>
        values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
}
