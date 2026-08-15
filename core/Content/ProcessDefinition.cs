using System.Text.Json.Serialization;

namespace Dungeons.Content;

/// <summary>
/// How a reagent releases what it carries (docs/emergent-item-system.md §7.3). A process
/// declares one; it names the reagent property that governs release, which is why Ember Sap
/// (solubility 55) is an alchemy reagent while Ember Core (instability 90) is a forge one.
/// A closed vocabulary, so it is an enum rather than data (DECISIONS.md D16).
/// </summary>
public enum TransferMedium
{
    /// <summary>Steeping, decoction, alchemy — release governed by <c>solubility</c>.</summary>
    Solvent,

    /// <summary>Forge, smelt, quench — release governed by <c>instability</c>.</summary>
    Thermal,

    /// <summary>Grinding, milling, pressing — release governed by <i>inverse</i> <c>hardness</c>.</summary>
    Mechanical,

    /// <summary>Attunement, enchanting — release governed by <c>resonance</c>. Used from P3.</summary>
    Arcane,
}

/// <summary>One property the process opens, and how fast it moves along that channel (§7.2).</summary>
public sealed class ChannelEntry
{
    public string Property { get; init; } = string.Empty;

    /// <summary>Base convergence rate (0–1) before acceptance/release coefficients apply (§8.2).</summary>
    public double Rate { get; init; }
}

/// <summary>
/// How much each slot contributes to the resulting potency (§6.1). Weights sum to 1.0 so
/// potency stays a weighted mean and can never be gamed by dumping quantity into a slot.
/// </summary>
public sealed class RoleWeights
{
    public double Substrate { get; init; }
    public double Reagent { get; init; }
    public double Catalyst { get; init; }

    public double Total => Substrate + Reagent + Catalyst;
}

/// <summary>What a substrate must be before the process will run on it (§7.2, §8.7 step 1).</summary>
public sealed class ProcessRequirements
{
    /// <summary>Tags the substrate must all carry (e.g. <c>form:metal</c>). Empty accepts anything.</summary>
    [JsonPropertyName("substrate_tags")]
    public IReadOnlyList<string> SubstrateTags { get; init; } = Array.Empty<string>();

    /// <summary>Minimum level in the process's profession. Ignored when the process is ungated.</summary>
    [JsonPropertyName("profession_level")]
    public int ProfessionLevel { get; init; }
}

/// <summary>
/// The tag edits a process asserts on its result — the highest-priority source in the
/// tag-derivation pass (§4.2). A <c>clear</c> entry may name a whole family with
/// <c>family:*</c> (e.g. <c>form:*</c>), which is how Grind makes something a powder and
/// not simultaneously still an ingot.
/// </summary>
public sealed class ProcessTagEffects
{
    public IReadOnlyList<string> Set { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Clear { get; init; } = Array.Empty<string>();

    /// <summary>The wildcard value that clears every tag in a family.</summary>
    public const string ClearFamilyWildcard = "*";
}

/// <summary>
/// A crafting process — the authored data that decides <i>which</i> properties react and how
/// violently (docs/emergent-item-system.md §7). Processes are the only content the emergent
/// system needs: the algebra is universal, so the same two materials put through Forge
/// Infusion, Distillation or Grinding produce completely different materials because
/// different channels are open. Choosing the process is a first-class player decision.
///
/// <para>There are no recipes. A process never references an item id.</para>
/// </summary>
public sealed class ProcessDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    /// <summary>Gating profession id, or empty for an ungated process (e.g. Grind).</summary>
    public string Profession { get; init; } = string.Empty;

    /// <summary>
    /// How violent the transformation is (0–1). Multiplies the integrity charged for a given
    /// amount of change (§6.2a) and the spread of variance (§12.3) — so a gentle, well-chosen
    /// step costs little and a brute-force one costs a lot.
    /// </summary>
    public double Severity { get; init; }

    public TransferMedium Medium { get; init; }

    [JsonPropertyName("role_weights")]
    public RoleWeights RoleWeights { get; init; } = new();

    /// <summary>The properties this process opens. Everything else is off-channel (§8.3).</summary>
    public IReadOnlyList<ChannelEntry> Channel { get; init; } = Array.Empty<ChannelEntry>();

    /// <summary>Rate at which essence transfers along this process (§8.4). Consumed from P3.</summary>
    [JsonPropertyName("essence_rate")]
    public double EssenceRate { get; init; }

    public ProcessRequirements Requires { get; init; } = new();

    [JsonPropertyName("tag_effects")]
    public ProcessTagEffects TagEffects { get; init; } = new();

    /// <summary>True when no profession gates this process.</summary>
    public bool IsUngated => string.IsNullOrEmpty(Profession);

    /// <summary>The base rate for <paramref name="property"/>, or 0 if it is off-channel.</summary>
    public double ChannelRate(string property)
    {
        foreach (var entry in Channel)
        {
            if (string.Equals(entry.Property, property, StringComparison.OrdinalIgnoreCase))
                return entry.Rate;
        }

        return 0.0;
    }

    /// <summary>True if <paramref name="property"/> is opened by this process.</summary>
    public bool IsOnChannel(string property) => ChannelRate(property) > 0.0;
}
