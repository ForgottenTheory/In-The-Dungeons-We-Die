namespace Dungeons.Content;

/// <summary>
/// What a destroyed material leaves behind (docs/emergent-item-system.md §6.2c). Integrity 0
/// is a terminal event, but it must never be total loss: a blown craft is a setback and a
/// consolation prize, not a zero, or players stop experimenting — which would defeat the
/// entire design goal.
///
/// <para>Which byproduct you get is decided by the destroyed material's dominant <c>form:</c>
/// tag, so slag comes from metal and cinders come from wood without a single item id being
/// named. §21 lists the table as an open tuning question, which is why it is data.</para>
/// </summary>
public sealed class ByproductDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    /// <summary>The material produced. Authored, stackable, and useful as a reagent in its
    /// own right — that usefulness is what makes destruction survivable.</summary>
    public string Material { get; init; } = string.Empty;

    /// <summary><c>form:</c> tag values this byproduct covers (bare values, not namespaced).</summary>
    public IReadOnlyList<string> Forms { get; init; } = Array.Empty<string>();

    /// <summary>Used when the destroyed material's forms match nothing. Exactly one byproduct
    /// is the fallback, so the table is total — every destruction yields something.</summary>
    public bool Fallback { get; init; }

    public bool Covers(string formValue) =>
        Forms.Contains(formValue, StringComparer.OrdinalIgnoreCase);
}
