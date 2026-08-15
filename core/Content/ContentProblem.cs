namespace Dungeons.Content;

/// <summary>
/// A single cross-reference / well-formedness problem found in the loaded content.
/// <see cref="Category"/> groups problems by the store they were found in (e.g.
/// "actors", "crafting"); <see cref="Message"/> names the offending definition and
/// the broken reference. Collected by <see cref="ContentValidator"/> so all problems
/// can be reported at once rather than throwing on the first bad id.
/// </summary>
public sealed record ContentProblem(string Category, string Message)
{
    public override string ToString() => $"[{Category}] {Message}";
}
