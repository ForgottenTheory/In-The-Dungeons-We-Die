namespace Dungeons.Content;

/// <summary>
/// Minimal data-driven material definition used in Milestone 1 to exercise the
/// full content pipeline (JSON on disk → Godot file access → <see cref="DataStore{T}"/>).
/// Crafting-relevant fields such as properties are added in later milestones
/// (see docs/json-schema.md §7).
/// </summary>
public sealed class MaterialDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}
