using System.Text.Json;
using System.Text.Json.Serialization;
using Dungeons.Actions;
using Dungeons.Content;
using Dungeons.Rules;

namespace Dungeons.Combat;

/// <summary>
/// Dispatch and UI filtering ONLY — behaviour never switches on this. That is what tags are
/// for, and it is the same bargain <c>ItemType</c> struck: "if <c>Ore</c> is a type you will
/// write <c>if (type == Ore)</c> and the system calcifies" (docs/moves.md §2.1).
/// </summary>
public enum MoveKind
{
    Attack,
    Spell,
    Defensive,
    Utility,
    Reaction,
    Channel,
    Summon,
    ProfessionAction,
}

/// <summary>Who a move acts on. Declared now; range deferred entirely (U-2) — unused authored
/// numbers rot, and `delivery:melee`/`delivery:ranged` carry enough meaning today.</summary>
public enum Targeting
{
    Self,
    Enemy,
    AllEnemies,
    Ally,
    Ground,
}

/// <summary>
/// A combat move (docs/moves.md §2, D-18) — the thing that closed the GDD's largest gap.
///
/// <para>The insight: a Move's payload is <b>exactly what a Prefix or Suffix hook already
/// emits</b>. So a Move is not 25 fields of bespoke combat data — it is timing plus costs plus
/// packets plus a list of <see cref="EffectSpec"/>s from the same effect vocabulary everything
/// else uses. Attack vs Spell is a difference of data, not of engine: Heavy Strike, Fireball and
/// Shield Bash are three feels, one shape, zero new code per move.</para>
/// </summary>
public sealed class MoveDefinition : IDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    public MoveKind Kind { get; init; } = MoveKind.Attack;

    /// <summary>`action:` `delivery:` `form:` `mech:` — the closed move-tag vocabulary
    /// (docs/effect-foundation.md §5). Validated; a typo fails at load.</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public ActionTiming Timing { get; init; } = new();

    public IReadOnlyList<ActionCost> Costs { get; init; } = Array.Empty<ActionCost>();

    /// <summary>Requirements, in the same condition vocabulary rules use. `equippedTag
    /// form:sword` is how "needs a sword" is authored without a code branch.</summary>
    public IReadOnlyList<ConditionSpec> Requires { get; init; } = Array.Empty<ConditionSpec>();

    public Targeting Targeting { get; init; } = Targeting.Enemy;

    /// <summary>1 by default; move modifiers add more (`addTargets`).</summary>
    [JsonPropertyName("max_targets")]
    public int MaxTargets { get; init; } = 1;

    [JsonPropertyName("cooldown_ticks")]
    public int CooldownTicks { get; init; }

    public bool Interruptible { get; init; } = true;

    /// <summary>Base damage, typed and aspected. Empty for pure-utility moves.</summary>
    public IReadOnlyList<Packet> Packets { get; init; } = Array.Empty<Packet>();

    /// <summary>Control buildup toward Stun, resolved against the target's Resolve pool (D-08).
    /// Shield Bash carries 45 and 10 damage; the stagger is the point.</summary>
    [JsonPropertyName("stagger_power")]
    public double StaggerPower { get; init; }

    /// <summary>Riders: applyStatus, grantResource, heal… — executed on landing, each gated by
    /// its own <see cref="EffectSpec.Chance"/>.</summary>
    public IReadOnlyList<EffectSpec> Effects { get; init; } = Array.Empty<EffectSpec>();

    /// <summary>Whether this move carries the tag (exact, case-insensitive).</summary>
    public bool HasTag(string tag) => Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// One move granted to a moveset by a source — a Base, a weapon form, a prefix, an affix.
///
/// <para>Authored either as a bare id (<c>"move.slash"</c>) or as an object when it replaces
/// another (<c>{ "id": "move.frenzy", "replaces": "move.slash" }</c>). Replacement is how Druid
/// forms swap a moveset and how "Of The Wrong Weapon"-style substitution works; conflicts are
/// reported, never silently resolved (docs/moves.md §3.3).</para>
/// </summary>
[JsonConverter(typeof(MoveGrantSpecConverter))]
public sealed class MoveGrantSpec
{
    public string Id { get; init; } = string.Empty;

    public string? Replaces { get; init; }
}

/// <summary>Reads a <see cref="MoveGrantSpec"/> from either a bare string or an object.</summary>
public sealed class MoveGrantSpecConverter : JsonConverter<MoveGrantSpec>
{
    public override MoveGrantSpec Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new MoveGrantSpec { Id = reader.GetString() ?? string.Empty };

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("A move grant is a move id or an { id, replaces } object.");

        string id = string.Empty;
        string? replaces = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var property = reader.GetString();
            reader.Read();
            switch (property?.ToLowerInvariant())
            {
                case "id": id = reader.GetString() ?? string.Empty; break;
                case "replaces": replaces = reader.GetString(); break;
                default: reader.Skip(); break;
            }
        }

        return new MoveGrantSpec { Id = id, Replaces = replaces };
    }

    public override void Write(Utf8JsonWriter writer, MoveGrantSpec value, JsonSerializerOptions options)
    {
        if (value.Replaces is null)
        {
            writer.WriteStringValue(value.Id);
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        writer.WriteString("replaces", value.Replaces);
        writer.WriteEndObject();
    }
}
