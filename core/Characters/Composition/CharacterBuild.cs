namespace Dungeons.Characters.Composition;

/// <summary>
/// The four ids that define a character's identity. Resolving these against their
/// definitions (plus a baseline) produces a <see cref="CharacterBlueprint"/>.
/// This is the persisted description of "who" a character is (docs/architecture.md §17).
/// </summary>
public sealed record CharacterBuild(SpeciesId SpeciesId, BaseClassId BaseClassId, PrefixId PrefixId, SuffixId SuffixId);
