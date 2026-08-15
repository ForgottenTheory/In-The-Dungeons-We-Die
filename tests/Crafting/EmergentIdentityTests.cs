using Dungeons.Content;
using Dungeons.Crafting;
using Dungeons.Items;
using Dungeons.Randomness;
using Xunit;

namespace Dungeons.Tests.Crafting;

/// <summary>
/// Identity, determinism and the archetype registry (docs/emergent-item-system.md §12).
///
/// <para>The claim under test is §0 Decision 3: emergent materials are <b>stackable runtime
/// definitions keyed by a hash of their state</b>, not per-unit instances. Everything else in
/// this file exists to make that safe — the signature has to be stable, the registry has to
/// be a pure cache, and the same craft has to reach the same material every time.</para>
/// </summary>
public class EmergentIdentityTests
{
    private static DataStore<MaterialDefinition> Materials() =>
        TestPaths.LoadStore<MaterialDefinition>("materials");

    private static DataStore<ProcessDefinition> Processes() =>
        TestPaths.LoadStore<ProcessDefinition>("processes");

    private static MaterialProfile Profile(
        double heat = 35,
        int potency = 49,
        int integrity = 72,
        int generation = 2,
        string root = "material.iron_ingot") =>
        new(
            Properties: PropertySet.FromValues(new Dictionary<string, double>
            {
                ["heat"] = heat, ["hardness"] = 62, ["mass"] = 58,
            }),
            Potency: potency,
            Integrity: integrity,
            Lineage: new Lineage(
                new[] { new RootShare(root, 1.0) }, generation, "process.forge_infusion",
                new[] { "material.iron_ingot" }),
            Signature: string.Empty);

    private static readonly string[] IronTags = { "form:metal", "state:alloy", "origin:mineral", "comp:inorganic" };

    // ---- §12.1 the signature ----------------------------------------------------------------

    [Fact]
    public void Signature_HasTheDocumentedShape()
    {
        var signature = MaterialSignature.Compute(Profile(), IronTags);

        Assert.StartsWith("emergent.", signature);
        Assert.Equal("emergent.".Length + 8, signature.Length);
    }

    /// <summary>
    /// §12.2, and the reason discovery is worth talking about: "Same state ⇒ same signature ⇒
    /// same id ⇒ same name, for every player, forever." A per-process hash seed would break
    /// this silently across restarts, so it is worth asserting the value itself is stable.
    /// </summary>
    [Fact]
    public void Signature_IsStableForTheSameState()
    {
        Assert.Equal(
            MaterialSignature.Compute(Profile(), IronTags),
            MaterialSignature.Compute(Profile(), Enumerable.Reverse(IronTags).ToArray()));
    }

    /// <summary>Quantization is what collapses near-identical results, so the registry fills
    /// with distinct materials rather than meaningless neighbours (§12.2).</summary>
    [Fact]
    public void Signature_CollapsesResultsWithinTheSameBucket()
    {
        Assert.Equal(
            MaterialSignature.Compute(Profile(heat: 34.2), IronTags),
            MaterialSignature.Compute(Profile(heat: 35.8), IronTags));
    }

    [Fact]
    public void Signature_SeparatesResultsInDifferentBuckets()
    {
        Assert.NotEqual(
            MaterialSignature.Compute(Profile(heat: 35), IronTags),
            MaterialSignature.Compute(Profile(heat: 55), IronTags));
    }

    /// <summary>Each component of §12.1's hash must actually participate, or two genuinely
    /// different materials would share an identity and a name.</summary>
    [Fact]
    public void Signature_RespondsToEveryComponentOfTheState()
    {
        var baseline = MaterialSignature.Compute(Profile(), IronTags);

        Assert.NotEqual(baseline, MaterialSignature.Compute(Profile(potency: 70), IronTags));
        Assert.NotEqual(baseline, MaterialSignature.Compute(Profile(generation: 3), IronTags));
        Assert.NotEqual(baseline, MaterialSignature.Compute(Profile(root: "material.copper_ore"), IronTags));
        Assert.NotEqual(baseline, MaterialSignature.Compute(Profile(), new[] { "form:powder", "state:alloy" }));
        Assert.NotEqual(baseline, MaterialSignature.Compute(Profile(), new[] { "form:metal", "state:refined" }));
    }

    /// <summary>Integrity is deliberately absent from identity: how much budget a material has
    /// left is its condition, not what it <i>is</i>. Two units of the same alloy must stack
    /// even if one has been worked harder.</summary>
    [Fact]
    public void Signature_IgnoresIntegrity()
    {
        Assert.Equal(
            MaterialSignature.Compute(Profile(integrity: 72), IronTags),
            MaterialSignature.Compute(Profile(integrity: 20), IronTags));
    }

    /// <summary>A trace the floor left behind and an absent property are the same material —
    /// otherwise §8.3's pruning would still leave identity-splitting dust.</summary>
    [Fact]
    public void Signature_TreatsSubBucketTracesAsAbsent()
    {
        var withTrace = new MaterialProfile(
            PropertySet.FromValues(new Dictionary<string, double> { ["hardness"] = 60, ["heat"] = 1 }),
            50, 80, Lineage.ForBase("material.iron_ingot"), string.Empty);
        var without = new MaterialProfile(
            PropertySet.FromValues(new Dictionary<string, double> { ["hardness"] = 60 }),
            50, 80, Lineage.ForBase("material.iron_ingot"), string.Empty);

        Assert.Equal(
            MaterialSignature.Compute(withTrace, IronTags),
            MaterialSignature.Compute(without, IronTags));
    }

    /// <summary>Adding traits (P2) and essence (P3) must not re-key archetypes that have
    /// neither — a player's existing materials keep their ids and their names.</summary>
    [Fact]
    public void Canonical_ReservesSlotsForTraitsAndEssence()
    {
        var canonical = MaterialSignature.Canonical(Profile(), IronTags);

        Assert.Contains("|traits=", canonical);
        Assert.Contains("|essence=", canonical);
    }

    // ---- §12.3 variance ----------------------------------------------------------------------

    [Fact]
    public void Variance_IsAbsentAtPerfectSkill()
    {
        var state = PropertySet.FromValues(new Dictionary<string, double> { ["heat"] = 35 });
        var scattered = VariancePerturbation.Apply(
            state, Processes().GetById("process.forge_infusion"), varianceMagnitude: 0, new SeededRandom(1));

        Assert.Same(state, scattered);
    }

    /// <summary>Same seed, same scatter — one of only two probabilistic things in the whole
    /// system, and it still has to be reproducible (§12.5).</summary>
    [Fact]
    public void Variance_IsReproducibleFromItsSeed()
    {
        var state = PropertySet.FromValues(new Dictionary<string, double> { ["heat"] = 35, ["hardness"] = 62 });
        var process = Processes().GetById("process.forge_infusion");

        var first = VariancePerturbation.Apply(state, process, 40, new SeededRandom(1234));
        var second = VariancePerturbation.Apply(state, process, 40, new SeededRandom(1234));

        Assert.Equal(first.AsDictionary(), second.AsDictionary());
    }

    [Fact]
    public void Variance_OnlyScattersChannelPropertiesTheMaterialActuallyHas()
    {
        var state = PropertySet.FromValues(new Dictionary<string, double>
        {
            ["heat"] = 35,      // on channel, present  → scattered
            ["toxicity"] = 40,  // off channel          → untouched
        });

        var scattered = VariancePerturbation.Apply(
            state, Processes().GetById("process.forge_infusion"), 60, new SeededRandom(7));

        Assert.NotEqual(35.0, scattered.Get("heat"));
        Assert.Equal(40.0, scattered.Get("toxicity"));
        Assert.False(scattered.Has("affinity"), "an absent channel property must not be conjured into existence.");
    }

    /// <summary>
    /// §12.3's actual point: a bad roll yields a <i>different material</i>, not the same one
    /// with worse numbers. Low skill should scatter across signatures; high skill should not.
    /// </summary>
    [Fact]
    public void Variance_ProducesDifferentMaterials_NotRandomStatsOnOne()
    {
        var process = Processes().GetById("process.forge_infusion");
        var state = PropertySet.FromValues(new Dictionary<string, double>
        {
            ["heat"] = 40, ["hardness"] = 60, ["affinity"] = 30,
        });

        var scattered = new HashSet<string>(StringComparer.Ordinal);
        var precise = new HashSet<string>(StringComparer.Ordinal);

        for (var seed = 0; seed < 25; seed++)
        {
            scattered.Add(SignatureOf(VariancePerturbation.Apply(state, process, 80, new SeededRandom(seed))));
            precise.Add(SignatureOf(VariancePerturbation.Apply(state, process, 0, new SeededRandom(seed))));
        }

        Assert.True(scattered.Count > 1, "an unskilled crafter should land on more than one material.");
        Assert.Single(precise);

        static string SignatureOf(PropertySet properties) => MaterialSignature.Compute(
            new MaterialProfile(properties, 50, 80, Lineage.ForBase("material.iron_ingot"), string.Empty),
            IronTags);
    }

    // ---- §12.4 the registry --------------------------------------------------------------------

    [Fact]
    public void Registry_RegistersOnFirstDiscoveryAndStacksAfterwards()
    {
        var materials = Materials();
        var registry = new EmergentRegistry(materials);
        var signature = MaterialSignature.Compute(Profile(), IronTags);

        var first = registry.GetOrRegister(signature, () => Archetype(signature));
        var second = registry.GetOrRegister(signature, () => throw new InvalidOperationException("must not recreate"));

        Assert.True(first.IsFirstDiscovery);
        Assert.False(second.IsFirstDiscovery);
        Assert.Same(first.Definition, second.Definition);
        Assert.Equal(1, registry.Count);
    }

    /// <summary>
    /// §0 Decision 3's whole promise: an emergent archetype must flow through every existing
    /// code path with no special-casing. Registering into the shared material store is what
    /// delivers that — so lookups and stacking must simply work.
    /// </summary>
    [Fact]
    public void RegisteredArchetypes_BehaveLikeAnyOtherMaterial()
    {
        var materials = Materials();
        var registry = new EmergentRegistry(materials);
        var signature = MaterialSignature.Compute(Profile(), IronTags);
        registry.GetOrRegister(signature, () => Archetype(signature));

        Assert.True(materials.Contains(signature));
        Assert.True(materials.GetById(signature).Stackable);
        Assert.Equal(ItemType.Material, materials.GetById(signature).ItemType);

        var inventory = new Inventory();
        inventory.Add(signature, 40);
        Assert.Equal(40, inventory.Snapshot().Single(s => s.ItemId == signature).Quantity);
    }

    /// <summary>An archetype's id <i>is</i> its signature; anything else would make the
    /// registry's lookups and the save's references disagree.</summary>
    [Fact]
    public void Registry_RejectsAnArchetypeWhoseIdIsNotItsSignature()
    {
        var registry = new EmergentRegistry(Materials());

        Assert.Throws<InvalidOperationException>(() =>
            registry.GetOrRegister("emergent.abc12345", () => Archetype("emergent.deadbeef")));
    }

    [Fact]
    public void Registry_RestoreIsIdempotent()
    {
        var materials = Materials();
        var registry = new EmergentRegistry(materials);
        var signature = MaterialSignature.Compute(Profile(), IronTags);
        var archetype = Archetype(signature);

        registry.Restore(new[] { archetype });
        registry.Restore(new[] { archetype });

        Assert.Equal(1, registry.Count);
    }

    private static MaterialDefinition Archetype(string signature) => new()
    {
        Id = signature,
        Name = "Emberveined Iron",
        Tags = IronTags,
        Properties = new Dictionary<string, double>(Profile().Properties.AsDictionary()),
        Profile = Profile() with { Signature = signature },
    };
}
