using Dungeons.Items;
using Dungeons.Presentation;
using Xunit;

namespace Dungeons.Tests.Presentation;

/// <summary>
/// Slot names on the two player-facing surfaces. <see cref="EquipmentSlot"/> members are
/// persistent identifiers chosen to be unambiguous in a save file, not to read as English — so
/// D30's one-way rule covers them the same way it covers properties.
///
/// <para>The guard that matters is <see cref="NoSlotEverReachesThePlayerWithADigitInItsName"/>:
/// it fails for any slot added later that nobody wrote a name for.</para>
/// </summary>
public class EquipmentSlotNameTests
{
    /// <summary>
    /// The bug this whole file exists for. <c>Ring1</c> is a good save key and unacceptable as
    /// something the player reads, and the fallback in <c>PositionOf</c> would happily pass a
    /// future <c>Ring3</c> straight through — so the rule is stated over every slot rather than
    /// spot-checked on the two that exist today.
    /// </summary>
    [Fact]
    public void NoSlotEverReachesThePlayerWithADigitInItsName()
    {
        foreach (var slot in EquipmentSlots.DisplayOrder)
        {
            Assert.DoesNotContain(EquipmentSlotNames.PositionOf(slot), c => char.IsDigit(c));
            Assert.DoesNotContain(EquipmentSlotNames.CategoryOf(slot), c => char.IsDigit(c));
            Assert.NotEmpty(EquipmentSlotNames.PositionOf(slot));
            Assert.NotEmpty(EquipmentSlotNames.CategoryOf(slot));
        }
    }

    /// <summary>On the character sheet the two rings sit in a list, so the player has to be able
    /// to tell which one they are about to take off.</summary>
    [Fact]
    public void TheTwoRingPositionsAreDistinguishableOnTheCharacterSheet()
    {
        Assert.Equal("Ring I", EquipmentSlotNames.PositionOf(EquipmentSlot.Ring1));
        Assert.Equal("Ring II", EquipmentSlotNames.PositionOf(EquipmentSlot.Ring2));
    }

    /// <summary>
    /// …but on an item card they are both just "a ring". A ring in the stash has not taken a
    /// position yet, and every ring definition declares <c>Ring1</c> regardless of where it will
    /// end up — so "Ring I" on the card would be wrong for whichever one goes on the other hand.
    /// </summary>
    [Fact]
    public void ARingsOwnCardNamesTheKindOfThingItIsRatherThanAPositionItHasNotTakenYet()
    {
        Assert.Equal("ring", EquipmentSlotNames.CategoryOf(EquipmentSlot.Ring1));
        Assert.Equal("ring", EquipmentSlotNames.CategoryOf(EquipmentSlot.Ring2));
    }

    /// <summary>Every other slot already read as English and must not have changed — the two
    /// surfaces differ only in casing there.</summary>
    [Fact]
    public void EverySlotButTheRingsIsUnchangedOnBothSurfaces()
    {
        foreach (var slot in EquipmentSlots.DisplayOrder)
        {
            if (EquipmentSlots.RingPositions.Contains(slot))
                continue;

            Assert.Equal(slot.ToString(), EquipmentSlotNames.PositionOf(slot));
            Assert.Equal(slot.ToString().ToLowerInvariant(), EquipmentSlotNames.CategoryOf(slot));
        }
    }
}
