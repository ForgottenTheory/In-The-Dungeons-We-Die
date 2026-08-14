using Dungeons.Simulation;
using Xunit;

namespace Dungeons.Tests.Simulation;

public class TickEngineTests
{
    [Fact]
    public void CurrentTick_StartsAtZero()
    {
        var engine = new TickEngine();
        Assert.Equal(0, engine.CurrentTick);
    }

    [Fact]
    public void Advance_IncrementsCurrentTick()
    {
        var engine = new TickEngine();
        engine.Advance(5);
        Assert.Equal(5, engine.CurrentTick);
    }

    [Fact]
    public void Advance_RaisesTickAdvancedOncePerTick()
    {
        var engine = new TickEngine();
        var seen = new List<long>();
        engine.TickAdvanced += seen.Add;

        engine.Advance(3);

        Assert.Equal(new long[] { 1, 2, 3 }, seen);
    }

    [Fact]
    public void ScheduledAction_ResolvesOnItsResolveTick()
    {
        var engine = new TickEngine();
        long? resolvedAt = null;
        engine.Schedule(3, () => resolvedAt = engine.CurrentTick);

        engine.Advance(2);
        Assert.Null(resolvedAt); // not yet

        engine.Advance(1);
        Assert.Equal(3, resolvedAt);
    }

    [Fact]
    public void SimultaneousActions_ResolveInScheduleOrder()
    {
        var engine = new TickEngine();
        var order = new List<string>();
        engine.Schedule(2, () => order.Add("first"));
        engine.Schedule(2, () => order.Add("second"));
        engine.Schedule(2, () => order.Add("third"));

        engine.Advance(2);

        Assert.Equal(new[] { "first", "second", "third" }, order);
    }

    [Fact]
    public void Cancel_PreventsResolution()
    {
        var engine = new TickEngine();
        var fired = false;
        var action = engine.Schedule(2, () => fired = true);

        Assert.True(engine.Cancel(action.Id));
        engine.Advance(5);

        Assert.False(fired);
        Assert.False(engine.Cancel(action.Id)); // already gone
    }

    [Fact]
    public void Cancel_OfFutureAction_FromWithinCallback_IsHonoured()
    {
        // Models an interrupt: a callback on tick 1 cancels an action scheduled
        // for a later tick before it can resolve.
        var engine = new TickEngine();
        var firedLater = false;
        var later = engine.Schedule(3, () => firedLater = true);
        engine.Schedule(1, () => engine.Cancel(later.Id));

        engine.Advance(5);

        Assert.False(firedLater);
    }

    [Fact]
    public void Schedule_RejectsNonFutureDelay()
    {
        var engine = new TickEngine();
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Schedule(0, () => { }));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Schedule(-1, () => { }));
    }

    [Fact]
    public void Advance_RejectsNegativeTicks()
    {
        var engine = new TickEngine();
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Advance(-1));
    }

    [Fact]
    public void PendingCount_ReflectsScheduledAndResolvedActions()
    {
        var engine = new TickEngine();
        engine.Schedule(2, () => { });
        engine.Schedule(4, () => { });
        Assert.Equal(2, engine.PendingCount);

        engine.Advance(2);
        Assert.Equal(1, engine.PendingCount);

        engine.Advance(2);
        Assert.Equal(0, engine.PendingCount);
    }
}
