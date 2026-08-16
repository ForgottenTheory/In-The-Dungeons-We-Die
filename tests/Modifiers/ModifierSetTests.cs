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
/// must survive, and the combination kinds must behave as documented.</para>
///
/// <para>Since D-12 there is a second class of honesty to keep: a scoped key resolved in the
/// wrong context must <b>throw</b> rather than return a plausible number. Those tests are the
/// ones guarding the only silent-wrong-answer failure mode in the effect package.</para>
/// </summary>
public class ModifierSetTests
{
    private static DataStore<ModifierKeyDefinition> Keys() =>
        TestPaths.LoadStore<ModifierKeyDefinition>("modifier_keys");

    private static ModifierSet Set() => new(Keys());

    /// <summary>A registry authored inline, for key shapes the shipped content has no use for yet.</summary>
    private static ModifierSet SetFrom(string keysJson)
    {
        var store = new DataStore<ModifierKeyDefinition>();
        store.LoadMany(keysJson);
        return new ModifierSet(store);
    }

    private static ModifierContext Fishing => ModifierContext.For(ScopeDimensions.Profession, "fishing");
    private static ModifierContext Mining => ModifierContext.For(ScopeDimensions.Profession, "mining");

    private const string ProfessionInterval = "profession.interval.mult";
    private const string PreserveChance = "profession.preserve.chance";

    [Fact]
    public void AdditiveKeysSumTheirContributions()
    {
        var set = Set()
            .Add(ModifierKeys.Strength, 2, "base.juggernaut")
            .Add(ModifierKeys.Strength, 3, "prefix.seismic");

        Assert.Equal(5, set.Resolve(ModifierKeys.Strength, ModifierContext.None, baseValue: 0));
        Assert.Equal(15, set.Resolve(ModifierKeys.Strength, ModifierContext.None, baseValue: 10));
    }

    [Fact]
    public void MultiplicativeKeysMultiplyAndDefaultToOne()
    {
        var set = Set()
            .Add(ModifierKeys.IntervalMult, 0.9, "base.juggernaut")
            .Add(ModifierKeys.IntervalMult, 0.5, "prefix.chrono");

        Assert.Equal(1.0, Set().Resolve(ModifierKeys.IntervalMult, ModifierContext.None), 6);
        Assert.Equal(0.45, set.Resolve(ModifierKeys.IntervalMult, ModifierContext.None), 6);
    }

    /// <summary>Flags are on/off rules — "uninterruptible" is not a number you accumulate.</summary>
    [Fact]
    public void FlagKeysAreSetByAnyNonzeroContribution()
    {
        Assert.False(Set().IsSet(ModifierKeys.InterruptImmune, ModifierContext.None));
        Assert.True(Set()
            .Add(ModifierKeys.InterruptImmune, 1, "base.juggernaut")
            .IsSet(ModifierKeys.InterruptImmune, ModifierContext.None));
    }

    /// <summary>
    /// Clamps are declared on the key, so no caller has to remember that an interval multiplier
    /// bottoms out — which is what stops stacked haste producing zero-tick actions
    /// (docs/effect-foundation.md §4.3's hard minimum, expressed as data).
    /// </summary>
    [Fact]
    public void ClampsComeFromTheKeyDefinition()
    {
        var stacked = Set()
            .Add(ModifierKeys.IntervalMult, 0.1, "a")
            .Add(ModifierKeys.IntervalMult, 0.1, "b")
            .Add(ModifierKeys.IntervalMult, 0.1, "c");

        Assert.Equal(0.25, stacked.Resolve(ModifierKeys.IntervalMult, ModifierContext.None), 6);
        Assert.Equal(0.9, Set().Add("combat.resist.magic", 5, "x").Resolve("combat.resist.magic", ModifierContext.None), 6);
    }

    /// <summary>A mistyped key must fail loudly. Silently ignoring it would make a modifier
    /// quietly do nothing, which is the worst possible failure for content authoring.</summary>
    [Fact]
    public void UnknownKeysThrowOnContributionAndOnResolution()
    {
        Assert.Throws<KeyNotFoundException>(() => Set().Add("attr.charisma", 5, "prefix.bard"));
        Assert.Throws<KeyNotFoundException>(() => Set().Resolve("attr.charisma", ModifierContext.None));
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
        Assert.Equal(0.0, Set().Resolve(ModifierKeys.Strength, ModifierContext.None));
        Assert.Equal(1.0, Set().Resolve(ModifierKeys.DamageMult, ModifierContext.None));
        Assert.False(Set().Has(ModifierKeys.Strength));
    }

    // ---- Scoped contributions (D-12) ---------------------------------------------------------

    /// <summary>
    /// The headline case, in one test: the same key, granted twice, once by a tool that only
    /// helps the profession it is built for and once by a ring that helps everything. Before
    /// D-12 this was inexpressible without forking the key per profession.
    /// </summary>
    [Fact]
    public void AScopedContributionAppliesOnlyInItsScopeAndAnUnscopedOneAppliesEverywhere()
    {
        var set = Set()
            .Add(ProfessionInterval, 0.87, "Tidecaller Rod", new ModifierScope(ScopeDimensions.Profession, "fishing"))
            .Add(ProfessionInterval, 0.95, "Ring of Haste");

        Assert.Equal(0.87 * 0.95, set.Resolve(ProfessionInterval, Fishing), 6);
        Assert.Equal(0.95, set.Resolve(ProfessionInterval, Mining), 6);
    }

    /// <summary>
    /// The worked trace from §4.2.2, read back as numbers:
    /// <code>
    ///   Interval      120 × 0.87 [Tidecaller Rod · profession:fishing]
    ///                     × 0.90 [Mastery 41 · profession:fishing]
    ///                 → 94 ticks
    /// </code>
    /// <para>Note the shape of the call: the <i>multiplier</i> is resolved, then applied. Passing
    /// 120 as the base value instead would hand the key's 0.2 floor an absolute tick count to
    /// clamp, and the floor exists to stop stacked haste, not to stop short intervals.</para>
    /// </summary>
    [Fact]
    public void TheWorkedFishingTraceResolvesToNinetyFourTicks()
    {
        var rod = new ModifierScope(ScopeDimensions.Profession, "fishing");
        var set = Set()
            .Add(ProfessionInterval, 0.87, "Tidecaller Rod", rod)
            .Add(ProfessionInterval, 0.90, "Mastery 41", rod);

        Assert.Equal(94, Math.Round(120 * set.Resolve(ProfessionInterval, Fishing)));

        // The same rod, mining: it is a fishing rod.
        Assert.Equal(120, Math.Round(120 * set.Resolve(ProfessionInterval, Mining)));
    }

    /// <summary>Scope matching is case-insensitive, like every other id comparison here.</summary>
    [Fact]
    public void ScopeMatchingIgnoresCasing()
    {
        var set = Set().Add(ProfessionInterval, 0.5, "rod", new ModifierScope(ScopeDimensions.Profession, "Fishing"));

        Assert.Equal(0.5, set.Resolve(ProfessionInterval, ModifierContext.For("PROFESSION", "fishing")), 6);
    }

    /// <summary>
    /// <c>For</c> answers "what was granted", <c>Applicable</c> answers "what counted here". The
    /// Character Lab needs the first; a hit trace needs the second, and rendering each applied
    /// contribution with its scope is the visibility guard behind the structural ones.
    /// </summary>
    [Fact]
    public void ApplicableFiltersByContextWhileProvenanceKeepsEverything()
    {
        var set = Set()
            .Add(ProfessionInterval, 0.87, "Tidecaller Rod", new ModifierScope(ScopeDimensions.Profession, "fishing"))
            .Add(ProfessionInterval, 0.95, "Ring of Haste");

        Assert.Equal(2, set.For(ProfessionInterval).Count);
        Assert.Single(set.Applicable(ProfessionInterval, Mining));
        Assert.Equal(2, set.Applicable(ProfessionInterval, Fishing).Count);

        Assert.Contains("profession:fishing", set.For(ProfessionInterval)[0].ToString());
    }

    // ---- The three guards on the wrong-context failure mode (§4.2.2) -------------------------

    /// <summary>
    /// <b>The load-bearing test of E3b.</b> A scoped key resolved without its dimension must
    /// throw — not return the unscoped subtotal, and not return the baseline. Both of those look
    /// like answers, and an answer that is quietly wrong is worse than a missing feature because
    /// nothing surfaces it.
    /// </summary>
    [Fact]
    public void ResolvingAScopedKeyWithoutItsDimensionThrows()
    {
        var set = Set()
            .Add(ProfessionInterval, 0.87, "Tidecaller Rod", new ModifierScope(ScopeDimensions.Profession, "fishing"))
            .Add(ProfessionInterval, 0.95, "Ring of Haste");

        // Not 0.95 (the unscoped subtotal), not 1.0 (the baseline).
        var error = Assert.Throws<InvalidOperationException>(
            () => set.Resolve(ProfessionInterval, ModifierContext.None));

        Assert.Contains("profession", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Throwing must not depend on anything having contributed — an empty set resolved
    /// in the wrong context is exactly as wrong, and exactly as silent.</summary>
    [Fact]
    public void ResolvingAScopedKeyWithoutItsDimensionThrowsEvenWhenNothingContributed()
    {
        Assert.Throws<InvalidOperationException>(() => Set().Resolve(ProfessionInterval, ModifierContext.None));
        Assert.Throws<InvalidOperationException>(
            () => Set().Resolve(ProfessionInterval, ModifierContext.For(ScopeDimensions.Lane, "physical")));
    }

    /// <summary>A scope the key never declared could only ever match nothing, so it is content
    /// that silently does nothing — rejected where it is written, not where it fails.</summary>
    [Fact]
    public void AContributionScopedByTheWrongDimensionIsRejectedAtAddTime()
    {
        var error = Assert.Throws<ArgumentException>(
            () => Set().Add(ProfessionInterval, 0.9, "rod", new ModifierScope(ScopeDimensions.Lane, "physical")));

        Assert.Contains(ProfessionInterval, error.Message);
    }

    [Fact]
    public void AScopeOnAGlobalKeyIsRejectedAtAddTime()
    {
        Assert.Throws<ArgumentException>(
            () => Set().Add(ModifierKeys.Strength, 5, "ring", new ModifierScope(ScopeDimensions.Item, "self")));
    }

    /// <summary>The dimension vocabulary is closed (§4.2.1) — an invented dimension is a scope
    /// nothing will ever supply.</summary>
    [Fact]
    public void UnknownScopeDimensionsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new ModifierScope("weather", "rain"));
        Assert.Throws<ArgumentException>(() => ModifierContext.For("weather", "rain"));
        Assert.Throws<ArgumentException>(() => ModifierContext.None.With("weather", "rain"));
    }

    /// <summary>
    /// The pinning test from §4.2.2: resolve every registered key with no context and assert
    /// that no scoped key ever quietly falls back to its baseline. Every one of them throws;
    /// every global one answers.
    /// </summary>
    [Fact]
    public void NoScopedKeyEverSilentlyDropsToItsBaseline()
    {
        var keys = Keys();
        var set = new ModifierSet(keys);

        foreach (var key in keys.GetAll())
        {
            if (key.IsScoped)
            {
                Assert.Throws<InvalidOperationException>(() => set.Resolve(key.Id, ModifierContext.None));

                // …and answers once the dimension is supplied.
                Assert.Equal(Untouched(key), set.Resolve(key.Id, ModifierContext.For(key.ScopedBy, "anything")), 6);
            }
            else
            {
                Assert.Equal(Untouched(key), set.Resolve(key.Id, ModifierContext.None), 6);
            }
        }
    }

    /// <summary>What a key reads as when nothing has contributed. Not simply its baseline: the
    /// clamps are part of the answer, and <c>resource.max_health</c> has a min of 1 precisely so
    /// that "nothing contributed" can never mean zero health.</summary>
    private static double Untouched(ModifierKeyDefinition key)
    {
        var value = key.Baseline;
        if (key.Min is { } min)
            value = Math.Max(min, value);
        if (key.Max is { } max)
            value = Math.Min(max, value);
        return value;
    }

    // ---- Stacking modes (D-13) ---------------------------------------------------------------

    /// <summary>
    /// The arithmetic from §4.3, which is the entire argument for the mode: stacking feels
    /// additive at low values and is mathematically incapable of reaching certainty.
    /// </summary>
    [Theory]
    [InlineData(3, 0.271)]
    [InlineData(10, 0.6513)]
    [InlineData(20, 0.8784)]
    [InlineData(40, 0.9852)]
    public void DiminishingStackingNeverReachesCertainty(int sources, double expected)
    {
        var set = SetFrom(Uncapped);
        for (var i = 0; i < sources; i++)
            set.Add("combat.avoid.test", 0.10, $"affix.{i}");

        Assert.Equal(expected, set.Resolve("combat.avoid.test", ModifierContext.None), 4);
    }

    /// <summary>The base value is one more term in the product, so an inherent 5% avoidance
    /// combines with a granted 10% the same way two granted sources would.</summary>
    [Fact]
    public void DiminishingTreatsTheBaseValueAsAnotherSource()
    {
        var set = SetFrom(Uncapped).Add("combat.avoid.test", 0.10, "affix");

        Assert.Equal(0.145, set.Resolve("combat.avoid.test", ModifierContext.None, baseValue: 0.05), 6);
    }

    /// <summary>An avoidance key with the cap deliberately left off, so the curve can be read on
    /// its own. Shipped avoidance keys are capped — that is what D-13 means by "both apply" —
    /// and no shipped key is used here because D-07 retires <c>combat.dodge.chance</c> in favour
    /// of keys that do not exist yet.</summary>
    private const string Uncapped =
        """[{ "id": "combat.avoid.test", "name": "Avoidance", "kind": "diminishing", "family": "defence" }]""";

    /// <summary>
    /// Curve and cap are both live and neither substitutes for the other: the asymptote bounds
    /// the limit, the max bounds the reachable value, and one Signature affix rolling large is
    /// exactly why the cap stays.
    /// </summary>
    [Fact]
    public void DiminishingStillObeysTheKeysCeiling()
    {
        var set = Set()
            .Add(PreserveChance, 0.60, "affix.a", new ModifierScope(ScopeDimensions.Profession, "mining"))
            .Add(PreserveChance, 0.60, "affix.b", new ModifierScope(ScopeDimensions.Profession, "mining"));

        // 1 − 0.4² = 0.84, clamped to the key's max of 0.8.
        Assert.Equal(0.8, set.Resolve(PreserveChance, Mining), 6);
    }

    /// <summary>Barrier from three sources is the strongest Barrier, not their sum.</summary>
    [Fact]
    public void HighestOnlyTakesTheStrongestSourceAndIgnoresTheRest()
    {
        var set = SetFrom(
            """[{ "id": "combat.barrier", "name": "Barrier", "kind": "highest_only", "family": "defence", "min": 0 }]""");

        set.Add("combat.barrier", 20, "status.stoneskin")
           .Add("combat.barrier", 35, "equip.aegis")
           .Add("combat.barrier", 15, "prefix.warded");

        Assert.Equal(35, set.Resolve("combat.barrier", ModifierContext.None), 6);
        Assert.Equal(0, SetFrom("""[{ "id": "combat.barrier", "name": "Barrier", "kind": "highest_only", "family": "defence" }]""")
            .Resolve("combat.barrier", ModifierContext.None), 6);
    }

    /// <summary>The docs and the content author <c>highest_only</c>; the enum member is
    /// <c>HighestOnly</c>. The converter is what keeps those from having to agree.</summary>
    [Theory]
    [InlineData("highest_only")]
    [InlineData("highestOnly")]
    [InlineData("HIGHEST_ONLY")]
    public void HighestOnlyParsesFromEitherSpelling(string authored)
    {
        var store = new DataStore<ModifierKeyDefinition>();
        store.LoadMany($$"""[{ "id": "combat.barrier", "name": "Barrier", "kind": "{{authored}}", "family": "defence" }]""");

        Assert.Equal(ModifierKind.HighestOnly, store.GetById("combat.barrier").Kind);
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

            if (key.IsScoped)
                Assert.True(ScopeDimensions.IsKnown(key.ScopedBy), $"{key.Id} is scoped by unknown dimension '{key.ScopedBy}'.");

            Assert.False(key.Danger && key.Max is null, $"{key.Id} is dangerous and uncapped.");
        }
    }
}
