using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dungeons.Characters.Composition;

// Strongly-typed content ids for the four character-composition slots. They exist to stop the
// four positional ids in CharacterBuild being swapped by mistake (a persisted, silent bug).
// Each serializes as a bare string, so save files are unchanged (docs/architecture.md §9).

[JsonConverter(typeof(SpeciesIdConverter))]
public readonly record struct SpeciesId(string Value) { public override string ToString() => Value; }

[JsonConverter(typeof(BaseClassIdConverter))]
public readonly record struct BaseClassId(string Value) { public override string ToString() => Value; }

[JsonConverter(typeof(PrefixIdConverter))]
public readonly record struct PrefixId(string Value) { public override string ToString() => Value; }

[JsonConverter(typeof(SuffixIdConverter))]
public readonly record struct SuffixId(string Value) { public override string ToString() => Value; }

/// <summary>Reads/writes a string-backed id struct as a bare JSON string.</summary>
internal abstract class StringIdConverter<T> : JsonConverter<T>
{
    protected abstract T Create(string value);
    protected abstract string ValueOf(T id);

    public override T Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
        Create(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(ValueOf(value));
}

internal sealed class SpeciesIdConverter : StringIdConverter<SpeciesId>
{
    protected override SpeciesId Create(string value) => new(value);
    protected override string ValueOf(SpeciesId id) => id.Value;
}

internal sealed class BaseClassIdConverter : StringIdConverter<BaseClassId>
{
    protected override BaseClassId Create(string value) => new(value);
    protected override string ValueOf(BaseClassId id) => id.Value;
}

internal sealed class PrefixIdConverter : StringIdConverter<PrefixId>
{
    protected override PrefixId Create(string value) => new(value);
    protected override string ValueOf(PrefixId id) => id.Value;
}

internal sealed class SuffixIdConverter : StringIdConverter<SuffixId>
{
    protected override SuffixId Create(string value) => new(value);
    protected override string ValueOf(SuffixId id) => id.Value;
}
