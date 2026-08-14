namespace Dungeons.Content;

/// <summary>
/// Thrown when two definitions share the same <see cref="IDefinition.Id"/>.
/// Broken content should fail loudly at load time rather than surfacing three
/// hours into a Realm Run (see docs/json-schema.md §21).
/// </summary>
public sealed class DuplicateDefinitionException : Exception
{
    public DuplicateDefinitionException(string id, Type definitionType)
        : base($"Duplicate {definitionType.Name} id '{id}'. Definition ids must be unique.")
    {
        Id = id;
        DefinitionType = definitionType;
    }

    public string Id { get; }
    public Type DefinitionType { get; }
}
