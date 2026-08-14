using Dungeons.Characters.Modifiers;
using Xunit;

namespace Dungeons.Tests.Characters;

public class ModifierPipelineTests
{
    [Fact]
    public void AddThenMultiply_InThatOrder()
    {
        var mods = new[]
        {
            new StatModifier(StatId.Strength, ModifierOperation.Add, 2),
            new StatModifier(StatId.Strength, ModifierOperation.Multiply, 2),
        };
        // (5 + 2) * 2 = 14, not 5 + (2*2)
        Assert.Equal(14, ModifierPipeline.ResolveInt(StatId.Strength, 5, mods));
    }

    [Fact]
    public void IgnoresModifiersForOtherStats()
    {
        var mods = new[] { new StatModifier(StatId.Dexterity, ModifierOperation.Add, 100) };
        Assert.Equal(5, ModifierPipeline.ResolveInt(StatId.Strength, 5, mods));
    }

    [Fact]
    public void MultipleMultipliersCompound()
    {
        var mods = new[]
        {
            new StatModifier(StatId.MaxHealth, ModifierOperation.Multiply, 1.5),
            new StatModifier(StatId.MaxHealth, ModifierOperation.Multiply, 2.0),
        };
        Assert.Equal(300, ModifierPipeline.ResolveInt(StatId.MaxHealth, 100, mods));
    }

    [Fact]
    public void ClampsToMinimum()
    {
        var mods = new[] { new StatModifier(StatId.Strength, ModifierOperation.Add, -100) };
        Assert.Equal(0, ModifierPipeline.ResolveInt(StatId.Strength, 5, mods));
    }
}
