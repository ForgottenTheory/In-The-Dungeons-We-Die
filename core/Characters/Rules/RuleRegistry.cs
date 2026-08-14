namespace Dungeons.Characters.Rules;

/// <summary>
/// Maps rule ids to their code implementations. Rules are supplied by the caller
/// (constructor injection, no global state). Resolving an unknown id fails loudly
/// so a mistyped rule reference cannot slip through (docs/json-schema.md §21).
/// </summary>
public sealed class RuleRegistry
{
    private readonly Dictionary<string, ICharacterRule> _rules = new(StringComparer.Ordinal);

    public RuleRegistry(IEnumerable<ICharacterRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.RuleId))
                throw new ArgumentException("A rule has a null or empty RuleId.", nameof(rules));
            if (_rules.ContainsKey(rule.RuleId))
                throw new ArgumentException($"Duplicate rule id '{rule.RuleId}'.", nameof(rules));
            _rules.Add(rule.RuleId, rule);
        }
    }

    public bool Contains(string ruleId) => _rules.ContainsKey(ruleId);

    public bool TryResolve(string ruleId, out ICharacterRule rule) => _rules.TryGetValue(ruleId, out rule!);

    /// <exception cref="KeyNotFoundException">If no rule is registered for <paramref name="ruleId"/>.</exception>
    public ICharacterRule Resolve(string ruleId)
    {
        if (_rules.TryGetValue(ruleId, out var rule))
            return rule;
        throw new KeyNotFoundException($"No rule handler registered for rule id '{ruleId}'.");
    }
}
