using Dungeons.Characters.Modifiers;
using Dungeons.Content;
using Dungeons.Modifiers;
using Xunit;

namespace Dungeons.Tests.Modifiers;

/// <summary>
/// The open modifier vocabulary.
///
/// <para>This is the spine every later system leans on — Base growth, Prefix hooks, Suffix
/// expressions, equipment, professions and Realm effects all contribute through it. The tests
/// that matter are the ones that keep it honest: unknown keys must fail loudly, provenance
/// must survive, and the three combination kinds must behave as documented.</para>
/// </summary>
public class ModifierSetTests
{
    private static DataStore<ModifierKeyDefinition> Keys() =>
        TestPaths.LoadStore<ModifierKeyDefinition>("modifier_keys");

    private static ModifierSet Set() => new(Keys());

    [Fact]
    public void AdditiveKeysSumTheirContributions()
    {
        var set = Set()
            .Add(ModifierKeys.Strength, 2, "base.juggernaut")
            .Add(ModifierKeys.Strength, 3, "prefix.seismic");

        Assert.Equal(5, set.Resolve(ModifierKeys.Strength, baseValue: 0));
        Assert.Equal(15, set.Resolve(ModifierKeys.Strength, baseValue: 10));
    }

    [Fact]
    public void MultiplicativeKeysMultiplyAndDefaultToOne()
    {
        var set = Set()
            .Add(ModifierKeys.IntervalMult, 0.9, "base.juggernaut")
            .Add(ModifierKeys.IntervalMult, 0.5, "prefix.chrono");

        Assert.Equal(1.0, Set().Resolve(ModifierKeys.IntervalMult), 6);
        Assert.Equal(0.45, set.Resolve(ModifierKeys.IntervalMult), 6);
    }

    /// <summary>Flags are on/off rules — "uninterruptible" is not a number you accumulate.</summary>
    [Fact]
    public void FlagKeysAreSetByAnyNonzeroContribution()
    {
        Assert.False(Set().IsSet(ModifierKeys.InterruptImmune));
        Assert.True(Set().Add(ModifierKeys.InterruptImmune, 1, "base.juggernaut").IsSet(ModifierKeys.InterruptImmune));
    }

    /// <summary>
    /// Clamps are declared on the key, so no caller has to remember that an interval multiplier
    /// bottoms out — which is what stops stacked haste producing zero-tick actions
    /// (docs/combat-spec.md §11's hard minimum, expressed as data).
    /// </summary>
    [Fact]
    public void ClampsComeFromTheKeyDefinition()
    {
        var stacked = Set()
            .Add(ModifierKeys.IntervalMult, 0.1, "a")
            .Add(ModifierKeys.IntervalMult, 0.1, "b")
            .Add(ModifierKeys.IntervalMult, 0.1, "c");

        Assert.Equal(0.25, stacked.Resolve(ModifierKeys.IntervalMult), 6);
        Assert.Equal(0.9, Set().Add("combat.resist.magic", 5, "x").Resolve("combat.resist.magic"), 6);
    }

    /// <summary>A mistyped key must fail loudly. Silently ignoring it would make a modifier
    /// quietly do nothing, which is the worst possible failure for content authoring.</summary>
    [Fact]
    public void UnknownKeysThrowOnContributionAndOnResolution()
    {
        Assert.Throws<KeyNotFoundException>(() => Set().Add("attr.charisma", 5, "prefix.bard"));
        Assert.Throws<KeyNotFoundException>(() => Set().Resolve("attr.charisma"));
    }

    /// <summary>"Why is my interval 87?" has to be answerable, so contributions keep their source.</summary>
    [Fact]
    public void ProvenanceSurvivesResolution()
    {
        var set = Set()
            .Add(ModifierKeys.WindupMult, 0.85, "base.juggernaut:momentum")
            .Add(ModifierKeys.WindupMult, 0.95, "equip.heavy_blade");

        var contributions = set.For(ModifierKeys.WindupMult);

        Assert.Equal(2, contributions.Count);
        Assert.Contains(contributions, c => c.Source == "base.juggernaut:momentum");
        Assert.Contains(contributions, c => c.Source == "equip.heavy_blade");
    }

    [Fact]
    public void AKeyWithNoContributionsResolvesToItsBaseline()
    {
        Assert.Equal(0.0, Set().Resolve(ModifierKeys.Strength));
        Assert.Equal(1.0, Set().Resolve(ModifierKeys.DamageMult));
        Assert.False(Set().Has(ModifierKeys.Strength));
    }

    // ---- The registry itself ---------------------------------------------------------------

    /// <summary>Every key code names must exist in the registry, or the constant is a lie.</summary>
    [Fact]
    public void EveryCodeReferencedKeyIsRegistered()
    {
        var keys = Keys();

        foreach (var field in typeof(ModifierKeys).GetFields()
                     .Where(f => f.IsLiteral && f.FieldType == typeof(string)))
        {
            var key = (string)field.GetRawConstantValue()!;
            Assert.True(keys.Contains(key), $"ModifierKeys.{field.Name} = '{key}' is not in the registry.");
        }
    }

    /// <summary>The legacy attribute/resource enum must map cleanly, so one modifier system
    /// serves both the typed attribute path and everything else.</summary>
    [Fact]
    public void EveryLegacyStatIdMapsToARegisteredKey()
    {
        var keys = Keys();

        foreach (var stat in Enum.GetValues<StatId>())
            Assert.True(keys.Contains(ModifierKeys.From(stat)), $"{stat} maps to an unregistered key.");

        Assert.Equal(Enum.GetValues<StatId>().Length, ModifierKeys.StatBacked.Distinct().Count());
    }

    [Fact]
    public void ShippedKeysAreCoherent()
    {
        foreach (var key in Keys().GetAll())
        {
            Assert.False(string.IsNullOrWhiteSpace(key.Name), $"{key.Id} has no display name.");
            Assert.False(string.IsNullOrWhiteSpace(key.Family), $"{key.Id} has no family.");

            // Namespaced by system, lowercase — so keys stay greppable and sort meaningfully.
            Assert.Contains('.', key.Id);
            Assert.Equal(key.Id.ToLowerInvariant(), key.Id);

            if (key.Min is { } min && key.Max is { } max)
                Assert.True(min < max, $"{key.Id} has min >= max.");

            // A multiplicative key clamped at 0 minimum can be zeroed out entirely, which is
            // almost never intended — it silently deletes whatever it multiplies.
            if (key.Kind == ModifierKind.Multiplicative)
                Assert.True(key.Min is null or >= 0, $"{key.Id} allows a negative multiplier.");
        }
    }
}
