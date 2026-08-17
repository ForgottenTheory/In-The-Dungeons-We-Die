using System.Text;

namespace Dungeons.Combat;

/// <summary>One pipeline stage that did something, and why.</summary>
/// <param name="Stage">The stage name, e.g. <c>Armour</c>.</param>
/// <param name="Detail">Why it did what it did — the part that answers the question.</param>
/// <param name="Before">Running total entering the stage, when the stage moved a number.</param>
/// <param name="After">Running total leaving it.</param>
public sealed record HitLogLine(string Stage, string Detail, double? Before = null, double? After = null)
{
    public override string ToString()
    {
        var change = Before is null || After is null
            ? string.Empty
            : $"  {Before:0.##} → {After:0.##}";

        return $"{Stage,-14}{Detail}{change}";
    }
}

/// <summary>
/// The combat analogue of the Reaction Log, and required scope for the same reason
/// (docs/effect-foundation.md §2.3): a pipeline with eight multiplicative sources is unplayable
/// if it cannot say why a hit landed for 43.
///
/// <para>This is what the Combat Lab renders and what golden tests assert. A test that pins the
/// whole trace catches a reordering that a test pinning only the final number would not.</para>
/// </summary>
public sealed class HitLog
{
    private readonly List<HitLogLine> _lines = new();

    public IReadOnlyList<HitLogLine> Lines => _lines;

    public void Add(string stage, string detail) => _lines.Add(new HitLogLine(stage, detail));

    public void Add(string stage, string detail, double before, double after) =>
        _lines.Add(new HitLogLine(stage, detail, before, after));

    /// <summary>Records a stage only when it actually changed the number — a trace full of
    /// no-ops is a trace nobody reads.</summary>
    public void AddIfChanged(string stage, string detail, double before, double after)
    {
        if (Math.Abs(before - after) > CombatTuning.MultiplierEpsilon)
            Add(stage, detail, before, after);
    }

    public string Render(string header)
    {
        var sb = new StringBuilder();
        sb.AppendLine(header);
        foreach (var line in _lines)
            sb.AppendLine("  " + line);
        return sb.ToString().TrimEnd();
    }

    /// <summary>Stage names in order — the cheap assertion for "did the ordering change?".</summary>
    public IReadOnlyList<string> Stages => _lines.Select(l => l.Stage).ToList();
}

/// <summary>Stage names. Constants because tests assert on them and typos would pass silently.</summary>
public static class HitStages
{
    public const string Packets = "Packets";
    public const string Dodge = "Dodge";
    public const string PerfectBlock = "PerfectBlock";
    public const string Evade = "Evade";
    public const string Parry = "Parry";
    public const string Negate = "Negate";
    public const string Crit = "Crit";
    public const string Scaling = "Scaling";
    public const string Increased = "Increased";
    public const string Armour = "Armour";
    public const string Resistance = "Resistance";
    public const string Vulnerability = "Vulnerability";
    public const string DamageTaken = "DamageTaken";
    public const string Block = "Block";
    public const string Floor = "Floor";
    public const string Applied = "Applied";
}
