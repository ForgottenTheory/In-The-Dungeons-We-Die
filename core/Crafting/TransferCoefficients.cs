using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Crafting;

/// <summary>
/// The four coefficients of one reagent step, plus the execution-quality multiplier
/// (docs/emergent-item-system.md §8.1). Together they decide how far the substrate moves
/// toward the reagent. Kept as a value so the Reaction Log (§15.3) can explain a craft in
/// the player's terms — "iron resists bonding: affinity 30" — rather than showing a number
/// with no provenance.
/// </summary>
public sealed record TransferCoefficients(
    double Compatibility,
    double TransferStrength,
    double WorkabilityFactor,
    double Catalyst,
    double QualityMultiplier)
{
    /// <summary>The product applied to every channel rate (§8.2).</summary>
    public double Product => Compatibility * TransferStrength * WorkabilityFactor * Catalyst * QualityMultiplier;

    /// <summary>
    /// Computes the coefficients for applying <paramref name="reagent"/> to
    /// <paramref name="substrate"/> under <paramref name="crafting action"/>.
    /// </summary>
    /// <param name="substrateWorkability">The substrate's remaining transformation budget (§6.2).</param>
    /// <param name="qualityMultiplier">Execution quality, 0.85–1.12 (§7.4).</param>
    /// <param name="catalyst">Catalyst factor; 1.0 when no catalyst is slotted.</param>
    public static TransferCoefficients For(
        PropertySet substrate,
        PropertySet reagent,
        CraftingActionDefinition craftingAction,
        int substrateWorkability,
        double qualityMultiplier = 1.0,
        double catalyst = MaterialTransformationTuning.NoCatalyst)
    {
        ArgumentNullException.ThrowIfNull(substrate);
        ArgumentNullException.ThrowIfNull(reagent);
        ArgumentNullException.ThrowIfNull(craftingAction);

        return new TransferCoefficients(
            Compatibility: ComputeAcceptance(substrate),
            TransferStrength: ComputeRelease(craftingAction.Medium, reagent),
            WorkabilityFactor: ComputeWorkabilityFactor(substrateWorkability),
            Catalyst: catalyst,
            QualityMultiplier: qualityMultiplier);
    }

    /// <summary>
    /// How willingly the substrate takes anything on (§8.1). <c>affinity</c> is the single
    /// most important gate in the engine: iron's affinity of 30 is the whole reason a solvent
    /// barely touches it.
    /// </summary>
    public static double ComputeAcceptance(PropertySet substrate) =>
        MaterialTransformationTuning.CompatibilityBase
        + MaterialTransformationTuning.CompatibilityScale * (substrate.Get(ItemProperties.Affinity) / 100.0);

    /// <summary>
    /// How readily the reagent gives up what it carries, governed by the property the
    /// crafting action's medium names (§7.3). This is why Ember Sap (solubility 55) is an alchemy
    /// reagent while Ember Core (instability 90) is a forge one.
    /// </summary>
    public static double ComputeRelease(TransferMedium medium, PropertySet reagent) =>
        MaterialTransformationTuning.TransferStrengthBase
        + MaterialTransformationTuning.TransferStrengthScale * (MediumProperty(medium, reagent) / 100.0);

    /// <summary>
    /// The value of the property governing release under <paramref name="medium"/> (§7.3).
    /// Mechanical is inverted — soft things grind readily, hard things resist the mill.
    /// </summary>
    public static double MediumProperty(TransferMedium medium, PropertySet reagent)
    {
        ArgumentNullException.ThrowIfNull(reagent);

        return medium switch
        {
            TransferMedium.Solvent => reagent.Get(ItemProperties.Solubility),
            TransferMedium.Thermal => reagent.Get(ItemProperties.Instability),
            TransferMedium.Mechanical => MaterialTransformationTuning.MaxPropertyValue - reagent.Get(ItemProperties.Hardness),
            TransferMedium.Arcane => reagent.Get(ItemProperties.Resonance),
            _ => 0.0,
        };
    }

    /// <summary>
    /// A worn-out substrate takes change less readily (§8.1) — one of the two ways workability
    /// makes itself felt, the other being the widening variance of §6.2b.
    /// </summary>
    public static double ComputeWorkabilityFactor(int substrateWorkability) =>
        MaterialTransformationTuning.WorkabilityFactorBase
        + MaterialTransformationTuning.WorkabilityFactorScale * (Math.Clamp(substrateWorkability, 0, 100) / 100.0);
}
