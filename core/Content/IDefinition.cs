namespace Dungeons.Content;

/// <summary>
/// Contract implemented by every data-driven definition. The <see cref="Id"/> is
/// a stable, namespaced string (e.g. "material.oak_bark") used as the persistence
/// and lookup key. Display names may change without breaking saves.
/// </summary>
public interface IDefinition
{
    string Id { get; }
}
