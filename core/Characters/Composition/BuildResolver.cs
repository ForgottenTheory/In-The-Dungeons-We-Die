using Dungeons.Content;
using Dungeons.Modifiers;
using Dungeons.Rules;

namespace Dungeons.Characters.Composition;

/// <summary>One hook a build has, and where it came from.</summary>
/// <param name="Origin">Player-facing provenance: "The Galvanic", "Exploding Kneecaps (Strike)".</param>
public sealed record AttachedRule(TriggerRule Rule, string Source, string Origin);

/// <summary>
/// Everything a build actually <i>is</i>, assembled from Base + Prefix + Suffix.
///
/// <para>This is the Character Lab's data source and the answer to "what changed when I swapped
/// that?". Every field is derived, nothing is authored per-combination.</para>
/// </summary>
public sealed record ResolvedBuild(
    string Name,
    BaseClassDefinition Base,
    PrefixDefinition? Prefix,
    SuffixDefinition? Suffix,
    ExpressionChannel Channel,
    IReadOnlyDictionary<AttributeType, double> GrowthPerLevel,
    IReadOnlyList<GaugeDefinition> Gauges,
    IReadOnlyList<AttachedRule> Rules,
    ModifierSet Modifiers)
{
    /// <summary>Cumulative attribute growth at <paramref name="level"/>.</summary>
    public IReadOnlyDictionary<AttributeType, int> GrowthAt(int level) =>
        GrowthPerLevel.ToDictionary(p => p.Key, p => (int)Math.Floor(p.Value * Math.Max(0, level - 1)));

    /// <summary>Rules from one origin, for grouped display.</summary>
    public IReadOnlyList<AttachedRule> RulesFrom(string source) =>
        Rules.Where(r => r.Source == source).ToList();
}

/// <summary>
/// Turns a <see cref="CharacterBuild"/> into a <see cref="ResolvedBuild"/>.
///
/// <para>The whole composition model lives in this one method, and it is short on purpose.
/// A Base contributes growth, a gauge and a channel; a Prefix contributes a mechanic and maybe
/// a second gauge; a Suffix contributes <b>the one expression matching the build's channel</b>.
/// There is no per-combination logic anywhere — swapping any component re-resolves everything
/// and nothing was authored for that pairing.</para>
/// </summary>
public sealed class BuildResolver
{
    private readonly ContentBundle _content;

    public BuildResolver(ContentBundle content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public ResolvedBuild Resolve(CharacterBuild build)
    {
        ArgumentNullException.ThrowIfNull(build);

        var @base = _content.Classes.GetById(build.BaseClassId.Value);
        _content.Prefixes.TryGetById(build.PrefixId.Value, out var prefix);
        _content.Suffixes.TryGetById(build.SuffixId.Value, out var suffix);

        // The Base owns the channel. A Prefix or equipment may shift it later; that hook goes
        // here, and nothing downstream needs to change when it does.
        var channel = @base.DefaultChannel;

        var gauges = new List<GaugeDefinition>();
        if (@base.Gauge is not null)
            gauges.Add(@base.Gauge);
        if (prefix?.Gauge is not null)
            gauges.Add(prefix.Gauge);

        var rules = new List<AttachedRule>();

        foreach (var gauge in gauges)
        foreach (var feed in gauge.Feeds)
            rules.Add(new AttachedRule(feed, "gauge", $"{gauge.Name} gauge"));

        if (prefix is not null)
        {
            foreach (var rule in prefix.Rules)
                rules.Add(new AttachedRule(rule, prefix.Id, prefix.Name));
        }

        // Exactly one Suffix expression applies — the one matching this build's channel. That
        // single lookup is what makes a Suffix usable by every Base instead of only by the
        // archetype it was written for.
        if (suffix?.For(channel) is { } expression)
            rules.Add(new AttachedRule(expression.Rule, suffix.Id, $"{suffix.Name} ({channel})"));

        return new ResolvedBuild(
            Name: ClassNameFormatter.Format(
                new ClassNameParts(
                    @base.Name,
                    prefix is null ? null : ClassNameFormatter.PrefixWord(prefix.Name),
                    suffix?.Name,
                    suffix?.Format ?? "standard",
                    suffix?.CustomPhrase),
                _content.NameFormats),
            Base: @base,
            Prefix: prefix,
            Suffix: suffix,
            Channel: channel,
            GrowthPerLevel: AttributeGrowth.PerLevel(@base.Growth),
            Gauges: gauges,
            Rules: rules,
            Modifiers: CollectModifiers(@base, prefix, suffix));
    }

    /// <summary>
    /// Static modifiers from every component, each tagged with its source so the Lab can answer
    /// "why is this number what it is?" rather than just showing the total.
    /// </summary>
    private ModifierSet CollectModifiers(
        BaseClassDefinition @base, PrefixDefinition? prefix, SuffixDefinition? suffix)
    {
        var set = new ModifierSet(_content.ModifierKeys);

        foreach (var component in new CharacterComponentDefinition?[] { @base, prefix, suffix })
        {
            if (component is null)
                continue;

            foreach (var modifier in component.Modifiers)
            {
                var key = ModifierKeys.From(modifier.ToModifier().Stat);
                set.Add(key, modifier.Value, component.Name);
            }
        }

        return set;
    }

    /// <summary>
    /// What changed between two builds, in the terms a player thinks in. This is the Character
    /// Lab's reason to exist — swapping one component should be immediately legible.
    /// </summary>
    public static IReadOnlyList<string> Diff(ResolvedBuild before, ResolvedBuild after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var changes = new List<string>();

        if (before.Name != after.Name)
            changes.Add($"Name: {before.Name}  →  {after.Name}");

        if (before.Base.Id != after.Base.Id)
        {
            changes.Add($"Engine: {after.Base.Engine}");
            changes.Add($"Weakness: {after.Base.Weakness}");
        }

        if (before.Channel != after.Channel)
            changes.Add($"Channel: {before.Channel} → {after.Channel} (suffix now expresses differently)");

        foreach (var attribute in Enum.GetValues<AttributeType>())
        {
            var delta = after.GrowthPerLevel[attribute] - before.GrowthPerLevel[attribute];
            if (Math.Abs(delta) > 0.001)
                changes.Add($"{attribute} growth: {delta:+0.###;-0.###}/level");
        }

        foreach (var gauge in after.Gauges.Where(g => before.Gauges.All(b => b.Name != g.Name)))
            changes.Add($"Gained gauge: {gauge.Name} ({gauge.Behaviour})");

        foreach (var gauge in before.Gauges.Where(g => after.Gauges.All(a => a.Name != g.Name)))
            changes.Add($"Lost gauge: {gauge.Name}");

        foreach (var rule in after.Rules.Where(r => before.Rules.All(b => b.Rule.Id != r.Rule.Id)))
            changes.Add($"Gained hook: {rule.Origin} on {rule.Rule.Event}");

        foreach (var rule in before.Rules.Where(r => after.Rules.All(a => a.Rule.Id != r.Rule.Id)))
            changes.Add($"Lost hook: {rule.Origin}");

        return changes.Count == 0 ? new[] { "(no mechanical change)" } : changes;
    }
}
