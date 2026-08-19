namespace ContentStudio.Models;

/// <summary>How records of a type are laid out across files in its data directory.</summary>
public enum FileOrganization
{
    /// <summary>Every file holds a JSON array of records (materials, moves, loot tables…).</summary>
    ArrayFiles,

    /// <summary>Every file holds exactly one record (professions, species, equipment…).</summary>
    FilePerRecord,

    /// <summary>The directory mixes both styles (actors, profession_actions, realms).</summary>
    Mixed,
}

/// <summary>
/// One authored content type: the bridge between a <c>game/data</c> folder, the Core definition
/// class that parses it, and how Content Studio presents it. Mirrors the registration list in
/// the game's <c>ContentLoader.LoadAll</c> — if the game adds a folder, add one entry here.
/// </summary>
public sealed record ContentTypeDescriptor
{
    /// <summary>Stable key used in URLs and the API; equals the data folder name.</summary>
    public required string TypeId { get; init; }

    /// <summary>Folder under <c>game/data/</c>.</summary>
    public required string Folder { get; init; }

    /// <summary>The Core definition class the game deserializes this folder into.</summary>
    public required Type DefinitionType { get; init; }

    public required string DisplayName { get; init; }
    public required string SingularName { get; init; }

    /// <summary>Sidebar group ("Combat", "Crafting", …).</summary>
    public required string NavigationGroup { get; init; }

    /// <summary>Id prefix convention including the dot (e.g. <c>material.</c>); empty when ids are bare words.</summary>
    public string IdPrefix { get; init; } = "";

    /// <summary>Field names shown as columns in the list view, beyond id/name/status.</summary>
    public IReadOnlyList<string> ListColumns { get; init; } = Array.Empty<string>();

    /// <summary>Validator category strings that attach whole-type problems to this type.</summary>
    public IReadOnlyList<string> ValidatorCategories { get; init; } = Array.Empty<string>();

    /// <summary>Short description shown on the dashboard and type header.</summary>
    public string Description { get; init; } = "";
}
