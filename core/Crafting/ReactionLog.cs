using System.Text;

namespace Dungeons.Crafting;

/// <summary>What part of a craft a log entry describes, so a UI can style or filter without
/// parsing the text back apart.</summary>
public enum ReactionLogKind
{
    /// <summary>"Forge Infusion — Iron Ingot ← Ember Core".</summary>
    Step,

    /// <summary>An acceptance/release/integrity coefficient and why it is what it is.</summary>
    Coefficient,

    /// <summary>One property moving, with the reason it moved.</summary>
    Property,

    /// <summary>The integrity charged, and the arithmetic behind it.</summary>
    Integrity,

    /// <summary>The potency inputs and the resulting potency.</summary>
    Potency,

    /// <summary>The material was destroyed, and what was recovered.</summary>
    Destruction,

    /// <summary>The finished material, and whether it had ever been made before.</summary>
    Result,
}

/// <summary>One line of a Reaction Log. Carries the numbers alongside the text so the same
/// entry can be rendered as a UI row or as plain text.</summary>
public sealed record ReactionLogEntry(
    ReactionLogKind Kind,
    string Text,
    int Indent = 0,
    string? Property = null,
    double? Before = null,
    double? After = null);

/// <summary>
/// A structured, human-readable trace of one craft (docs/emergent-item-system.md §15.3).
///
/// <para>The spec is blunt that this is <b>required scope, not a nice-to-have</b>: "a system
/// this deep is only playable if it explains itself." It is simultaneously the tutorial (the
/// player learns the algebra by watching it run), the debugger (every constant's effect is
/// visible), and the raw material for the codex in P6.</para>
///
/// <para>The valuable part is not the numbers — it is the <i>because</i>. "Acceptance 0.48"
/// teaches nothing; "Acceptance 0.48 — iron resists bonding (affinity 30)" teaches the player
/// to look at affinity before choosing a process.</para>
/// </summary>
public sealed class ReactionLog
{
    public static readonly ReactionLog Empty = new(Array.Empty<ReactionLogEntry>());

    public ReactionLog(IReadOnlyList<ReactionLogEntry> entries)
    {
        Entries = entries ?? throw new ArgumentNullException(nameof(entries));
    }

    public IReadOnlyList<ReactionLogEntry> Entries { get; }

    /// <summary>The whole trace as indented text, in the shape §15.3 lays out.</summary>
    public string ToText()
    {
        var builder = new StringBuilder();

        foreach (var entry in Entries)
        {
            if (builder.Length > 0)
                builder.AppendLine();

            builder.Append(new string(' ', entry.Indent * 2)).Append(entry.Text);
        }

        return builder.ToString();
    }

    public override string ToString() => ToText();
}
