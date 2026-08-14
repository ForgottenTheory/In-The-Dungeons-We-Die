using Dungeons.Content;
using Dungeons.Professions;
using Dungeons.Randomness;

namespace Dungeons.Tests.Professions;

/// <summary>Deterministic RNG stub and store helpers for profession tests.</summary>
internal sealed class FakeRandom : IRandomSource
{
    private readonly Queue<double> _doubles;
    private readonly double _default;

    public FakeRandom(double @default = 0.99, params double[] sequence)
    {
        _default = @default;
        _doubles = new Queue<double>(sequence);
    }

    public double NextDouble() => _doubles.Count > 0 ? _doubles.Dequeue() : _default;

    public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
}

internal static class ProfessionsTestData
{
    public static DataStore<T> Store<T>(params T[] items) where T : IDefinition
    {
        var store = new DataStore<T>();
        foreach (var item in items)
            store.Add(item);
        return store;
    }

    public static ItemAmountData Amount(string itemId, int quantity = 1) => new() { ItemId = itemId, Quantity = quantity };

    public static ItemChanceData Chance(string itemId, double chance, int quantity = 1) =>
        new() { ItemId = itemId, Chance = chance, Quantity = quantity };
}
