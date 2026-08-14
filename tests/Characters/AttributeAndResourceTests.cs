using Dungeons.Characters;
using Xunit;

namespace Dungeons.Tests.Characters;

public class AttributeAndResourceTests
{
    [Fact]
    public void AttributeSet_IndexerAndWith()
    {
        var set = AttributeSet.Uniform(5).With(AttributeType.Strength, 9);
        Assert.Equal(9, set[AttributeType.Strength]);
        Assert.Equal(5, set[AttributeType.Luck]);
    }

    [Fact]
    public void AttributeSet_AddAndPlus()
    {
        var a = AttributeSet.Uniform(1).Add(AttributeType.Dexterity, 3);
        var b = AttributeSet.Uniform(2);
        var sum = a.Plus(b);
        Assert.Equal(6, sum[AttributeType.Dexterity]); // 1+3+2
        Assert.Equal(3, sum[AttributeType.Strength]);   // 1+2
    }

    [Fact]
    public void AttributeSet_EqualityByValue()
    {
        Assert.Equal(AttributeSet.Uniform(4), AttributeSet.Uniform(4));
        Assert.NotEqual(AttributeSet.Uniform(4), AttributeSet.Uniform(4).With(AttributeType.Luck, 5));
    }

    [Fact]
    public void ResourceCalculator_IsDeterministic()
    {
        var a = AttributeSet.Uniform(5);
        Assert.Equal(20 + 30 + 15, ResourceCalculator.MaxHealth(a));   // CON*6 + END*3
        Assert.Equal(20 + 25 + 10, ResourceCalculator.MaxStamina(a));  // END*5 + DEX*2
        Assert.Equal(10 + 25 + 15, ResourceCalculator.MaxMana(a));     // INT*5 + WIS*3
    }

    [Fact]
    public void ResourcePool_ClampsAndReports()
    {
        var pool = new ResourcePool(ResourceType.Health, 100);
        Assert.Equal(100, pool.Current);
        Assert.Equal(1.0, pool.Fraction);

        Assert.Equal(30, pool.Reduce(30));
        Assert.Equal(70, pool.Current);

        Assert.Equal(70, pool.Reduce(999)); // only removes what's there
        Assert.True(pool.IsDepleted);
        Assert.Equal(0.0, pool.Fraction);

        Assert.Equal(40, pool.Restore(40));
        Assert.Equal(40, pool.Current);
        Assert.Equal(60, pool.Restore(999)); // clamps at max
        Assert.Equal(100, pool.Current);
    }

    [Fact]
    public void ResourcePool_LoweringMaxClampsCurrent()
    {
        var pool = new ResourcePool(ResourceType.Mana, 50);
        pool.Max = 20;
        Assert.Equal(20, pool.Current);
    }

    [Fact]
    public void ResourcePool_RejectsNegativeAmounts()
    {
        var pool = new ResourcePool(ResourceType.Stamina, 10);
        Assert.Throws<ArgumentOutOfRangeException>(() => pool.Reduce(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => pool.Restore(-1));
    }
}
