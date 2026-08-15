using System.Globalization;
using System.Text;

namespace Dungeons.Crafting;

/// <summary>
/// Pure display formatting for crafts — the text the pre-commit UI shows
/// (docs/emergent-item-system.md §6.2c).
///
/// <para>It lives in Core rather than in the Godot client for the same reason
/// <c>ItemFormat</c> does: this wording is the thing that makes destruction-at-zero <i>fair</i>
/// rather than cruel, so it should be unit-tested, not eyeballed. The client only decides
/// colour and layout.</para>
/// </summary>
public static class CraftFormat
{
    /// <summary>Why a craft cannot proceed, in the player's language.</summary>
    public static string Failure(CraftFailure failure) => failure switch
    {
        CraftFailure.None => string.Empty,
        CraftFailure.ProfessionTooLow => "Your skill is not equal to this process yet.",
        CraftFailure.SubstrateRejected => "This process cannot work that material.",
        CraftFailure.MissingInputs => "You do not have the materials.",
        CraftFailure.NoReagents => "Choose at least one reagent.",
        CraftFailure.InvalidQuantity => "Choose how many to craft.",
        CraftFailure.UnknownProcess or CraftFailure.UnknownSubstrate
            or CraftFailure.UnknownReagent or CraftFailure.UnknownCatalyst => "Unknown material or process.",
        _ => "Nothing happens.",
    };

    /// <summary>
    /// The pre-commit summary. §6.2c requires three things of it, all present here: the
    /// projected integrity cost and result <i>before</i> committing, an explicit warning when
    /// the projection reaches zero, and a destruction <i>chance</i> rather than a false
    /// certainty when the outcome is inside the risk band.
    /// </summary>
    public static string Projection(CraftProjection projection, string substrateName)
    {
        ArgumentNullException.ThrowIfNull(projection);

        if (!projection.CanCraft)
            return Failure(projection.Failure);

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

    /// <summary>A one-line description of a process for a picker.</summary>
    public static string Process(Dungeons.Content.ProcessDefinition process, string professionName)
    {
        ArgumentNullException.ThrowIfNull(process);

        var gate = process.IsUngated
            ? "any skill"
            : $"{professionName} L{process.Requires.ProfessionLevel}";

        return $"{process.Name}  —  {process.Medium.ToString().ToLowerInvariant()}, "
            + $"severity {Number(process.Severity)}, {gate}";
    }

    /// <summary>The channel a process opens, so the player can see what will actually react.</summary>
    public static string Channel(Dungeons.Content.ProcessDefinition process)
    {
        ArgumentNullException.ThrowIfNull(process);

        return process.Channel.Count == 0
            ? "(opens nothing)"
            : "Opens: " + string.Join(", ", process.Channel.Select(c => $"{c.Property} {Number(c.Rate)}"));
    }

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Percent(double fraction) =>
        Math.Round(fraction * 100, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "%";
}
