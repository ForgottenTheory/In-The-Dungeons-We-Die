using Dungeons.Combat;
using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Presentation;

/// <summary>One damage packet of a granted move, in gameplay language: lane + combat units.</summary>
public sealed record PacketReading(string Lane, double Amount);

/// <summary>One move an item grants, resolved with the instance's delivery applied — the
/// same numbers combat will use, which is what makes them gameplay stats, not simulation.</summary>
public sealed record MoveReading(
    string Name,
    IReadOnlyList<PacketReading> Packets,
    int ImpactTicks,
    int RecoveryTicks,
    IReadOnlyList<string> Costs);

/// <summary>
/// The §6 reveal hierarchy's view model (docs/presentation-architecture.md): identity →
/// combat stats → effect sentences → material influence. Works identically for minted,
/// authored and (future) dropped equipment — it keys off instance + definition, never origin.
/// </summary>
public sealed record ItemReading(
    string Name,
    string Slot,
    IReadOnlyList<MoveReading> Moves,
    double Armor,
    IReadOnlyDictionary<string, double> Resistances,
    IReadOnlyList<string> ExpressedIdentityNames,
    IReadOnlyList<SentenceReading> IdentityEffects,
    IReadOnlyList<string> DormantNames,
    IReadOnlyList<string> MadeOf);

public static class ItemReadings
{
    /// <summary>Reads an owned item. <paramref name="instance"/> may be null for a bare
    /// definition (authored gear in a list).</summary>
    public static ItemReading From(ItemInstance? instance, EquipmentDefinition definition, ContentBundle content)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(content);

        var moves = definition.Slot == EquipmentSlot.Weapon
            ? EquipmentResolver.ResolveWeaponMoves(definition, instance, content.Moves)
                .Select(MoveReadingOf).ToList()
            : (IReadOnlyList<MoveReading>)Array.Empty<MoveReading>();

        var armor = EquipmentResolver.ResolveArmor(definition, instance);

        var madeOf = (instance?.Provenance ?? Array.Empty<string>() as IReadOnlyList<string>)
            .Select(id => content.Materials.TryGetById(id, out var m) ? m.Name : id)
            .ToList();

        // The identity layer (Phase 6): sentences and stakes read off the instance.
        var expressedIdentityNames = (instance?.ExpressedIdentities ?? Array.Empty<Crafting.Identity.IdentityStake>())
            .Select(stake => IdentityPhrases.Stake(stake, content))
            .ToList();
        var identityEffects = SentenceReadings.From(
            instance?.IdentitySentences ?? Array.Empty<Crafting.Identity.ItemEffectSentence>(), content);
        var dormantNames = (instance?.DormantIdentities ?? Array.Empty<Crafting.Identity.IdentityStake>())
            .Select(stake => IdentityPhrases.Stake(stake, content))
            .ToList();

        return new ItemReading(
            instance?.DisplayName ?? definition.Name,
            EquipmentSlotNames.CategoryOf(definition.Slot),
            moves,
            armor.Armor,
            armor.Resistances,
            expressedIdentityNames,
            identityEffects,
            dormantNames,
            madeOf);
    }

    private static MoveReading MoveReadingOf(MoveDefinition move) => new(
        move.Name,
        move.Packets.Select(p => new PacketReading(LaneText(p), p.Amount)).ToList(),
        move.Timing.TimeToImpactTicks,
        move.Timing.RecoveryTicks,
        move.Costs.Select(c => $"{c.Amount:0.#} {Capitalize(c.Resource)}").ToList());

    private static string LaneText(Packet packet) =>
        packet.Aspect is { Length: > 0 } aspect
            ? $"{packet.Type}·{Capitalize(aspect)}"
            : packet.Type.ToString();

    private static string Capitalize(string word) =>
        word.Length > 1 ? char.ToUpperInvariant(word[0]) + word[1..] : word.ToUpperInvariant();
}
