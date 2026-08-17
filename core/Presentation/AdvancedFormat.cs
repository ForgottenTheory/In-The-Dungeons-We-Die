using System.Globalization;
using System.Text;
using Dungeons.Content;
using Dungeons.Crafting;

namespace Dungeons.Presentation;

/// <summary>
/// The numeric voice (docs/presentation-architecture.md §2F): exact values, costs and rates,
/// one toggle away from the semantic surfaces and never the default. This is the former
/// player-facing wording of <c>CraftFormat</c>, preserved verbatim — the theorycrafter's and
/// the debugger's view, and the §6.2c arithmetic in the open.
/// </summary>
public static class AdvancedFormat
{
    /// <summary>The numeric pre-commit summary (formerly <c>CraftFormat.Projection</c>).</summary>
    public static string Projection(CraftProjection projection, string substrateName)
    {
        ArgumentNullException.ThrowIfNull(projection);

        if (!projection.CanCraft)
            return CraftFormat.Failure(projection.Failure);

        var integrity = projection.Integrity;
        var builder = new StringBuilder();

        builder.Append("Expect: ").Append(projection.ProjectedName);
        if (projection.WouldBeFirstDiscovery)
            builder.Append("  (never made before)");
        builder.AppendLine();

        builder.Append("Potency ").Append(projection.ProjectedPotency)
            .Append("   Integrity → ").Append(integrity.ProjectedIntegrity)
            .Append("  (cost ").Append(Number(integrity.ExpectedCost));

        if (integrity.CostSpread > 0.5)
            builder.Append(" ± ").Append(Number(integrity.CostSpread));

        builder.Append(')');

        if (projection.WarnsOfDestruction)
        {
            builder.AppendLine();
            builder.Append("⚠ This will DESTROY the ").Append(substrateName)
                .Append(". You will recover only byproducts.");
        }
        else if (projection.WarnsOfRisk)
        {
            builder.AppendLine();
            builder.Append("⚠ ").Append(Percent(integrity.DestructionChance))
                .Append(" chance of destroying the ").Append(substrateName).Append('.');
        }

        return builder.ToString();
    }

    /// <summary>The numeric process line (formerly <c>CraftFormat.Process</c>).</summary>
    public static string Process(ProcessDefinition process, string professionName)
    {
        ArgumentNullException.ThrowIfNull(process);

        var gate = process.IsUngated
            ? "any skill"
            : $"{professionName} L{process.Requires.ProfessionLevel}";

        return $"{process.Name}  —  {process.Medium.ToString().ToLowerInvariant()}, "
            + $"severity {Number(process.Severity)}, {gate}";
    }

    /// <summary>The numeric channel line (formerly <c>CraftFormat.Channel</c>).</summary>
    public static string Channel(ProcessDefinition process)
    {
        ArgumentNullException.ThrowIfNull(process);

        return process.Channel.Count == 0
            ? "(opens nothing)"
            : "Opens: " + string.Join(", ", process.Channel.Select(c => $"{c.Property} {Number(c.Rate)}"));
    }

    /// <summary>The numeric material summary (formerly <c>GameRoot.MaterialSummary</c>): meta
    /// fields, the top properties by value, and raw tags.</summary>
    public static string Material(MaterialDefinition material, MaterialProfile profile)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(profile);

        var properties = profile.Properties.AsDictionary()
            .OrderByDescending(p => p.Value)
            .ThenBy(p => p.Key, StringComparer.Ordinal)
            .Take(5)
            .Select(p => $"{p.Key} {p.Value.ToString("0", CultureInfo.InvariantCulture)}");

        return $"{material.Name} — potency {profile.Potency}, integrity {profile.Integrity}, "
            + $"gen {profile.Generation}\n  {string.Join(", ", properties)}\n  {string.Join(" ", material.Tags)}";
    }

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Percent(double fraction) =>
        Math.Round(fraction * 100, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "%";
}
