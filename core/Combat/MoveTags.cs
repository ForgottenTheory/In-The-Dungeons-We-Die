namespace Dungeons.Combat;

/// <summary>
/// The closed move-tag vocabulary (docs/effect-foundation.md §5, D-16) — the namespaces a move
/// may carry and every value each accepts. Closed and validated like every other vocabulary
/// here: tags are the interoperability layer, and an open set is a typo surface.
///
/// <para>Moves author <b>namespaced tags only</b>. The bare aliases shipped conditions match
/// (`heavy`, `melee`, `attack`) are derived at event time by combat, never authored — that is
/// what keeps 23 pre-vocabulary hooks alive without letting two spellings of the same fact
/// drift apart.</para>
/// </summary>
public static class MoveTags
{
    public static readonly IReadOnlySet<string> Actions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "attack", "spell", "defensive", "utility", "movement", "channel", "reaction", "summon", "profession",
    };

    public static readonly IReadOnlySet<string> Deliveries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "melee", "ranged", "projectile", "area", "direct", "dot",
    };

    public static readonly IReadOnlySet<string> Forms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "sword", "axe", "hammer", "dagger", "spear", "bow", "staff", "shield", "focus",
        "light_armour", "heavy_armour", "robe", "rod", "pick", "hammer_tool", "apparatus", "blade_tool",
    };

    public static readonly IReadOnlySet<string> Mechs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "critical", "block", "perfect_block", "parry", "dodge", "evade", "negate", "retaliation",
        "thorns", "healing", "barrier", "resource", "control", "ailment", "impairment", "stagger",
        "chain", "trigger", "overreach",
    };

    public static readonly IReadOnlySet<string> Essences = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "fire", "frost", "storm", "nature", "necrotic", "radiant", "abyssal",
    };

    /// <summary>
    /// Whether a tag is legal on a move, and why not when it isn't. Bare tags are rejected —
    /// the runtime derives aliases, content never authors them.
    /// </summary>
    public static bool IsValidOnMove(string tag, out string problem)
    {
        var colon = tag.IndexOf(':');
        if (colon <= 0 || colon == tag.Length - 1)
        {
            problem = $"'{tag}' is not namespaced. Moves author namespaced tags (action:/delivery:/form:/mech:/essence:); bare aliases are derived at runtime.";
            return false;
        }

        var family = tag[..colon];
        var value = tag[(colon + 1)..];

        var known = family.ToLowerInvariant() switch
        {
            "action" => Actions,
            "delivery" => Deliveries,
            "form" => Forms,
            "mech" => Mechs,
            "essence" => Essences,
            _ => null,
        };

        if (known is null)
        {
            problem = $"'{tag}' uses unknown namespace '{family}:'. Moves may carry action:/delivery:/form:/mech:/essence:.";
            return false;
        }

        if (!known.Contains(value))
        {
            problem = $"'{tag}' is not in the {family}: vocabulary.";
            return false;
        }

        problem = string.Empty;
        return true;
    }
}
