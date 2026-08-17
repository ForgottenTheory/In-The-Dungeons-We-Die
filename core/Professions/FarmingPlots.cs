using Dungeons.Content;
using Dungeons.Items;

namespace Dungeons.Professions;

/// <summary>Balance constants for the Hideout's growing plots.</summary>
public static class FarmingTuning
{
    /// <summary>Farming levels at which an additional plot opens. The count of entries at or
    /// below the player's level <em>is</em> the number of plots, so the ladder is the data.</summary>
    public static readonly IReadOnlyList<int> PlotUnlockLevels = new[] { 1, 5, 15, 30, 50, 70 };

    public static int MaximumPlots => PlotUnlockLevels.Count;

    /// <summary>How many plots are workable at <paramref name="farmingLevel"/>.</summary>
    public static int UnlockedPlots(int farmingLevel)
    {
        var unlocked = 0;
        foreach (var level in PlotUnlockLevels)
        {
            if (farmingLevel >= level)
                unlocked++;
        }

        return unlocked;
    }
}

/// <summary>Why a plant or harvest request was refused.</summary>
public enum PlotFailure
{
    None,
    NoSuchPlot,
    PlotLocked,
    PlotOccupied,
    PlotEmpty,
    NotFarming,
    StillGrowing,
    ActionUnavailable,
}

/// <summary>One growing plot: what is in it and when it finishes.</summary>
public sealed class FarmingPlot
{
    public FarmingPlot(int index) => Index = index;

    public int Index { get; }
    public string? PlantedActionId { get; internal set; }
    public long ReadyAtTick { get; internal set; }

    public bool IsEmpty => PlantedActionId is null;

    public bool IsReadyAt(long currentTick) => !IsEmpty && currentTick >= ReadyAtTick;

    /// <summary>Growth progress in [0, 1] for the UI. Needs the tick the crop was planted on,
    /// which the plot does not store — the caller passes the interval it was planted with.</summary>
    public double Progress(long currentTick, long growTicks)
    {
        if (IsEmpty || growTicks <= 0)
            return 0.0;
        var remaining = ReadyAtTick - currentTick;
        return Math.Clamp(1.0 - ((double)remaining / growTicks), 0.0, 1.0);
    }
}

/// <summary>
/// The Hideout's Farming plots: plant a seed, walk away, come back to a crop.
///
/// <para>This is the one profession that runs in parallel with itself, which is the whole
/// reason it needs a system of its own rather than a row in the passive runner. Everything
/// else is borrowed: a planting is an ordinary <see cref="ProfessionActionDefinition"/>, its
/// inputs are the seed, its outputs are the crop (plus seeds, so a bed sustains itself), and
/// its <see cref="ProfessionActionDefinition.BaseIntervalTicks"/> is the grow time. Harvest
/// runs through <see cref="ProfessionSystem.CompletePrepaidAction"/>, so XP, mastery and
/// bonus outputs behave exactly as they do everywhere else.</para>
///
/// <para>Growth is measured in absolute ticks, so it advances while the player is away for
/// the same reason offline progress does — the clock, not the frame loop, decides.</para>
/// </summary>
public sealed class FarmingPlots
{
    public const string FarmingProfessionId = "profession.farming";

    private readonly DataStore<ProfessionActionDefinition> _actions;
    private readonly ProfessionSystem _professions;
    private readonly Func<Inventory> _inventoryProvider;
    private readonly List<FarmingPlot> _plots = new();

    public FarmingPlots(
        DataStore<ProfessionActionDefinition> actions,
        ProfessionSystem professions,
        Func<Inventory> inventoryProvider)
    {
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _professions = professions ?? throw new ArgumentNullException(nameof(professions));
        _inventoryProvider = inventoryProvider ?? throw new ArgumentNullException(nameof(inventoryProvider));

        for (var index = 0; index < FarmingTuning.MaximumPlots; index++)
            _plots.Add(new FarmingPlot(index));
    }

    public IReadOnlyList<FarmingPlot> Plots => _plots;

    /// <summary>Plots currently workable, from the live Farming level.</summary>
    public int UnlockedPlots =>
        FarmingTuning.UnlockedPlots(_professions.GetProgress(FarmingProfessionId).Level);

    /// <summary>Every Farming action the player could plant right now, level gate included.</summary>
    public IReadOnlyList<ProfessionActionDefinition> PlantableActions()
    {
        var level = _professions.GetProgress(FarmingProfessionId).Level;
        return _actions.GetAll()
            .Where(action => action.ProfessionId == FarmingProfessionId && action.RequiredLevel <= level)
            .OrderBy(action => action.RequiredLevel)
            .ThenBy(action => action.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Sows <paramref name="actionId"/> in a plot, taking its seed now. The crop
    /// arrives at the action's interval from <paramref name="currentTick"/>.</summary>
    public PlotFailure Plant(int plotIndex, string actionId, long currentTick)
    {
        if (plotIndex < 0 || plotIndex >= _plots.Count)
            return PlotFailure.NoSuchPlot;
        if (plotIndex >= UnlockedPlots)
            return PlotFailure.PlotLocked;

        var plot = _plots[plotIndex];
        if (!plot.IsEmpty)
            return PlotFailure.PlotOccupied;

        if (!_actions.TryGetById(actionId, out var action))
            return PlotFailure.ActionUnavailable;
        if (action.ProfessionId != FarmingProfessionId)
            return PlotFailure.NotFarming;
        if (_professions.GetProgress(FarmingProfessionId).Level < action.RequiredLevel)
            return PlotFailure.ActionUnavailable;

        // Checked against *this* system's bag, not ProfessionSystem's. The two differ inside a
        // Realm — profession actions deposit into the unsecured run inventory, while plots are
        // always tended at the Hideout — so asking ProfessionSystem.CheckExecutable here would
        // read one bag and then take the seed out of another.
        var bag = _inventoryProvider();
        if (action.Inputs.Count > 0 && !bag.CanRemoveAll(action.Inputs))
            return PlotFailure.ActionUnavailable;

        // The seed is spent at planting. Nothing else consumes it later — the harvest is
        // deliberately prepaid.
        if (action.Inputs.Count > 0)
            bag.TryRemoveAll(action.Inputs);

        plot.PlantedActionId = actionId;
        plot.ReadyAtTick = currentTick + _professions.EffectiveIntervalTicks(actionId);
        Planted?.Invoke(plot);
        return PlotFailure.None;
    }

    /// <summary>Lifts a finished crop, returning the same outcome any other action would.</summary>
    public ActionOutcome? Harvest(int plotIndex, long currentTick, out PlotFailure failure)
    {
        failure = PlotFailure.None;
        if (plotIndex < 0 || plotIndex >= _plots.Count)
        {
            failure = PlotFailure.NoSuchPlot;
            return null;
        }

        var plot = _plots[plotIndex];
        if (plot.IsEmpty)
        {
            failure = PlotFailure.PlotEmpty;
            return null;
        }

        if (currentTick < plot.ReadyAtTick)
        {
            failure = PlotFailure.StillGrowing;
            return null;
        }

        var actionId = plot.PlantedActionId!;
        plot.PlantedActionId = null;
        plot.ReadyAtTick = 0;

        var outcome = _professions.CompletePrepaidAction(actionId);
        Harvested?.Invoke(plot, outcome);
        return outcome;
    }

    /// <summary>Harvests every ready plot — what the client calls on returning from an absence.</summary>
    public IReadOnlyList<ActionOutcome> HarvestAllReady(long currentTick)
    {
        var harvested = new List<ActionOutcome>();
        foreach (var plot in _plots)
        {
            if (!plot.IsReadyAt(currentTick))
                continue;
            var outcome = Harvest(plot.Index, currentTick, out _);
            if (outcome is not null)
                harvested.Add(outcome);
        }

        return harvested;
    }

    public event Action<FarmingPlot>? Planted;
    public event Action<FarmingPlot, ActionOutcome>? Harvested;

    /// <summary>Replaces all plot state (used when loading a save).</summary>
    public void Restore(IEnumerable<(int Index, string ActionId, long ReadyAtTick)> plantings)
    {
        ArgumentNullException.ThrowIfNull(plantings);
        foreach (var plot in _plots)
        {
            plot.PlantedActionId = null;
            plot.ReadyAtTick = 0;
        }

        foreach (var planting in plantings)
        {
            if (planting.Index < 0 || planting.Index >= _plots.Count)
                continue;
            if (!_actions.Contains(planting.ActionId))
                continue; // the action was removed from content since the save was written
            _plots[planting.Index].PlantedActionId = planting.ActionId;
            _plots[planting.Index].ReadyAtTick = planting.ReadyAtTick;
        }
    }
}
