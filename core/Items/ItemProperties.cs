namespace Dungeons.Items;

/// <summary>
/// Well-known material/item property names (docs/itemization.md §2). These are
/// convenience constants only — a <see cref="PropertySet"/> is string-keyed, so new
/// properties can be introduced in data/rules without touching this file.
/// </summary>
public static class ItemProperties
{
    // Physical
    public const string Hardness = "hardness";
    public const string Mass = "mass";
    public const string Flexibility = "flexibility";
    public const string Affinity = "affinity";
    public const string Conductivity = "conductivity";
    public const string Insulation = "insulation";

    // Processing
    public const string HarvestResistance = "harvest_resistance";
    public const string Solubility = "solubility";
    public const string Instability = "instability";

    // Reactive — influences a material introduces into another
    public const string Heat = "heat";
    public const string Cold = "cold";
    public const string Charge = "charge";
    public const string Toxicity = "toxicity";
    public const string Growth = "growth";
    public const string Decay = "decay";
    public const string Corrosion = "corrosion";
    public const string Arcane = "arcane";

    // Response — how strongly a material resists an introduced influence
    public const string HeatResistance = "heat_resistance";
    public const string ColdResistance = "cold_resistance";

    /// <summary>Legacy/derived resistance carried by the Barkbound Iron demo. Kept as a
    /// known property so shipped content validates; revisit its naming when the reaction
    /// simulation defines the final resistance vocabulary (docs/crafting.md §17).</summary>
    public const string ToxinResistance = "toxin_resistance";

    public static readonly IReadOnlyList<string> Physical = new[]
    {
        Hardness, Mass, Flexibility, Affinity, Conductivity, Insulation,
    };

    public static readonly IReadOnlyList<string> Processing = new[]
    {
        HarvestResistance, Solubility, Instability,
    };

    public static readonly IReadOnlyList<string> Reactive = new[]
    {
        Heat, Cold, Charge, Toxicity, Growth, Decay, Corrosion, Arcane,
    };

    public static readonly IReadOnlyList<string> Response = new[]
    {
        HeatResistance, ColdResistance, ToxinResistance,
    };

    public static readonly IReadOnlyList<string> All =
        Physical.Concat(Processing).Concat(Reactive).Concat(Response).ToList();
}
