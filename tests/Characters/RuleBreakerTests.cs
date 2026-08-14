using Dungeons.Characters;
using Dungeons.Characters.Composition;
using Dungeons.Characters.Rules;
using Xunit;
using static Dungeons.Tests.Characters.CharacterTestData;

namespace Dungeons.Tests.Characters;

/// <summary>
/// Proves the MVP requirement that composition can alter actual gameplay: two
/// characters differing only by suffix respond to Health changes in opposite ways.
/// </summary>
public class RuleBreakerTests
{
    private static Character Compose(string suffixId, params string[] ruleIds)
    {
        var composer = Composer(
            Store(new SpeciesDefinition { Id = "species.s", Name = "Human" }),
            Store(new BaseClassDefinition { Id = "class.c", Name = "Bastion", PrimaryResource = ResourceType.Stamina }),
            Store(new PrefixDefinition { Id = "prefix.p", Name = "Plain" }),
            Store(new SuffixDefinition { Id = suffixId, Name = "Of Something", RuleIds = ruleIds }),
            RealRules());

        var blueprint = composer.Compose(new CharacterBuild("species.s", "class.c", "prefix.p", suffixId), AttributeSet.Uniform(5));
        return new Character(blueprint);
    }

    [Fact]
    public void UnreasonableConfidence_BonusPresentAtFullHealth_LostWhenWounded()
    {
        var character = Compose("suffix.uc", UnreasonableConfidenceRule.Id);
        Assert.Equal(65, character.Health.Max); // CON5/END5 → 20+30+15

        // Full health: +2 to every attribute.
        Assert.Equal(7, character.EffectiveAttributes.Strength);
        Assert.Equal(7, character.EffectiveAttributes.Luck);
        Assert.Equal(5, character.BaseAttributes.Strength); // base unchanged

        // Drop below 90% (58.5): bonus vanishes.
        character.TakeDamage(10); // 55/65 ≈ 0.846
        Assert.Equal(5, character.EffectiveAttributes.Strength);

        // Heal back to full: bonus returns.
        character.RestoreAll();
        Assert.Equal(7, character.EffectiveAttributes.Strength);
    }

    [Fact]
    public void InappropriateOptimism_BonusOnlyWhenBadlyWounded()
    {
        var character = Compose("suffix.io", InappropriateOptimismRule.Id);

        // Full health: no bonus.
        Assert.Equal(5, character.EffectiveAttributes.Strength);

        // Drop to <= 34% (22.1): +3 STR / +3 DEX.
        character.TakeDamage(45); // 20/65 ≈ 0.307
        Assert.Equal(8, character.EffectiveAttributes.Strength);
        Assert.Equal(8, character.EffectiveAttributes.Dexterity);
        Assert.Equal(5, character.EffectiveAttributes.Intelligence); // untouched

        character.RestoreAll();
        Assert.Equal(5, character.EffectiveAttributes.Strength);
    }

    [Fact]
    public void SameCharacterMinusSuffix_HasNoDynamicBehaviour()
    {
        var plain = Compose("suffix.none"); // no rule ids
        plain.TakeDamage(60);
        Assert.Equal(5, plain.EffectiveAttributes.Strength);
        Assert.Empty(plain.Blueprint.Rules);
    }
}
