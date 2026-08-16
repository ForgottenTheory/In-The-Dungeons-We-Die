using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Crafting;

/// <summary>One trait pass, fully described — what was born, merged, dropped, and eaten.</summary>
public sealed record TraitResolution(
    IReadOnlyList<TraitInstance> Traits,
    PropertySet Properties,
    IReadOnlyList<TraitInstance> Born,
    IReadOnlyList<(TraitInstance A, TraitInstance B, TraitInstance Into)> Superseded,
    IReadOnlyList<TraitInstance> Displaced)
{
    /// <summary>The §6.2a cost driver: merges are free (the components already paid).</summary>
    public int TraitsCreated => Born.Count;
}

/// <summary>
/// The §10 trait pass, run once per craft after the reaction state settles:
/// <b>birth → supersede → cap</b>, in that order, deterministically (trait-id order breaks
/// every tie, so the same state always yields the same traits — a signature requirement).
///
/// <para>Birth consumes properties immediately and sequentially; a later trait's condition is
/// evaluated against the already-eaten state, so two traits contending for the same property
/// resolve by id order rather than both feasting on the same points. Supersession merges run
/// until stable (a merged trait may itself merge). The cap keeps the strongest
/// <see cref="MaterialCap"/> by magnitude; displaced traits' costs are <b>not</b> refunded.</para>
/// </summary>
public sealed class TraitResolver
{
    /// <summary>§10.4 — materials cap at 3 named traits (equipment's 4 arrives with P5).</summary>
    public const int MaterialCap = 3;

    private readonly DataStore<TraitDefinition> _traits;

    public TraitResolver(DataStore<TraitDefinition> traits)
    {
        _traits = traits ?? throw new ArgumentNullException(nameof(traits));
    }

    public TraitResolution Apply(PropertySet state, IReadOnlyList<TraitInstance> existing)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(existing);

        var held = existing.ToList();
        var born = new List<TraitInstance>();

        // --- Birth (state traits only, id order) ------------------------------------------
        foreach (var definition in _traits.GetAll().OrderBy(t => t.Id, StringComparer.Ordinal))
        {
            if (!definition.IsStateBorn || held.Any(t => t.Id == definition.Id))
                continue;

            if (!definition.Condition.All(pair => pair.Value.Contains(state.Get(pair.Key))))
                continue;

            var magnitude = definition.MagnitudeOf.Count == 0
                ? 0.0
                : definition.MagnitudeOf.Min(p => state.Get(p));
            var trait = new TraitInstance(definition.Id, Math.Clamp(magnitude, 0, 100));

            foreach (var (property, amount) in definition.Consumes)
                state = state.With(property, Math.Max(0, state.Get(property) - amount));

            held.Add(trait);
            born.Add(trait);
        }

        // --- Supersession (until stable; a merged trait may merge again) -------------------
        var superseded = new List<(TraitInstance, TraitInstance, TraitInstance)>();
        bool merged;
        do
        {
            merged = false;
            foreach (var trait in held.OrderBy(t => t.Id, StringComparer.Ordinal))
            {
                var merge = FindMerge(trait, held);
                if (merge is not { } found)
                    continue;

                var (partner, intoId) = found;
                // The merged trait inherits the stronger magnitude — supersession is the
                // reason to go deep, never a downgrade.
                var into = new TraitInstance(intoId, Math.Max(trait.Magnitude, partner.Magnitude));
                held.Remove(trait);
                held.Remove(partner);
                held.Add(into);
                superseded.Add((trait, partner, into));
                merged = true;
                break;
            }
        } while (merged);

        // --- Cap: keep the strongest, report the rest (§10.4) ------------------------------
        var displaced = new List<TraitInstance>();
        if (held.Count > MaterialCap)
        {
            var kept = held
                .OrderByDescending(t => t.Magnitude)
                .ThenBy(t => t.Id, StringComparer.Ordinal)
                .Take(MaterialCap)
                .ToList();
            displaced.AddRange(held.Except(kept));
            held = kept;
        }

        return new TraitResolution(
            held.OrderBy(t => t.Id, StringComparer.Ordinal).ToList(),
            state, born, superseded, displaced);
    }

    private (TraitInstance Partner, string Into)? FindMerge(TraitInstance trait, List<TraitInstance> held)
    {
        if (!_traits.TryGetById(trait.Id, out var definition))
            return null;

        // Authored on either partner; check this trait's merges, then everyone pointing at it.
        foreach (var merge in definition.Merges)
        {
            var partner = held.FirstOrDefault(t => t.Id == merge.With);
            if (partner is not null)
                return (partner, merge.Into);
        }

        foreach (var other in held.Where(t => t.Id != trait.Id).OrderBy(t => t.Id, StringComparer.Ordinal))
        {
            if (_traits.TryGetById(other.Id, out var otherDef)
                && otherDef.Merges.FirstOrDefault(m => m.With == trait.Id) is { } reverse)
                return (other, reverse.Into);
        }

        return null;
    }

    public string TraitName(string id) => _traits.TryGetById(id, out var t) ? t.Name : id;
}
